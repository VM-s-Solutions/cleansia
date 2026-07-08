---
id: T-0393
title: "Mobile FEATURE gap (iOS+Android) — the Home notification bell is inert on both platforms; no notifications inbox/feed exists (owner: \"clicking notifications does nothing\")"
status: proposed
size: L
owner: pm
created: 2026-07-08
updated: 2026-07-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [analyst, backend, ios, android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-4 owner remark #7 — reported on iOS, confirmed as a shared unbuilt feature (Android bell is also a no-op)
---

> **Owner remark #7 (phase/ios-fix2, 4th device pass): "When I click on notifications on the home page then
> nothing happens."** Verified: this is **not an iOS regression** — the bell is faithful Android parity. Android
> wires `onNotificationClick = {}` (`HomeTab.kt:228`) — an explicit no-op — and neither platform has a
> notifications inbox/feed screen. The iOS `HomeTab.swift` renders the same inert bell and documents it
> (`HomeTab.swift:162-163`). Building an iOS-only handler would diverge the two platforms; the real work is a
> cross-platform **notifications inbox** feature.

## Context
- iOS `CleansiaCustomer/.../Features/Home/HomeTab.swift:161-204` — `AddressTopBar` renders a `bell` SF symbol
  with no action; comment: "The bell is rendered but inert — Android wires `onNotificationClick = {}`".
- Android `customer-app/.../features/home/HomeTab.kt:228,316,353` — `IconButton(onClick = onNotificationClick)`
  where `onNotificationClick = {}`.
- The only notifications surface that exists on either platform is **notification preferences** (iOS
  `Profile/NotificationsView.swift`, Android settings) — the push/email opt-in toggles, NOT a message feed.
- There is no backend notifications-feed endpoint (no `GET /api/Notifications` list contract) today.

## Acceptance criteria
- [ ] **AC1 (decision first)** — analyst/architect decide the MVP: does the bell open (a) a real notifications
  inbox (list of order/booking/system events with read/unread), or (b) an interim empty-state sheet ("No
  notifications yet") on BOTH platforms? Record the choice; do NOT diverge iOS from Android.
- [ ] **AC2 (backend, if inbox)** — if (a): a mobile-audience `GET` notifications list endpoint (paged,
  per-user, tenant-scoped, S1–S10 reviewed) + a mark-read command; DTOs + mobile spec re-dump + BOTH mobile
  client regens (MANUAL_STEP). If (b): no backend.
- [ ] **AC3 (mobile UI, parity)** — the Home bell navigates to the chosen surface on iOS and Android
  identically (same route, same empty-state copy ×5 locales); an unread badge only if AC2 delivers unread
  state.
- [ ] **AC4 (non-regression)** — both apps compile; existing home tests green; the bell remains reachable/
  accessible with a proper accessibility label.

## Out of scope
- Push-notification delivery/registration (separate; APNs is blocked on the paid Apple account T-0342).
- Notification **preferences** (already shipped on both platforms) — this is the message **feed**, not the
  toggles.

## Implementation notes
- Cheapest first increment = option (b): an interim "No notifications yet" empty-state sheet on both platforms,
  so the bell stops being a dead tap, with zero backend. Promote to (a) when the feed contract lands.
- iOS: the `AddressTopBar` bell already has the tap target; only an `onNotificationClick` closure + a route need
  wiring once the destination exists.

## Status log
- 2026-07-08 — filed `proposed` by pm from phase/ios-fix2 fix-round-4 owner remark #7. Confirmed the iOS bell is
  faithful Android parity (both inert; no feed feature exists), so it was correctly NOT hacked iOS-only in the
  fix round. Medium priority: a visible dead-tap on the most-seen screen, but a genuine feature (not a fix).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
