# Security findings — auth / session lifecycle

## 2026-06-10 — T-0188 re-gate (security): latent multi-tenant gap in token-by-user reads

**Severity: latent (dormant in single-tenant production; opens only when users carry `tenant_id`).**

`RefreshToken` is `ITenantEntity`, but refresh tokens are issued/rotated on **anonymous** endpoints
(login, refresh), where `TenantProvider.GetCurrentTenantId()` reads claims from an unauthenticated
`HttpContext.User` and returns null — so token rows are stamped `TenantId = null`
(`CleansiaDbContext` SaveChanges stamping). The new per-device session kill,
`RefreshTokenRepository.GetActiveByUserIdAsync` (`src/Cleansia.Infra.Database/Repositories/RefreshTokenRepository.cs:18-24`),
runs on an **authenticated** request: for a user whose JWT carries `tenant_id = T`
(`AuthExtensions.SetClaims`, `src/Cleansia.Core.AppServices/Extensions/AuthExtensions.cs:23-26`), the
global tenant filter requires `e.TenantId == T` and hides the null-stamped token rows — the device
revoke would silently match zero tokens. Same class as the webhook-path finding: anonymous-write /
authenticated-read tenant asymmetry. `Logout`/`RevokeAsync` (`GetByTokenHashAsync`) shares the shape.

Not a regression of T-0188 (pre-existing class; correct in today's null-TenantId single-tenant mode,
and the proving tests use a null tenant provider on both sides). Must be fixed before any tenant is
onboarded with non-null `User.TenantId`.

**Proposed ticket:** "RefreshToken tenant stamping/read asymmetry — token-by-user and token-by-hash
reads miss null-TenantId rows for tenant-claimed users (device revoke + logout silently no-op in
multi-tenant mode)".

## 2026-06-14 — T-0236 contract-lock + fix: the symmetry rule (read-side `IgnoreQueryFilters`)

**The one rule (architect call at contract-lock):** the refresh-token **read/revoke paths clear the
EF global tenant filter** (`IgnoreQueryFilters()`) and **re-scope to the caller's own identity** — the
unguessable SHA-256 `TokenHash`, or the `UserId` taken from the caller's own JWT. Issuance stays as-is
(rows stamped `TenantId = null` on the anonymous login/refresh path; nothing to stamp there since the
tenant claim does not yet exist). Chosen over issuance-side stamping because:
- It is the **contained, durable** fix and mirrors the established codebase idiom — the
  anonymous-write/tenant-read pattern already fixed for the order webhook in **T-0245**
  (`ExistsIgnoringTenantAsync`), and the `*IgnoringTenant` reads on `UserRepository`/`OrderRepository`
  (memory note *tenant-ignoring-read-on-webhook-paths*).
- It is **correct against existing data with no backfill**: tokens already stamped `TenantId = null`
  are found regardless of the caller's tenant claim. Issuance-side stamping would instead require
  backfilling every already-issued null-stamped row to be correct.
- It does **not widen the surface (S1/S3)**: every read still pins `TokenHash` or the caller's own
  `UserId`, so a user can never read or revoke another tenant's (or another user's) tokens. The filter
  is only cleared to stop it hiding the caller's **own** null-stamped rows — not to look across users.

**AC4 — backfill:** NOT required under this rule (the read-side fix finds null-stamped rows as-is). No
data migration; no `manual_step: ef-migration` for T-0236.

**Scope of the fix:** `RefreshTokenRepository.GetByTokenHashAsync` (logout/rotate/rotation-reuse),
`GetActiveByUserIdAsync` (per-device revoke), and `RevokeChainAsync` (theft-signal chain revoke) now
read via `IgnoreQueryFilters()` with the hash/`UserId` predicate. `DeleteStaleAsync` already did.
`IRefreshTokenService` and all auth endpoints are unchanged (no DTO/route change → no nswag-regen).
T-0149 rotation re-checks are untouched and stay green. Proven by
`RefreshTokenServiceTenantRevokeTests` (real `CleansiaDbContext`, real tenant filter): revoke from a
tenant context actually flips `RevokedAt` on the null-stamped row, and never touches another user's
token. Postgres-provider parity in `RefreshTokenTenantRevokePostgresTests`.
