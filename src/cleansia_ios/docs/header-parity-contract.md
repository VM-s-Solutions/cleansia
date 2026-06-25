# Mobile API header-parity contract — the invisible out-of-band rules

> **What this is.** The Mobile API contract has rules that the OpenAPI spec does **not** describe: custom
> request headers, a no-`Bearer`-on-anonymous-endpoints rule, a refresh-token rotation/theft protocol, and
> a 200-with-empty-token login special case. Android honours them by hand in `core/auth`; iOS must honour
> the **same** rules or it breaks remote device-revoke, leaks tokens to anonymous endpoints, or self-revokes
> a healthy session. This document writes them down so the iOS auth spine (the `HeaderAdapter` /
> `SessionRefresher` / `AuthClient`) is built against a spec, not by reverse-engineering Kotlin.
>
> **Authoritative sources** (read these if anything here is ambiguous):
> - Android: `src/cleansia_android/core/src/main/java/cz/cleansia/core/auth/` —
>   `AuthInterceptor.kt`, `DeviceIdProvider.kt`, `AuthAuthenticator.kt`, `TokenStore.kt`; per-app
>   `NetworkModule.kt` / `AuthModule.kt` (the `X-Time-Zone` interceptor); `AuthRepository.kt` (login outcomes).
> - Backend: `Cleansia.Core.AppServices/Services/RefreshTokenService.cs` (rotation + theft detection),
>   `Cleansia.Web.Mobile.{Partner,Customer}/Controllers/*.cs` (the `[AllowAnonymous]` surface),
>   `Cleansia.Core.AppServices/Features/Devices/{RegisterDevice,RevokeDevice}.cs` (the device-id invariant),
>   `Cleansia.Core.AppServices/Shared/DTOs/ResponseModels/JwtTokenResponse.cs` (the login body).
>
> **Scope.** This is the spec the iOS auth ticket (the hand-written auth/session/header middleware)
> implements against. Gate-AR / Gate-DP are **N/A** — this is an infra/contract document, no screen.

---

## 1. The three custom request headers

Attached to outgoing requests by the iOS `HeaderAdapter` (the parity of Android's `AuthInterceptor` +
the per-request `X-Time-Zone` interceptor). All values are **ASCII-only** and **length-capped** —
HTTP header values reject non-ASCII (em-dash, middle-dot, accented device names), and the server columns
are bounded.

| Header | Value | Sent on | Cap | iOS source |
|---|---|---|---|---|
| `X-Device-Id` | the stable per-install id (§2) | **every** request | 64 chars, code-points 32–126 only | `DeviceIdProvider` (one source) |
| `X-Device-Label` | human-readable device, e.g. `iPhone 14 Pro - iOS 17.4` | **every** request | 120 chars, ASCII | `HeaderAdapter` (from `UIDevice`) |
| `X-Time-Zone` | the IANA tz id, e.g. `Europe/Prague` | **every** request | — | read **per request** from `TimeZone.current.identifier` |

Notes:
- **`X-Device-Id`** is the load-bearing one — §2 is its own section. **It must equal the `deviceId`
  body field sent to `POST /api/Device/Register`** or remote device-revoke silently no-ops (§2).
- **`X-Device-Label`** is best-effort audit metadata only ("signed in from iPhone 14 Pro"); the server
  stamps it on the refresh-token record. Nothing keys off it. ASCII-filter + 120-char cap to match the
  Android `deviceLabel()` and the server column. Build it from `UIDevice.current.name`/`.model` +
  `systemName`/`systemVersion`; strip non-ASCII.
- **`X-Time-Zone`** must be read **fresh on every request** (Android reads `TimeZone.getDefault()` per
  call), so a system time-zone change is picked up on the next call without an app restart. Do **not**
  cache it at adapter construction. The server uses it to compute day/week/month boundaries (dashboard
  counts, earnings/revenue windows) in the user's wall clock instead of UTC — without it a cleaner who
  finishes a job at 00:30 local sees it under "yesterday".
- These headers go on **both** the authenticated session and the no-auth refresh session (Android adds
  the time-zone interceptor to both OkHttp clients; the device headers ride the auth interceptor on the
  main client — on iOS, attach `X-Device-Id`/`X-Device-Label`/`X-Time-Zone` on **both** sessions so the
  refresh call is also attributable and tz-correct).

---

## 2. `X-Device-Id` — ONE stable per-install id (the device-revoke invariant)

**The single most breakable rule on the whole auth surface.** There must be exactly **one** source of
the device id in the app, and it must feed **two** consumers with the identical string:

1. The **`X-Device-Id` header** on every request.
2. The **`deviceId` body field** of `POST /api/Device/Register` (push registration —
   `RegisterDeviceRequest { deviceId, deviceToken, platform }`).

### Why one source — the revoke chain

The backend wires remote "sign out this device" through a **string match on the device id**:

- At login/refresh, the server stamps the incoming **`X-Device-Id` header** onto the issued
  `RefreshToken.DeviceId` (and carries it across rotation — see `RefreshTokenService.RotateAsync`,
  `carriedDeviceId`).
- `POST /api/Device/Register` stores `Device.DeviceId = body.deviceId` (`RegisterDevice` handler).
- "Your devices" → revoke calls `RevokeDevice`, which runs
  `RefreshTokenService.RevokeByDeviceAsync(userId, device.DeviceId, "device_revoked")`. That method
  revokes refresh tokens **where `RefreshToken.DeviceId == Device.DeviceId`**.

So the kill works **only if** `X-Device-Id` header (what's on the refresh token) **==** the `deviceId`
that was registered (what's on the `Device` row). A second id source, or a per-launch random id, or a
value that drifts between the header and the register body, means **revoke matches nothing and silently
no-ops** — the revoked device keeps refreshing forever. `RevokeByDeviceAsync` also has a load-bearing
null-guard: a refresh token with **no** `DeviceId` never matches, so an early request that forgot the
header survives until natural expiry rather than being swept by an unrelated device.

### iOS implementation rule

- A single `DeviceIdProvider` in `CleansiaCore` is the **only** place the id is produced. Both the
  `HeaderAdapter` and the push/device-registration client read it from there. Never compute a device id
  anywhere else.
- **Stability:** the id must be **stable for the install** and survive app restarts and updates — it is
  persisted in the **Keychain** (not `UserDefaults`, which is wiped on uninstall but is otherwise the
  weaker choice; Keychain can also be configured to survive reinstalls but the v1 target is per-install
  stability). Generate a `UUID` once on first launch, store it, and return the stored value thereafter.
  - This is the deliberate iOS analogue of Android's `Settings.Secure.ANDROID_ID` (stable per-install,
    per-app-signing-key, resets on factory reset). `identifierForVendor` is **not** a safe substitute on
    its own — it can change when all vendor apps are uninstalled — so persist your own generated UUID
    and treat IDFV only as an optional seed.
- **Per-app, not per-device:** the partner and customer apps each have their own id (separate Keychain
  items / bundle ids), exactly like Android's per-app-signing-key `ANDROID_ID` scoping, so their `Device`
  rows don't collide on one handset.
- **ASCII + 64-char cap** before it goes on the wire (a `UUID` string is already ASCII and 36 chars, so
  the filter is a no-op on the normal path — keep it as the guard).

---

## 3. The no-`Bearer`-on-anonymous rule + the full anon allow-list

### The rule

`/api/Auth/*` endpoints are **anonymous** and some **reject an unexpected `Authorization: Bearer`**. The
`HeaderAdapter` must **skip the `Authorization` header entirely** when the request path matches an
anonymous endpoint — even if a (stale/revoked) access token is stored. Android does this with a
path-contains check in `AuthInterceptor`; iOS does the same. The device/tz headers (§1) are **still
sent** on anon endpoints — only `Authorization` is withheld.

A separate **no-auth session** (the parity of Android's `NoAuthOkHttp`) handles the refresh call so a
401 on refresh can never recursively trigger another refresh. The path-skip and the separate session are
belt-and-braces: either alone keeps the Bearer off the refresh call, but keep both.

### The allow-list — and the customer host's wider surface

The allow-list is **host-specific**. Match these by path (case-insensitive), mirroring the backend's
`[AllowAnonymous]` attributes. The partner mobile host's anonymous surface is **auth-only**; the customer
mobile host additionally exposes the **pre-account booking flow** as anonymous (a guest can price and
place an order before signing in), so its allow-list is larger.

**Both hosts — `/api/Auth/*` (+ password reset on `/api/User/*`):**

| Path | Method | Notes |
|---|---|---|
| `/api/Auth/Login` | POST | partner host binds the partner-only login command; customer host the permissive one |
| `/api/Auth/Register` | POST | partner host also has `/api/Auth/RegisterEmployee` (POST) |
| `/api/Auth/GoogleAuth` | POST | both hosts |
| `/api/Auth/ConfirmUserEmail` | PUT | |
| `/api/Auth/ResendConfirmationEmail` | POST | |
| `/api/Auth/ForgotPassword` | POST | |
| `/api/Auth/RefreshToken` | POST | always goes via the no-auth session |
| `/api/User/RequestPasswordChange` | PUT | password reset runs pre-session |
| `/api/User/ChangePassword` | PUT | |

> `/api/Auth/Logout` is **`[Authorize]`** (NOT anonymous) on both hosts — it needs the Bearer to identify
> the session, and it carries the refresh token in the body to revoke. Do **not** add it to the allow-list.

**Customer host ONLY — the anonymous guest-booking surface (in addition to the above):**

| Path | Method | Purpose |
|---|---|---|
| `/api/Service/GetOverview` | GET | service catalogue for the booking wizard |
| `/api/Package/GetOverview` | GET | package catalogue |
| `/api/Extra/GetOverview` | GET | extras catalogue |
| `/api/Membership/GetPlans` | GET | membership/Plus plans |
| `/api/Order/Quote` | POST | server-side price quote before account |
| `/api/Order/CreateOrder` | POST | guest order creation |
| `/api/Order/Lookup` | GET | look up a guest order |
| `/api/Order/LookupBatch` | POST | batch guest-order lookup |
| `/api/Payment/CreateOrder` | POST | guest payment-intent creation |
| `/api/Referral/Validate` | POST | validate a referral code at signup |

> `/api/Payment/webhook` is also `[AllowAnonymous]` (Stripe is unauthenticated; the signature is its auth)
> but it is **server-to-server only** — the iOS app never calls it, so it is not part of the client
> allow-list. Listed here only so it isn't mistaken for a client-callable anon endpoint.

### iOS implementation rule

- Encode the allow-list as a **path-contains, case-insensitive** match (Android uses `contains`), keyed
  per app target (the partner adapter carries only the auth list; the customer adapter carries the auth
  list **plus** the guest-booking list).
- A mismatch is a security bug in **both directions**: omitting an auth path leaks a stale Bearer to an
  endpoint that may reject it (breaking login/refresh); over-broadly skipping an authed endpoint drops the
  Bearer and 401s a real call. The security gate on the auth-spine ticket checks this list is complete and
  host-correct.

---

## 4. Refresh = single-use with theft detection (replace the stored refresh token every refresh)

Refresh tokens are **single-use and rotating**. Every successful refresh returns a **new** refresh token;
the old one is marked `rotated` and revoked server-side. The client **must overwrite the stored refresh
token with the new one on every refresh** — keep using the old one and the server treats the second use
as **token theft** and revokes the entire chain.

### The server protocol (`RefreshTokenService.RotateAsync`)

1. The presented token is looked up by hash.
2. If it was **already rotated** (revoked with reason `"rotated"`) and is presented **again** →
   **rotation-reuse / theft signal**: the server revokes the **whole token chain** for the user
   (`RevokeChainAsync`), commits that revocation immediately, and returns a failure (the user is forced
   to sign in again on **every** device). This is the protection — and the foot-gun: a client that
   doesn't persist the rotated token will trip it on its *next* refresh.
3. Otherwise a **new** token is issued with a fresh sliding-window expiry; the old one is marked used +
   revoked (`"rotated"`) with `ReplacedByTokenId` set (the forensic chain); the new refresh token + its
   `refreshTokenExpiresAt` come back in the body. `rememberMe` semantics (long vs short lifetime) and the
   `audience` and the `DeviceId` are carried across the rotation.

### iOS implementation rule

- On a successful refresh, atomically **replace both** the access token **and** the refresh token in the
  Keychain `TokenStore` with the values from the response — never keep the old refresh token. (Android's
  `toTokens()` requires a non-null `refreshToken` + `refreshTokenExpiresAt` in the refresh body, then
  `tokenStore.save(...)` overwrites the whole bundle.)
- **Single-flight** the 401-refresh so N concurrent 401s do **one** network refresh and the rest reuse
  the freshly-stored token — otherwise two parallel refreshes present the same (now-rotated) token twice
  and you self-trigger the theft revoke. iOS uses an `actor SessionRefresher` (the parity of Android's
  `synchronized(this)` in `AuthAuthenticator`): the first caller refreshes; queued callers, on entering,
  check whether the stored token already changed and reuse it without hitting the network.
- A refresh **failure** (server rejected the token, refresh expired, theft revoke) → wipe the
  `TokenStore`, clear every session-scoped cache, and emit a **forced sign-out** event. Do **not** retry.
  (Android returns `null` from the Authenticator and emits `ForcedSignOutReason.SessionExpired`.)
- Pre-emptively **stop** if the stored refresh token is already past its `refreshTokenExpiresAt` — there's
  no point calling the endpoint; go straight to forced sign-out.

---

## 5. The empty-`Token` unconfirmed-email login special case

`POST /api/Auth/Login` (and `PUT /api/Auth/ConfirmUserEmail`) return **`200 OK`** even when the user's
email is **not yet confirmed**. The success body (`JwtTokenResponse`) is then a **special shape** the
client must branch on — it is **not** an error:

```
JwtTokenResponse {
  Token: string                  // may be EMPTY on the unconfirmed path
  IsEmailConfirmed: bool
  RefreshToken: string?          // null/absent on the unconfirmed path
  RefreshTokenExpiresAt: date?
  Email, UserId, Role, HasAdminAccess, ...
}
```

### The gate (mirror Android exactly)

On a `200` login/confirm body:

- **`IsEmailConfirmed == false` OR `Token` is empty/blank** → this is the **email-unconfirmed** outcome.
  Do **not** store a session as "authenticated"; surface a distinct state
  (`AuthSuccess.EmailUnconfirmed` / `LoginOutcome.UnverifiedEmail`) and **route to the email-verification
  screen**. (The customer app treats `!isEmailConfirmed || token.isEmpty()` as unconfirmed. The partner
  app additionally persists the token when one *is* present so the verify screen can call
  `ResendConfirmationEmail` with a Bearer, then still routes to verify if `isEmailConfirmed != true`.)
- **`IsEmailConfirmed == true` AND `Token` non-empty** → full session: persist the token bundle, then
  proceed.
- **Defensive empty-token on a path that should always carry one** (e.g. `ConfirmUserEmail` returning
  `200` with no token) → treat as a non-fatal "unverified, no token" outcome and show a generic error /
  re-prompt, rather than navigating into the app with no session. (Android's confirm flow returns
  `UnverifiedEmail(hasToken = false)` here.)

The iOS login/confirm view-models map these to the sealed state enum; the empty-token branch is one of
the documented `UiState`/outcome cases, not an `ApiError`.

---

## 6. Body token, never cookie (mobile transport)

The web hosts authenticate with an **HttpOnly cookie + CSRF token** (`X-CSRF-Token`). The **mobile** hosts
deliberately do **not** — native clients can't read HttpOnly cookies, so the mobile auth controllers
return the access + refresh tokens **in the JSON body** for the client to store in the **Keychain** (iOS)
/ `EncryptedSharedPreferences` (Android). Consequences for iOS:

- Read tokens from the **response body** (`JwtTokenResponse.Token` / `.RefreshToken`), never from
  `Set-Cookie`. Configure the `URLSession` so cookie storage is irrelevant to auth (do not rely on it).
- Send the access token as **`Authorization: Bearer <token>`** (§3 governs when to omit it). The mobile
  contract has **no CSRF token** — `CsrfToken` comes back `null` on mobile and there is no `X-CSRF-Token`
  header to echo. `Bearer` is unforgeable by CSRF, so the second factor isn't needed.
- The refresh + logout flows carry the **refresh token in the request body** (`RefreshToken.Command`,
  `Logout.Command`), not in a cookie.

---

## 7. Implementation checklist (for the auth-spine ticket)

- [ ] One `DeviceIdProvider` in `CleansiaCore`; Keychain-persisted UUID; both `HeaderAdapter` and
      device-registration read it. `X-Device-Id` (header) == `deviceId` (Device/Register body). (§2)
- [ ] `HeaderAdapter` attaches `X-Device-Id` (64, ASCII), `X-Device-Label` (120, ASCII),
      `X-Time-Zone` (read fresh per request) on **every** request, on **both** sessions. (§1)
- [ ] No-`Bearer`-on-anon path skip; host-specific allow-list (customer adds the guest-booking surface);
      `Logout` stays authed. (§3)
- [ ] Separate no-auth session for refresh; `actor SessionRefresher` single-flight; **replace** the stored
      refresh token every refresh; theft/expiry/reject → forced sign-out, no retry. (§4)
- [ ] Login/confirm `200` gate: empty-`Token`/`!IsEmailConfirmed` → email-unconfirmed state, not auth, not
      error. (§5)
- [ ] Tokens read from body, stored in Keychain; `Bearer` transport; no cookie, no CSRF. (§6)
