---
id: T-0405
title: "Security — revoking a device leaves it fully functional for up to 24 h: AccessTokenExpMinutes=1440 + no per-request device/session check"
status: proposed
size: S
owner: architect
created: 2026-07-15
updated: 2026-07-15
depends_on: []
blocks: []
stories: []
adrs: []
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
- **A (recommended, minimal):** drop `AccessTokenExpMinutes` on the mobile hosts to 15–60 min.
  Refresh is already solid (rotation + theft-cascade + forced-logout on refresh failure verified on
  iOS and Android customer). Revocation latency becomes ≤ TTL. Config-only; no code.
- **B (immediate revocation):** per-request device/session validation (cached `Device.IsActive`
  lookup or jti denylist). Real cost per request; decide if the product needs instant kill.
- **C (UX upgrade, later):** push-driven force-logout on `device_revoked` — the FCM queue pipeline
  exists; needs a session/device event in `NotificationEventCatalog` + client handling (relates to
  T-0404 receive-side work on iOS).

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

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
