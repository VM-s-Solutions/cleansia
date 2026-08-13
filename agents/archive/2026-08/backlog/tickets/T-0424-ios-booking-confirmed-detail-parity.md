---
id: T-0424
title: "iOS — Booking-Confirmed screen shows only the confirmation code; add the order summary + progress (Android parity)"
status: done
size: M
owner: ios
created: 2026-07-16
updated: 2026-07-19
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
- 2026-07-19 — **done** on `feature/payroll-invoice-paid-notify`. Premise was PARTIALLY stale: a minimal
  pass (best-effort VM + icon summary rows + a 5-dot `LiveProgress` row) had landed in `8c8f96e8`; this
  pass closed the remaining Android-parity gap. `BookingSuccessViewModel` refit to the sealed
  `BookingSuccessUiState` Loading/Loaded/Error (was an Optional flag — E1), + the Android
  `orderRepository.refresh()` warm (wired from the shell through `BookingSheetView`) and the
  server-code-wins pill (`effectiveCode`). New `BookingSuccessTimeline` ports `computeTimelineSteps`
  (`BookingSuccessScreen.kt`) exactly: the REAL 4-step Done/Active/Pending vertical timeline over
  status + `assignedEmployees` with the "just placed" pre-load fallback — replacing the 5-dot
  order-detail indicator, which was the wrong semantic (the ticket's "reuse LiveProgressLogic" hint
  didn't match Android's actual mapping; Android parity wins). View now mirrors Android's section
  order: labeled Arrival/Address/Total rows (street+city, right-aligned values), Loading spinner,
  timeline card with title, "what's next" note, selectable code. 13 new keys ×5 locales
  (`booking_success_progress`/`t1..t4_title|desc`/`arrival|address|total_label`/`whats_next`,
  Android wording). Tests: `BookingSuccessViewModelTests` (8 — loaded populates the summary, nil fetch
  degrades to `.error` with the pill intact, blank-id skip, single-flight, warm, effectiveCode) +
  `BookingSuccessTimelineTests` (8 — full status×assignment matrix incl. Cancelled). Both schemes
  BUILD SUCCEEDED; customer 578 tests / 2 known Stripe-key locals on iPhone 17 (26.x) AND the 16.4
  floor (Gate 8.5, by UDID); swiftformat 0.60.1 + swiftlint 0.65.0 --strict clean.
