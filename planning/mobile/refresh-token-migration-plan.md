# Refresh Token Migration Plan

> **Scope:** introduce OAuth2-style access+refresh token pattern across the Cleansia backend + all three Angular web apps + the Android customer app (in progress). This replaces the current single-JWT-with-long-lifetime design.
>
> **Driver:** Android customer app rollout. Doing it now while greenfield is cheaper than retrofitting post-launch.
>
> **Status (2026-04-18):** **Backend implementation complete.** Awaiting: migration apply (MANUAL), integration tests, cleanup function, NSwag regen, web-app interceptor upgrades.

---

## Background facts (from code audit)

- All four API projects (Web/Admin/Mobile/Customer) share a single `TokenService` in `Cleansia.Core.AppServices/Services/TokenService.cs`
- Handlers (`Login`, `GoogleAuth`, `ConfirmUserEmail`, etc.) are **shared** across APIs — one handler in Core.AppServices, all four controllers call it via MediatR
- `User` entity has no refresh-token fields today — needs a new `RefreshToken` entity (or a field on a `UserSession` entity — see decision below)
- Token TTL is already asymmetric: Web/Admin = 6h, Mobile/Customer = 168h (7 days). All will become 15 minutes post-migration.
- `JwtTokenResponse` record has 5 fields — will add `refreshToken` + `refreshTokenExpiresAt`
- Three Angular web apps have trivial auth interceptors — attach Bearer, no 401 retry, no refresh awareness. All three need to be updated.
- EF entity convention: inherit from `Auditable` (has `CreatedBy`, `UpdatedBy`, `DeactivatedBy`, `IsActive` soft-delete), string (ULID) PK, nullable `TenantId`
- Migrations are MANUAL — owner (Michael) runs `dotnet ef migrations add` + `database update`. Claude writes the migration code, does not apply it.

---

## Design decisions (locked in)

### Entity shape: `RefreshToken` as its own aggregate

Not a field on `User`. Reasons:
- A single user can have multiple active sessions (phone + web + tablet). Each needs its own revocable refresh token.
- We need audit trail: when was this session created, from what IP/device, when was it last used, when was it revoked
- Enables "log out from all devices" feature cheaply

**Fields:**

| Field | Type | Notes |
|---|---|---|
| `Id` | `string` (ULID) | PK, same convention as rest of the codebase |
| `UserId` | `string` (ULID) | FK to User, indexed |
| `TokenHash` | `string` | SHA-256 of the raw token — never store raw |
| `CreatedAt` | `DateTime` | inherited from `Auditable` |
| `ExpiresAt` | `DateTime` | sliding window — extended on each use |
| `LastUsedAt` | `DateTime?` | null until first refresh |
| `RevokedAt` | `DateTime?` | null until revoked |
| `RevokedReason` | `string?` | `"logout" \| "rotated" \| "admin" \| "security"` |
| `ReplacedByTokenId` | `string?` | set when rotated — chain for forensics |
| `DeviceLabel` | `string?` | e.g. `"Pixel 9 Pro (Chrome)"` — UA-derived best-effort |
| `IpAddress` | `string?` | audit only, not used for validation |

Inherits `Auditable` → gets `IsActive`, `CreatedBy`, `TenantId` for free.

### Token format

**Access token:** unchanged — same JWT claims as today, short lifetime (15 min).

**Refresh token:** opaque random 64-char base64url string. Server stores SHA-256 hash only. Returned to client in response body once (never retrievable again).

### Rotation strategy

Every `POST /api/auth/RefreshToken` call:
1. Client sends its current refresh token in the request body
2. Server hashes it, looks up by hash, validates: exists, not expired, not revoked, user not deactivated
3. Server **rotates**: marks current refresh token as `RevokedAt = now, RevokedReason = "rotated"`, creates a new refresh token, sets old one's `ReplacedByTokenId` to new one's id
4. Returns new access + new refresh token
5. If the client ever sends a revoked-by-rotation token → **security alarm**: this means someone else is using a stolen token. Revoke the entire chain for this user and force re-login everywhere. (Log + optional Sentry alert)

### Lifetime policy

| Token | Lifetime | Configurable? |
|---|---|---|
| Access | **15 min** (fixed) | Yes, per-API in appsettings — but same default everywhere |
| Refresh | **30 days** sliding | Yes, per-API |

"Remember me = false" on login returns a refresh token with **1 day** lifetime instead of 30. This preserves the existing `rememberMe` semantic without making session expiry asymmetric with access-token expiry.

### Logout endpoint contract

`POST /api/auth/Logout` — authenticated endpoint.

Body: `{ refreshToken: string }` (the client's current refresh token).

Server action: marks that token as `RevokedAt = now, RevokedReason = "logout"`. Returns 204.

"Log out all sessions" = future enhancement; revoke all tokens where `UserId = me and RevokedAt is null`. Out of scope for v1.

### Refresh endpoint contract

`POST /api/auth/RefreshToken` — **anonymous** endpoint (you don't have a valid access token if you're refreshing).

Body: `{ refreshToken: string }`.

Response: `JwtTokenResponse` (same shape as login) — new access + new refresh.

Errors:
- 401 if the refresh token is invalid/expired/revoked/not found
- 401 if the user was deactivated since token issue

### Security behavior on 401

- Web: interceptor catches 401 on any protected call, triggers refresh, retries original request once. If refresh itself returns 401, wipe tokens + redirect to login.
- Android: same logic, in an OkHttp Authenticator (cleaner than Interceptor for auth-specifically, because OkHttp automatically retries once).

### JwtTokenResponse new shape

```csharp
record JwtTokenResponse(
    string Token,
    string? RefreshToken,              // new — null if email unconfirmed
    DateTime? RefreshTokenExpiresAt,   // new — for client-side display ("session expires in 29 days")
    bool IsEmailConfirmed,
    bool HasAdminAccess = true,
    string? UserId = null,
    string? Email = null)
```

### Cleanup job

Revoked + expired refresh tokens are kept 90 days for forensic purposes, then hard-deleted via a daily cleanup task. Added to `Cleansia.Functions` (Azure Functions project).

---

## Execution sequence

### Backend (must ship first, before any client work)

1. **Domain entity** — `src/Cleansia.Core.Domain/Users/RefreshToken.cs` + navigation on `User`
2. **EF configuration** — `src/Cleansia.Infra.Database/Configurations/RefreshTokenConfiguration.cs`. Indexes: `(UserId, RevokedAt)` composite, `TokenHash` unique
3. **Migration** — Michael runs: `dotnet ef migrations add AddRefreshTokens --project src/Cleansia.Infra.Database --startup-project src/Cleansia.Web` (once from any startup project, the migration is shared)
4. **RefreshTokenService** — `src/Cleansia.Core.AppServices/Services/RefreshTokenService.cs`. Methods: `Issue(userId)`, `Rotate(rawToken)`, `Revoke(rawToken, reason)`, `RevokeAllForUser(userId, reason)`. Handles hashing, validation, rotation, theft detection.
5. **TokenService update** — add a new method `GenerateAccessToken` with short lifetime; keep existing signature but point it at the new implementation. Access tokens now 15min.
6. **Handler updates** — `Login`, `GoogleAuth`, `ConfirmUserEmail` handlers now call `RefreshTokenService.Issue` and populate the new `JwtTokenResponse` fields.
7. **Two new handlers + endpoints:**
   - `RefreshToken.Command` + handler, exposed as `POST /api/auth/RefreshToken` on all 4 AuthControllers
   - `Logout.Command` + handler, exposed as `POST /api/auth/Logout` on all 4 AuthControllers (authenticated)
8. **appsettings updates** — bump `DefaultTokenExpMinutes = 15` in all 4 APIs. Add `RefreshTokenLifetimeDays = 30` and `RememberMeShortLifetimeDays = 1`.
9. **Cleanup function** — `src/Cleansia.Functions/RefreshTokenCleanupFunction.cs`, daily timer trigger, deletes `WHERE (RevokedAt IS NOT NULL OR ExpiresAt < NOW()) AND (DeactivatedAt < NOW() - 90 days)`.
10. **Integration tests** — `src/Cleansia.IntegrationTests/Auth/RefreshTokenFlowTests.cs`: happy path, expired, revoked-by-rotation detection, logout, user-deactivated-mid-session.

**Estimated backend effort:** 1–1.5 days.

**MANUAL_STEP list for Michael:**
- Run `dotnet ef migrations add AddRefreshTokens ...` after step 3
- Run `dotnet ef database update ...` once satisfied with the migration SQL
- Run `npm run generate-partner-client && npm run generate-admin-client && npm run generate-customer-client` after backend work is done to regenerate NSwag clients for the three web apps

### Web apps (ship in parallel with backend, or immediately after)

For each of Partner / Admin / Customer Angular apps:

1. **TokenStore service** — wrap localStorage reads/writes. Exposes `accessToken`, `refreshToken`, `clear()`.
2. **AuthInterceptor upgrade** — on 401: call refresh endpoint, retry original request once. Use `HttpInterceptorFn` with `switchMap`. Standard RxJS pattern.
3. **Login flow** — update to store both tokens returned by backend.
4. **Logout flow** — call new `/api/auth/Logout` endpoint before wiping tokens.
5. **App bootstrap** — on startup, check if access token is expired; if yes and refresh token exists, refresh first before any API call.

**Estimated web effort:** 0.5 day total for all three apps (they share the same pattern).

### Android (blocked on backend; resumes Phase 2 once backend is live)

Picks up from the paused Phase 2 work. Now includes:

1. **Generated OpenAPI client** includes `/RefreshToken` and `/Logout` endpoints automatically.
2. **TokenStore** — EncryptedSharedPreferences. Stores both tokens + access expiry (decoded from JWT).
3. **AuthAuthenticator** — OkHttp `Authenticator` (not Interceptor). On 401, calls refresh endpoint, returns new request with updated `Authorization` header. OkHttp retries exactly once automatically. If refresh itself fails, emits a logout signal via a shared flow.
4. **AuthRepository** — `login()`, `register()`, `confirmEmail()`, `googleAuth()`, `logout()` (calls backend logout then clears TokenStore), `isSessionValid()`.
5. **Session state** — a singleton `SessionManager` exposes `Flow<SessionState>` (Authenticated / Unauthenticated). Navigation listens and kicks to SignIn on logout.
6. **Screen wiring** — SignIn, SignUp, EmailVerify, ForgotPassword, account-deletion row.

**Estimated Android effort:** 1 day after backend is available.

---

## Rollout sequence

No existing users yet (pre-launch). No migration dance needed for real data. Do NOT:
- Keep a compatibility layer for old long-lived tokens
- Accept old-format tokens alongside new ones
- Worry about forced-logout user communication

Do:
- Ship the backend change as a single PR
- Web apps get updated in the same sprint
- Android integrates in the PR that follows

---

## Open questions (answer before backend work starts)

1. **Access token lifetime** — 15min is my recommendation. Industry typical is 5–30min. Shorter = more refresh traffic, more secure. Longer = less refresh traffic, longer window for a stolen access token. Confirm 15 min is OK.
2. **Refresh token family revocation** — my plan says "if rotation detects reuse, revoke the whole chain." Agree, or leave it as just revoking the single reused token? My rec: revoke the chain. Standard OAuth2 practice.
3. **Remember-me short lifetime** — 1 day for `rememberMe=false` vs 30 days for `rememberMe=true`. Agree? Or should `rememberMe=false` just mean "refresh token expires when you close the tab/app" — which we can't reliably detect on native, so 1 day is the practical equivalent.
4. **Device label** — best-effort from User-Agent header. Android will need to send a custom header (`X-Device-Label: "Pixel 9 Pro"`). Web gets it from UA. OK?
5. **Token hash algorithm** — SHA-256 (plan assumes this). Alternatives: bcrypt (overkill for random-64-char tokens that already have 384 bits of entropy), HMAC-SHA256 with a secret (slightly more defense in depth). Confirm SHA-256 or specify.

---

## Not in scope for v1

- "Log out from all devices" UI (backend supports it, no UI)
- Device management page ("here are your active sessions")
- Push-based session invalidation ("log out all devices NOW" from admin)
- Biometric-locked refresh token (already rejected per master plan)
- Per-device permission scopes (no scopes today, won't add here)

---

## Related docs

- Master plan: `customer-app-master-plan.md` — this work lives in new Phase 1.5 ahead of Phase 2 execution
- Backend auth facts: see summary earlier in this thread, TokenService.cs + JwtTokenResponse.cs are the anchors
