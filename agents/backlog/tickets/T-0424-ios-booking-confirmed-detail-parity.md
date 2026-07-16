---
id: T-0424
title: "iOS — Booking-Confirmed screen shows only the confirmation code; add the order summary + progress (Android parity)"
status: proposed
size: M
owner: ios
created: 2026-07-16
updated: 2026-07-16
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: owner remark ("booking confirmed doesn't show status/total like Android") + remarks-sweep (wf_064232d3)
---

> **Owner-observed:** the iOS booking-confirmed screen shows ONLY the confirmation code, while Android shows
> the arrival window, address, total, and a 4-step progress tracker. **iOS-only; no backend/contract change.**

## Current state
`BookingSuccessView` renders just the confirmation-code pill (from the value already in hand). It gets an
`orderId` at confirmation but never fetches the order to show details. Android's equivalent loads the order
and renders the full summary + progress.

## Approach (mirror Android; all helpers already exist on iOS)
1. **`BookingSuccessViewModel`** (new): inject the order client/repository, take `orderId`, expose
   `Loading / Loaded(order) / Error`; call `getById(orderId)` once in `init`; degrade silently to `.Error`
   (the code pill still renders). Optionally fire `OrderRepository.refresh()` to warm the Orders tab.
2. **Extend `BookingSuccessView`** to accept `orderId` + the VM. Keep the code pill; add an
   `OrderSummaryCard` (arrival via `OrdersFormat` date/estimated-time helpers, address street+city, total via
   the existing price formatter — each row drops out if blank) and a 4-step progress timeline reusing
   `OrderStatusPresentation` + `LiveProgressLogic` over `orderStatus` + `assignedEmployees`.
3. **Wire in `BookingSheetView.swift`** (the `if let success` branch, ~line 39): pass `success.orderId` and
   construct the VM.
4. **L10n:** add the booking-success detail keys to `L10n+Booking.swift` (or a new `L10n+BookingSuccess`) +
   all 5 locale `.xcstrings`.

## Acceptance criteria
- [ ] **AC1** — the confirmed screen shows: confirmation code (unchanged), arrival window, address, total,
  and the 4-step progress tracker — parity with Android; each detail row hides gracefully if its data is
  blank.
- [ ] **AC2** — the screen renders the code immediately and fills in details on load; a fetch failure still
  shows the code (no blank/broken screen).
- [ ] **AC3** — 5-locale strings; no hardcoded user text; `CleansiaCore`/customer build+test green;
  swiftformat/swiftlint clean.

## Status log
- 2026-07-16 — filed from the remarks-sweep parity finding.
