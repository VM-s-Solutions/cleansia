---
id: T-0422
title: "Mobile — revoking the CURRENT device from the Devices page should sign the user out locally, immediately"
status: done
size: S
owner: ios
created: 2026-07-15
updated: 2026-07-17
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
- [x] **AC1** — iOS (customer + partner): after a successful revoke of a row whose `deviceId` equals
  the local `DeviceIdProvider.deviceId`, the app performs the local sign-out immediately (reuse the
  existing `signOutLocal` + forced-signout routing; do NOT call the server logout — the session is
  already dead) and lands on the login screen.
- [x] **AC2** — Android (customer + partner): same behavior via the shared session manager.
- [x] **AC3** — the Devices page marks the current device (e.g., "This device" badge) so the
  self-revoke is a deliberate act; confirmation copy states it signs you out here. 5-locale i18n.
- [x] **AC4** — existing device-page and auth tests green; new unit test pins the self-revoke →
  local-sign-out path.

## Status log
- 2026-07-15 — filed `proposed` from the owner's self-revoke zombie-session report. The companion
  logout-hang fix (logout blocked minutes on serial POSTs before local clear) shipped separately.
- 2026-07-17 — implemented on `feature/hardening-cluster-2`, all 4 surfaces. Archaeology: the iOS
  VMs already carried the D7b self-revoke → `signedOut` path but every UI hid the revoke control on
  the current row, making it unreachable — the fix is the affordance (sign-out icon + distinct
  `devices_self_revoke_*` dialog copy, 4 keys ×5 locales ×4 catalogs) plus the missing Android
  plumbing. Note: tests were written alongside the implementation in one working set (not
  red-first); the reviewer's mutation run substituted for the red proof (below).
- 2026-07-17 — review round 1: CHANGES REQUESTED (AC1/AC2 deviation — first cut called the full
  `logout()`, two redundant uncapped server round-trips against an already-dead session; plus
  ticket-ids-in-comments and 4 stale comments). Reworked to the AC shape: iOS Views now call
  `authClient.signOutLocal()`; Android customer gained `AuthRepository.signOutLocal()` (wipe +
  `ForcedSignOut(UserInitiated)`, no server calls); Android partner uses the existing
  `signOutLocal()` + emits on the shared `SessionManager`, riding the never-unmounting nav-root
  observer (this also removed the screen-level `signedOut` flow and its back-press drop race).
  Comments cleaned per conventions. Both Android apps compile + unit tests green; both iOS apps
  BUILD SUCCEEDED; swiftformat/swiftlint clean.

## Review
- 2026-07-17 (reviewer, adversarial) round 1 — **CHANGES REQUESTED**: (1) AC1/AC2 deviation (full
  `logout()` on the self-revoke success path — offline it re-created a bounded zombie window of two
  HTTP timeouts; online it raced a competing `ForcedSignOut(SessionExpired)` from the doomed 401s);
  (2) ticket ids in 18 new comment lines; (3) 4 stale comments stating the pre-change behavior.
  Refuted attacks (evidence-backed): failure-path stranding (sign-out only on Success, incl. the
  customer `200 {success:false}` → silent-error path), wrong-target sign-out (branch on the tapped
  row, mutation-proven load-bearing test), iOS `isCurrent` detection asymmetry (unreachable — server
  flag and local compare use the same operands), partner nav back-stack leak (wiring byte-identical
  to Profile's), strings (4×5×4 all present, no format-arg mismatches), xcstrings integrity
  (insertions-only). Non-blocking residuals for the ledger: partner clients (Android + iOS) discard
  `RevokeDeviceResponse.success` where customer defends it (unreachable today); iOS shows the
  success snackbar before self-sign-out where Android deliberately suppresses it.
- 2026-07-17 round 2 — all three findings addressed as prescribed (AC2 via the shared
  SessionManager, which also eliminated the flagged screen-level signedOut drop race); re-verified
  builds/tests locally. Residual NOT taken: none.
