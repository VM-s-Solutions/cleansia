---
id: T-0414
title: "Security — device deleted from the Devices list must lose API access ~immediately: device_id claim in mobile access tokens + RevokedDeviceDirectory enforcement at token validation (ADR-0026)"
status: proposed
size: M
owner: architect
created: 2026-07-15
updated: 2026-07-15
depends_on: []
blocks: []
stories: []
adrs: [ADR-0026, ADR-0024]
layers: [backend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: owner directive 2026-07-15 ("I want the device once it's deleted from the list of devices to be revoked IMMEDIATELY. Not wait until the token is revoked.") — the named ADR-0024 D3-B revisit trigger
---

> **Owner-directed.** ADR-0024 (T-0405, landed) bounded a revoked device's residual access at
> ≤ 30 min (mobile TTL). The owner now mandates ~immediate enforcement. ADR-0026 (**ACCEPTED —
> panel verdict 2026-07-15, amendments A1–A6 folded; conditional only on the owner ratifying the
> ≤ 30 s bound, which does NOT gate this ticket — the ≤ 30 s form is the substrate of both
> possible answers**) ratifies: mobile-minted access tokens
> carry a signed `device_id` claim; the two mobile hosts check every validated token against an
> in-memory revoked-device snapshot polled from Postgres every ≤ 30 s; a token predating the
> revocation fails with **401**, and the client's existing 401→refresh→refresh-rejected machinery
> converts that into token wipe + forced sign-out with **zero client code change**. The 30-min TTL
> stays untouched as the fail-open backstop. Full rationale, alternatives (per-request DB check,
> header-keyed, jti denylist, push-driven sign-out, LISTEN/NOTIFY), failure posture, and residues:
> ADR-0026.

## Ratified implementation instruction (backend lane — from ADR-0026 D1–D8)

**1. Claim (shared Core, one extension + two call sites):**
- `AuthExtensions.SetClaims(this User user, string? employeeId = null, string? deviceId = null)` —
  yield `new Claim("device_id", deviceId)` when non-empty (mirrors `employee_id`/`tenant_id`).
- `TokenService.GenerateAccessToken` (login): pass `requestMetadata.DeviceId`.
- `RefreshToken.Handler.GenerateAccessToken` (refresh): pass **`issued.Record.DeviceId`** — the
  persisted, rotation-carried value. **Never the request header on the refresh path.**
- Do NOT touch expiry minting (`DateTime.UtcNow.AddMinutes(...)` lines stay — TC-REVOKE-TTL pins).

**2. Deactivation stamp (write path):**
- `DeviceRepository` overrides `Deactivate` to call `entity.Deactivated(actor, now)` — actor from
  `IUserSessionProvider` with `"System"` fallback (copy the `SavedAddressRepository.cs:44-49`
  precedent). Covers both `RevokeDevice` and `UnregisterDevice` (logout — deliberate, ADR-0026 D7).
- **No migration**: `DeactivatedBy/On` exist on `Auditable` and are mapped
  (`EntityConfiguration.cs:49`).

**3. Repository read (poll source):**
- `IDeviceRepository.GetDeactivatedSinceAsync(DateTimeOffset cutoff, CancellationToken)` →
  projection of `(UserId, DeviceId, DeactivatedOn)` where **`DeactivatedOn >= cutoff` ALONE — no
  `IsActive` conjunct** (panel amendment **A1**, load-bearing: `Device.MarkRegistered` reactivates
  a tombstone for any authenticated caller and never clears `DeactivatedOn`; filtering on
  `IsActive` would let a revoked device expunge its own entry by re-registering. The snapshot must
  be reactivation-insensitive — the `iat` guard alone decides survival). **With
  `.IgnoreQueryFilters()`** + a comment citing the sanctioned background/cross-tenant read
  pattern (T-0245 lineage). No index in this ticket (table is tiny; ADR-0026 D9.6).

**4. Directory + refresher (new, `Cleansia.Config` registration; see the CRC card
`agents/knowledge/roles/revoked-device-directory.md`):**
- `IRevokedDeviceDirectory.IsRevoked(userId, deviceId, tokenIssuedAt)` — immutable snapshot
  dictionary, atomic reference swap, reject iff entry exists and `tokenIssuedAt < RevokedAt`.
- `RevokedDeviceDirectoryRefresher : BackgroundService` — poll every
  `DeviceRevocation:RefreshSeconds` (default **30**); horizon = `AccessTokenExpMinutes` + 5 min;
  scoped `IDeviceRepository` per tick; **`TimeProvider`** for all time math; synchronous initial
  fill attempt at startup, empty-on-failure; on poll failure keep last snapshot and WARN when
  snapshot age > 3× interval (**fail-open** — ADR-0026 D4; never fail-closed).
- **The loop must be un-killable** (panel amendment **A3**): the entire tick body sits inside the
  `while (!stoppingToken.IsCancellationRequested)` loop's try/catch — **no exception may escape
  `ExecuteAsync`** (.NET's default `BackgroundServiceExceptionBehavior.StopHost` would turn a poll
  bug into a host crash, and a dead loop can never emit the staleness warning). Structure:
  `while (...) { try { tick(); } catch (Exception ex) { log; } await Task.Delay(interval, token); }`.
- **`Enabled = false` semantics** (panel amendment **A5**): the enforcement helper no-ops, but the
  **refresher keeps polling** — the snapshot stays warm (re-enable is instant) and staleness
  telemetry never goes dark.
- `AddDeviceRevocationEnforcement(configuration)` extension in `Cleansia.Config.Services` —
  registers directory + refresher + binds `DeviceRevocation` options (`Enabled` default true,
  `RefreshSeconds` default 30).

**5. Host wiring (the ONLY per-host touch — two mobile hosts, two small edits each):**
- `Web.Mobile.Partner` + `Web.Mobile.Customer` `ServiceExtensions.AddServices`: add
  `.AddDeviceRevocationEnforcement(configuration)`.
- In each host's existing `OnTokenValidated` (Partner `ServiceExtensions.cs:168-186`; Customer
  `:171` area), after the role-claim mapping, call the shared helper (in `Cleansia.Config`): read
  `sub` / `device_id` / `iat` from the principal; no `device_id` claim → pass; directory match →
  `context.Fail("device_revoked")` (→ **401**, never 403). **Edge rule (panel amendment A2):** a
  token that HAS a `device_id` claim and matches a directory entry but whose `iat` is
  missing/unreadable → `context.Fail` (it cannot prove it postdates the revocation; both mint
  sites always stamp `iat`, and a legitimate anomaly self-heals via refresh). When
  `DeviceRevocation:Enabled` is false the helper is a no-op (the refresher still polls — A5).
- Add `"DeviceRevocation": { "Enabled": true, "RefreshSeconds": 30 }` to the four mobile
  appsettings files (`appsettings.json` + `appsettings.Production.json`, both hosts).
- **Web hosts (`Web.Partner`/`Web.Admin`/`Web.Customer`): byte-untouched.** No Bicep change. No
  NSwag (no DTO/endpoint change). No client change.

## Test contract (TC definitions in ADR-0026 §"How a reviewer verifies compliance")
- **TC-REVOKE-NOW-1** — revoked device's EXISTING unexpired access token → **401** (not 403) after
  a directory refresh; another user's device unaffected. (HostTests, mobile host, short
  `RefreshSeconds` or a test refresh hook.)
- **TC-REVOKE-NOW-2** — same user, devices A+B; revoke A → A 401, **B stays 200** (device-keyed,
  never user-keyed).
- **TC-REVOKE-NOW-3** — re-login on the revoked device while the entry is still in the snapshot →
  new token 200 (the `iat` guard: revoke kills sessions, not the device).
- **TC-REVOKE-NOW-4** — claim minting: (i) login with `X-Device-Id` → claim = header; (ii) login
  without → no claim; (iii) refresh with a different/absent header → claim = **persisted record's**
  DeviceId.
- **TC-REVOKE-NOW-5** — claim-less token + directory entry for that user → 200 (transition
  fail-open is a pinned decision, bounded by the TTL).
- **TC-REVOKE-NOW-6** — refresher repo-fake throws → last snapshot keeps serving + staleness WARN
  past 3× interval; counting fake proves **zero repository calls on the request-path check** (the
  perf pin). **Extended (A3):** the fake throws on *consecutive* ticks → the refresher still
  attempts the next tick (the loop survives repeated failure; nothing escapes `ExecuteAsync`).
- **TC-REVOKE-NOW-7** — raw-file config pin (TC-REVOKE-TTL-4 mechanism, NOT a bound-config
  HostTests assertion): four mobile appsettings carry `Enabled: true` and `RefreshSeconds <= 30`.
- **TC-REVOKE-NOW-8** — `UnregisterDevice` (logout) → that device's outstanding access token 401s
  after the next refresh (the D7 bonus gap-closure).
- **TC-REVOKE-NOW-9 (A1 — the resurrection pin)** — login `X-Device-Id: A` → revoke A → *before
  any directory refresh*, `Device/Register` for A with the still-valid token (succeeds; the row
  reactivates via `MarkRegistered`) → force directory refresh → **the old access token still
  401s** (snapshot keys on `DeactivatedOn`, not row state); a fresh login on A afterwards → 200.
- **Regression:** all TC-REVOKE-TTL-1..5 stay green **unedited**; `AccessTokenExpMinutes` stays 30.

## Acceptance criteria
- [x] **AC1** — ADR-0026 is `accepted` (panel: challenge/defense/verdict complete, 2026-07-15;
  conditional only on the owner's ≤ 30 s ratification, which does not gate this ticket). UNBLOCKED.
- [ ] **AC2** — deleting a device from the Devices list ends that device's API access within
  `RefreshSeconds` (default 30 s): its existing access token 401s, its refresh is rejected
  (`InvalidRefreshToken`), and the client force-signs-out — pinned by TC-REVOKE-NOW-1/2/8, and
  **it survives hostile re-registration** — pinned by TC-REVOKE-NOW-9 (A1).
- [ ] **AC3** — no false kills: sibling devices, re-logins after revoke, and claim-less/web-shaped
  tokens are unaffected — pinned by TC-REVOKE-NOW-2/3/5.
- [ ] **AC4** — request-path enforcement performs zero I/O; poll failure degrades open with
  telemetry, never fail-closed — pinned by TC-REVOKE-NOW-6.
- [ ] **AC5** — `dotnet test` green including all TC-REVOKE-TTL-* unedited; web hosts and
  `deploy/bicep/**` byte-untouched; no client contract change.

## Notes
- 401-vs-403 is load-bearing: OkHttp's authenticator and the iOS bridge react to 401 only —
  enforcement must fail *authentication* (`context.Fail` in `OnTokenValidated`), not authorization.
- The false-positive self-heal is free: a stale-snapshot 401 against a legitimate session triggers
  refresh → refresh **succeeds** → retry carries a fresher `iat` → passes. Forced sign-out only
  ever follows refresh *rejection*.
- Escalation seams (do NOT build): literal-zero = read-through `IRevokedDeviceDirectory` impl;
  ~0 s poll = LISTEN/NOTIFY second trigger; user-level kill = superseding ADR. Named in ADR-0026.
- Sibling lanes are editing backend/Android/iOS concurrently — coordinate merge order on
  `AuthExtensions.cs` / the two host `ServiceExtensions.cs` via the PM.
- **Explicitly NOT this ticket's scope** (panel verdict cross-ticket instructions — PM files
  separately): **X1** user-keyed directory entries on password reset (needs its own ADR);
  **X2** revoke↔rotation TOCTOU hardening (`xmin` concurrency on `RefreshToken` — ADR-0026 D9.7,
  `security_touching`); **X3** WARN-log headerless mobile-host logins (observability, evidence-
  gates a future required-header validator).

## Status log
- 2026-07-15 — filed `proposed` by the architect (author mode) alongside ADR-0026; awaiting the
  challenger/lead panel on the ADR before the backend lane picks this up.
- 2026-07-15 — **panel verdict: ADR-0026 ACCEPTED** (amendments A1–A6 folded into the instruction
  above: A1 reactivation-insensitive poll predicate + TC-REVOKE-NOW-9; A2 missing-`iat` edge rule;
  A3 un-killable refresher loop + TC-6 extension; A5 kill-switch semantics; A4/A6 are ADR-text
  residue/evidence updates with no implementation delta). AC1 satisfied — **ready for the backend
  lane**. One owner question is in flight (does "immediately" tolerate ≤ 30 s, or literal-zero →
  B-literal swap behind `IRevokedDeviceDirectory`); it does not gate this build.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
