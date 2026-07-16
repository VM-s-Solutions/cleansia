---
id: T-0422
title: "Mobile — revoking the CURRENT device from the Devices page should sign the user out locally, immediately"
status: proposed
size: S
owner: ios
created: 2026-07-15
updated: 2026-07-15
depends_on: [T-0414]
blocks: []
stories: []
adrs: [ADR-0024, ADR-0026]
layers: [ios, android]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: owner report — deleted own device from its own Devices page, stayed logged in as a zombie
note: renumbered from T-0414 (that ID was taken by ADR-0026's server-side immediate revocation); this is the CLIENT-side complement — sign the revoking device out at 0s instead of waiting for the ≤30s directory
---

> **Owner-observed:** deleting *your own* device from the Devices page leaves you logged in with a
> dead session — screens degrade ("no data"), and the real logout arrives only when the ≤30-min
> access token expires and refresh fails (the accepted ADR-0024 bound). Server-side this is correct
> (`RevokeDevice` revokes the device's refresh tokens); the client just doesn't act on what it did.
>
> The client KNOWS the deviceId it revoked and its own deviceId. When they match, it should treat the
> successful revoke as a self-logout: run the local sign-out (clear tokens + session caches) and route
> to login — no server round-trip needed beyond the revoke itself, no waiting for TTL. Alternatively
> (product call): visually distinguish the current device ("This device") and confirm with "This will
> log you out here".

## Acceptance criteria
- [ ] **AC1** — iOS (customer + partner): after a successful revoke of a row whose `deviceId` equals
  the local `DeviceIdProvider.deviceId`, the app performs the local sign-out immediately (reuse the
  existing `signOutLocal` + forced-signout routing; do NOT call the server logout — the session is
  already dead) and lands on the login screen.
- [ ] **AC2** — Android (customer + partner): same behavior via the shared session manager.
- [ ] **AC3** — the Devices page marks the current device (e.g., "This device" badge) so the
  self-revoke is a deliberate act; confirmation copy states it signs you out here. 5-locale i18n.
- [ ] **AC4** — existing device-page and auth tests green; new unit test pins the self-revoke →
  local-sign-out path.

## Status log
- 2026-07-15 — filed `proposed` from the owner's self-revoke zombie-session report. The companion
  logout-hang fix (logout blocked minutes on serial POSTs before local clear) shipped separately.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
