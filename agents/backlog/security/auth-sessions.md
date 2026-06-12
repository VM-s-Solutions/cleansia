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
