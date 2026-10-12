---
id: T-0834
title: Stop a shared phone's previous account receiving pushes when its sign-out never reached the server
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0822]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
---

## Context

Found on 2026-10-12. This is S11-adjacent and server-side. It existed before T-0822.

T-0822's `PushTokenRegistrar` fix corrects only the phone's own record of which token it has registered. The server still keeps the previous account's device row active on a shared phone:
- **A sign-out whose `Device/Unregister` never lands.** `AuthApiClient.logout` caps the pre-logout hook at 5 s (`Auth.swift:214-218`), and an offline phone never sends it.
- **A late `Device/Register`**, which reactivates the tombstone (`RegisterDevice.cs:48-57`).

The revoked-device check runs only at token validation, against a snapshot refreshed every 30 s (`DeviceRevocationTokenValidation.cs:21-59`; `DeviceRevocationOptions.RefreshSeconds = 30`).

Either way, account A's row stays `IsActive`:
- `SendPushNotificationHandler.cs:120-134` sends to every active row of the recipient.
- Nothing dedupes rows across users by `DeviceId` or token.
- The APNs payload names no user, and the iOS notification-service extension filters nothing.

So account B, signed in on the same phone, receives A's pushes until the 90-day stale-device sweep (`RetentionDefaults.cs:21`). Those pushes carry the event plus A's order number, count and amount. Tapping one opens an order B cannot read.

## Doing (proposed, for Mike)

- `RegisterDevice` deactivates every other user's active row with the same `DeviceId`. That is the security review's suggestion. A test registers B on A's device and proves A's row goes inactive.

## NOT doing

- No change to the phone-side registrar.
- No change to the 90-day sweep.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Once B registers a device that A's active row also holds, a push to A no longer reaches it. The test goes RED, then GREEN.
- [ ] **AC2** — Security review passes (S11).
- [ ] **AC3** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the security re-review of the native PR (T-0822). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
