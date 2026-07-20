---
id: T-0405
title: "Security — revoking a device leaves it fully functional for up to 24 h: AccessTokenExpMinutes=1440 + no per-request device/session check"
status: done
size: S
owner: architect
created: 2026-07-15
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0024]
layers: [architect, backend]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: owner report (revoked iPhone from the sim; iPhone stayed logged in) + revoke-device-session-audit (wf_88de1ca0)
---

> **Owner-observed:** revoke device A from device B → A stays logged in. The audit shows revocation IS
> correctly wired — `RevokeDevice.Handler` deactivates the row AND calls
> `RevokeByDeviceAsync(userId, deviceId, "device_revoked")` (`RevokeDevice.cs:43-44`), which revokes
> exactly the target device's refresh tokens via the persisted `RefreshToken.DeviceId` column (stamped
> from `X-Device-Id` at login, carried across rotation; SQLite+Postgres tests pin the semantics). The
> problem is the **effect latency**: the outstanding access JWT stays valid until natural expiry, and
> **`AccessTokenExpMinutes` is 1440 (24 h)** on every host (mobile Customer/Partner appsettings +
> Production; the code default of 15 in `JwtSettingsConfig.cs:11` is overridden everywhere). There is
> **no per-request check** of `Device.IsActive` / token state (mobile inherits the no-op host
> middleware; the JWT carries no jti for a denylist). Net: a revoked/stolen device keeps full API
> access for up to a day.

## Decision to make (architect)
**DECIDED — ADR-0024 accepted 2026-07-15 (panel: challenge/defense/verdict trail in the ADR).**
Option **A** at **30 min**, mobile hosts only, config-only. B and C recorded as deferred escalations
with named revisit triggers (ADR-0024 D3). AC1 is satisfied by the ADR.

- **A (CHOSEN, minimal):** drop `AccessTokenExpMinutes` on the mobile hosts to **30** min.
  Refresh is already solid (rotation + theft-cascade + forced-logout on refresh failure verified on
  iOS and Android customer). Revocation latency becomes ≤ TTL. Config-only; no code.
- **B (deferred):** per-request device/session validation. Trigger: a real incident where ≤ 30 min
  residual access caused harm, or a compliance/product demand for instant kill.
- **C (deferred):** push-driven force-logout on `device_revoked`. Trigger: T-0404 (iOS receive-side)
  closing. Not a security control — UX layer over A only.

## Ratified implementation instruction (backend lane — from the ADR-0024 verdict)
**Config change (exactly four files, value `30`, key `JwtSettings:AccessTokenExpMinutes`):**
1. `src/Cleansia.Web.Mobile.Partner/appsettings.json` (line 23)
2. `src/Cleansia.Web.Mobile.Partner/appsettings.Production.json` (line 20)
3. `src/Cleansia.Web.Mobile.Customer/appsettings.json` (line 23)
4. `src/Cleansia.Web.Mobile.Customer/appsettings.Production.json` (line 20)

**No code change** in `TokenService.cs` / `RefreshToken.cs` / `RefreshTokenService.cs` / any
`ServiceExtensions.cs` (ClockSkew stays `Zero`) / any client / `deploy/bicep/**`. Web hosts'
appsettings stay byte-identical.

**Test contract (AC2/AC3 — full definitions in ADR-0024 §"How a reviewer verifies compliance"):**
- **TC-REVOKE-TTL-1** — integration: login `X-Device-Id: A` → `RevokeDevice(A)` → A's refresh →
  `BusinessErrorMessage.InvalidRefreshToken`; device B's refresh still rotates (extends
  `RefreshTokenFlowTests`, which lacks the `device_revoked` reason today).
- **TC-REVOKE-TTL-2** — boot a mobile host with `AccessTokenExpMinutes` overridden to a fractional
  value (~`0.05` ≈ 3 s; the setting is a `double`), login, authed call OK, wait past expiry, same
  call → 401. Do NOT try a fake-clock/boundary test: minting uses raw `DateTime.UtcNow`
  (`TokenService.cs:74`) and this ticket must not touch that file.
- **TC-REVOKE-TTL-3** — re-run/extend the existing client forced-signout pins as AC2 evidence.
- **TC-REVOKE-TTL-4** — plain xUnit raw-file pin (NOT a HostTests bound-config assertion — the
  HostTests overlay pins 15 and would mask the value): parse the four files above as JSON, assert
  `30`; also assert the three web hosts' six appsettings files still carry `1440`.
- **TC-REVOKE-TTL-5** — `RevokeByDeviceAsync_Leaves_A_Null_DeviceId_Token_Alive` stays green,
  unedited.

## Acceptance criteria
- [ ] **AC1** — ADR records the chosen latency posture (TTL value and/or per-request check) and why.
- [ ] **AC2** — revoking device A from device B ends A's API access within the documented bound;
  integration test pins: revoked refresh token → `InvalidRefreshToken`; expired access token + revoked
  refresh → forced logout path.
- [ ] **AC3** — `dotnet test` green; no client contract change (or clients regenerated if any).

## Notes
- Legacy refresh tokens with `DeviceId = null` can never be device-revoked (deliberate null-guard,
  `RefreshTokenService.cs:129`); they die only by expiry/logout. Acceptable residue — record in the ADR.
- Logout path division of labor is intentional: `Logout` revokes the presented token;
  `UnregisterDevice` handles the device row only.

## Status log
- 2026-07-15 — filed `proposed` from the owner's report + the session-revocation audit.
- 2026-07-15 — **ADR-0024 accepted** by the architect panel (challenger + lead; amendments A1–A5:
  429-vs-transport honesty, burst-window NAT math, admin-host separability in the web follow-up,
  D4.5/D4.6 residues, TC-2/TC-4 re-specified to feasible mechanisms). AC1 done. Backend lane
  unblocked — see "Ratified implementation instruction" above. Living doc
  `agents/architecture/decisions/auth-sessions.md` updated to accepted state. PM to file the four
  follow-up tickets listed in the ADR Verdict (client refresh classification; web/admin TTL;
  password-change session revocation; TimeProvider minting) + the `security-rules.md` catalog line.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped `c198a275` (ADR-0024 accepted): 30-min TTL on the two mobile hosts.
