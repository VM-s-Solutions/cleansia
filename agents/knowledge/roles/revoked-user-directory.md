# RevokedUserDirectory (ADR-0027, accepted 2026-07-15, amendments U1–U3; extends ADR-0026 X1)

**Responsibility (one sentence):** Answer, from memory and in O(1), whether a given `userId`'s
session established at a given instant (`iat`) predates that user's most-recent **password reset** —
with the same documented staleness bound as the device directory (`DeviceRevocation:RefreshSeconds`,
default 30 s).

**Kind:** singleton service + companion `BackgroundService` pump (`RevokedUserDirectoryRefresher`),
registered by `AddUserRevocationEnforcement()` in `Cleansia.Config` — **mobile hosts only**. A
structural sibling of `RevokedDeviceDirectory`, one key narrower (`userId`, not
`(userId, deviceId)`).

## Collaborators
- `IRefreshTokenRepository.GetPasswordResetsSinceAsync(cutoff)` — the refresher's only data source
  (background, `IgnoreQueryFilters` cross-tenant read; filters `RevokedReason == "password_reset"`
  **alone** — never `password_changed` — and projects `(UserId → MAX(RevokedAt))`). No new schema:
  the reset already persisted this timestamped signal (T-0407).
- `TimeProvider` — horizon math and snapshot-age/staleness telemetry (never `DateTime.UtcNow`).
- `IJwtSettings.AccessTokenExpMinutes` — sizes the retention horizon (TTL + 5 min slack): a reset
  older than the TTL cannot have a live token predating it.
- The two mobile hosts' `JwtBearerEvents.OnTokenValidated` (via the **shared** helper in Config,
  alongside the device probe) — its only consumer; a match becomes `context.Fail("session_revoked")`
  → 401.

## Contract
- `IsRevoked(userId, tokenIssuedAt)` → true iff a snapshot entry exists for `userId` and
  `tokenIssuedAt < entry.ResetAt` (strict `<`). The `iat` guard makes reset a *session* kill, not a
  user ban: the post-reset re-login (`iat > ResetAt`) passes even while the entry is still present, so
  no spared-session bookkeeping is needed on the reset path (reset is keep-none — there is no session
  to spare). A token matching an entry with a missing/unreadable `iat` is treated as revoked (A2
  parity). **Same-second recovery is a false-FAIL that self-heals, never a lockout (ADR-0027 U1):**
  `iat` is whole-second and `ResetAt` sub-second, so a post-reset re-login inside the reset's
  wall-clock second can 401 once — but its refresh token was minted *after* the reset (not in the
  keep-none revoke set), so refresh succeeds and the retry passes. Never a false-PASS.
- **Keys on `sub`, which every access token carries** — so there is **no claim-transition window**
  (contrast the device directory's `device_id`, which needed the ADR-0026 D6 grace). Effective from
  the first request after deploy.
- **Reset only, never change** (ADR-0027 D3): the poll predicate is `password_reset`; password
  *change* (`password_changed`) is deliberately NOT fed in — it is authenticated hygiene, not takeover
  recovery, and feeding it would self-inflict a 401 on the change caller's own spared session.
- Latest-wins on `Replace`: a user reset twice inside the horizon keeps the later instant, so a
  session minted between two resets is still killed.
- Request path: snapshot dictionary lookup only — **zero I/O, zero locks, zero clock reads**
  (perf-pinned by TC-REVOKE-USER-4).
- Refresh failure: keep serving the last snapshot (**fail-open**; ceiling = the 30-min TTL, and the
  reset user's refresh chain is already dead so they cannot renew past it); warn when snapshot age
  exceeds 3× the interval. Startup: one synchronous fill attempt, empty-on-failure, never crash/block
  the host. **The pump loop may not die** (ADR-0026 A3 parity): the whole tick sits inside the loop's
  try/catch — an escaping exception would `StopHost` and silence the staleness warning with it.
- `DeviceRevocation:Enabled = false` (the **shared** switch, ADR-0027 D7) no-ops the *consumer* (the
  JwtBearer helper) for both the device and user checks; the pump keeps polling so the snapshot stays
  warm and telemetry stays alive. **Accepted-risk coupling (ADR-0027 D9.8):** the shared switch cannot
  express "device revocation off, reset cutoff on" — the two facets have different blast radii
  (device = UX/availability; reset = active-compromise recovery). The default is shared; the
  pre-analyzed split is a distinct `UserRevocation:Enabled`, triggered by any incident that needs
  device revocation off while reset cutoff stays on.

## Does NOT know
- **HTTP, JWTs, or claims** — the JwtBearer helper parses the token and hands it plain values
  (`userId`, `iat`).
- **Devices** — device revocation is `RevokedDeviceDirectory`'s job; this directory never keys on a
  device id and never reads the `Devices` table. The two are independent siblings; the shared touch is
  one `OnTokenValidated` helper doing both O(1) probes.
- **Password change** — `password_changed` rows are out of scope by decision (ADR-0027 D3);
  accelerating change requires a superseding ADR (it must first answer the spared-session 401).
- **Web hosts** — the three web hosts install no directory; web reset cutoff rides the standing
  web-host TTL follow-up (ADR-0024 D4.3), not this ADR.
- **Refresh tokens' lifecycle** — `RefreshTokenService` owns revocation; this directory is a
  read-only projection of the already-written `password_reset` rows and never revokes anything.
- **Tenancy** — keys are globally unique user ids; the poll is deliberately cross-tenant
  (`IgnoreQueryFilters`).

**Smell guard:** if a scenario requires this role to read a device id, to distinguish *which* of a
user's sessions to spare, to read the `password_changed` reason, or to vary by tenant/country, the
responsibility is being stretched — go back to the ADR. The whole enforcement contract is the `iat`
guard against a per-user reset instant.
