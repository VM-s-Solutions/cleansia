---
id: T-0423
title: "iOS — tapping an order notification from a COLD launch is dropped (lands on Home instead of the order)"
status: proposed
size: S
owner: ios
created: 2026-07-16
updated: 2026-07-16
depends_on: []
blocks: []
stories: []
adrs: [ADR-0025]
layers: [ios]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: owner remark ("notifications not redirecting to the order") + remarks-sweep (wf_064232d3)
---

> **Owner-observed:** order-status notifications don't open the order. **Root-caused: a cold-launch race**,
> not a payload/resolver bug — the rest of the chain is correct.

## Evidence (verified end-to-end)
- **Payload is fine:** `FcmMessageFactory.Build` injects `event_key`, and every order-event dispatcher
  passes `["orderId"]=order.Id`; FCM delivers top-level data next to `aps`, so `userInfo` carries both.
- **Resolver is fine:** `CustomerNotificationDeepLink.resolve` maps every `order.*` / `recurring.scheduled`
  key to `.order(orderId:)` (unit-tested, incl. the alert-carrying `userInfo` shape).
- **Navigation is fine:** `onTap → PushNavigationModel.pendingDestination → CustomerShellView.applyPushTap →
  OrderDetailView`.
- **The break:** `CustomerAppDelegate.onTap` is assigned only inside the SwiftUI `.task` in
  `CleansiaCustomerApp.swift` (runs async after the view appears). On a **cold launch** iOS calls
  `didReceive` *before* that `.task` wires `onTap`, so `onTap` is nil; `didReceive` snapshots
  `let onTap = onTap` (nil) and dispatches `Task { onTap?(destination) }` — a silent no-op. Nothing buffers
  the launch tap, so `pendingDestination` is never set. **`PartnerAppDelegate` (~line 108) has the identical
  race.** Works only when the app was already running this process session.

## Fix (both apps)
Buffer the resolved destination in the app delegate when `onTap` is nil and flush it the instant `onTap`
is assigned:
- Make `onTap` a stored property with a flushing `didSet` (`guard let onTap, let pending =
  pendingTapDestination … Task { @MainActor in onTap(pending) }`), plus `private var pendingTapDestination`.
- In `didReceive`: if `onTap` is set, fire it; else store `pendingTapDestination = destination`.
- Both the `UNUserNotificationCenterDelegate` callback and the `.task` assignment are main-thread, so the
  buffer needs no locking. Apply symmetrically to `CustomerAppDelegate` and `PartnerAppDelegate`.

## Acceptance criteria
- [ ] **AC1** — from a fully terminated app, tapping an order notification opens that order's detail (both
  apps), for the alert (visible) push and for `didReceive` at launch.
- [ ] **AC2** — warm-launch behavior unchanged (still routes).
- [ ] **AC3** — a unit test pins the buffer→flush: a destination delivered before `onTap` is set is fired
  exactly once when `onTap` is assigned; a destination delivered after fires immediately; no double-fire.
- [ ] **AC4** — both apps build on the iOS-16 floor; swiftformat/swiftlint clean.

## Status log
- 2026-07-16 — filed from the remarks-sweep cold-launch root cause.
