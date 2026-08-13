# RevokedDeviceDirectory (ADR-0026, accepted — panel 2026-07-15, amendments A1–A6)

**Responsibility (one sentence):** Answer, from memory and in O(1), whether a given
`(userId, deviceId)` session established at a given instant (`iat`) predates a device revocation —
with a documented staleness bound of `DeviceRevocation:RefreshSeconds` (default 30 s).

**Kind:** singleton service + companion `BackgroundService` pump (`RevokedDeviceDirectoryRefresher`),
registered by `AddDeviceRevocationEnforcement()` in `Cleansia.Config` — **mobile hosts only**.

## Collaborators
- `IDeviceRepository.GetDeactivatedSinceAsync(cutoff)` — the refresher's only data source
  (background, `IgnoreQueryFilters` cross-tenant read; projects `(UserId, DeviceId, DeactivatedOn)`).
- `TimeProvider` — horizon math and snapshot-age/staleness telemetry (never `DateTime.UtcNow`).
- `IJwtSettings.AccessTokenExpMinutes` — sizes the retention horizon (TTL + 5 min slack): a
  revocation older than the TTL cannot have a live token predating it.
- The two mobile hosts' `JwtBearerEvents.OnTokenValidated` (via the shared helper in Config) — its
  only consumer; a match becomes `context.Fail("device_revoked")` → 401.

## Contract
- `IsRevoked(userId, deviceId, tokenIssuedAt)` → true iff a snapshot entry exists and
  `tokenIssuedAt < entry.RevokedAt`. The `iat` guard makes revoke a *session* kill, not a device
  ban: a re-login after revoke passes even while the entry is still present. A device-claimed
  token matching an entry with a missing/unreadable `iat` is treated as revoked (A2).
- **The snapshot is reactivation-insensitive** (A1): the poll keys on `DeactivatedOn >= cutoff`
  alone — never on the row's current `IsActive`. A revoked device re-registering
  (`MarkRegistered`) must not expunge its own entry; only the `iat` guard clears a session
  (pinned by TC-REVOKE-NOW-9).
- Request path: snapshot dictionary lookup only — **zero I/O, zero locks, zero clock reads**
  (perf-pinned by TC-REVOKE-NOW-6).
- Refresh failure: keep serving the last snapshot (**fail-open**; ceiling = the 30-min TTL,
  ADR-0026 D4); warn when snapshot age exceeds 3× the interval. Startup: one synchronous fill
  attempt, empty-on-failure, never crash/block the host. **The pump loop may not die** (A3): the
  whole tick sits inside the loop's try/catch — an escaping exception would stop the host
  (`BackgroundServiceExceptionBehavior.StopHost`) and silence the staleness warning with it.
- `DeviceRevocation:Enabled = false` no-ops the *consumer* (the JwtBearer helper); the pump keeps
  polling so the snapshot stays warm and telemetry stays alive (A5).

## Does NOT know
- **HTTP, JWTs, or claims** — the JwtBearer helper parses the token and hands it plain values.
- **Why a device was deactivated** (revoke vs logout — both mean "sessions before T are dead").
- **A device row's current `IsActive`** (A1, deliberate) — reactivation is invisible to the
  directory; if a scenario ever wants the entry removed on re-registration, the answer is the
  `iat` guard, not a predicate change.
- **Refresh tokens** — `RefreshTokenService` owns that lifecycle; the directory never revokes
  anything (read-only projection of the Devices table).
- **Tenancy** — keys are globally unique ids; the poll is deliberately cross-tenant.
- **Users** — user-level disables (`User.IsActive`) are out of scope by decision (ADR-0026 D9.4);
  feeding them in requires a superseding ADR, not a quiet extension.

**Smell guard:** if a scenario requires this role to read headers, revoke tokens, or vary by
tenant/country, the responsibility is being stretched — go back to the ADR.
