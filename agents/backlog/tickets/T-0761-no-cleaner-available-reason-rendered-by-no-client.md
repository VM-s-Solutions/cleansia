---
id: T-0761
title: order.cancelled.no_cleaner_available is written by the unfilled-order sweep and rendered by no client — the copy and the three reason maps
status: todo
size: S
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: [ADR-0064]
layers: [frontend, android, ios]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Found by the Batch 2 docs lane on 2026-09-16, from the booking-policy parity gate
(`agents/tools/check-booking-policy-parity.mjs`): its `REASONS_NOT_YET_RENDERED` allow-list names
**`order.cancelled.no_cleaner_available`** as the one key the server writes that no client renders — the
list exists so the gate fails on the *next* unrendered key while this one is reported. The gate's own
summary line reads *"3 cancellation reason(s) render on every client"* for a catalogue of four.

**Ground truth today.** `OrderCancellationReasons.NoCleanerAvailable` is written by
`CancelUnfilledOrders` — the one no-show the platform can prove (the slot was reached with nobody on the
seat), which is also the one system cancellation that pays the apology credit
(`/product/business-rules#cancellation`). The customer gets the cancellation push and the credit, and the
order detail on customer web, Android and iOS shows a cancelled order **with no sentence saying why**:
`grep -rn no_cleaner_available src` matches the C# constant only. The other three reasons
(`payment_not_completed`, `recurring_not_confirmed`, `company_wind_down`) each have five locales of copy
and a map entry on all three clients.

## Doing (when picked up)

- **Customer web** — `pages.order_detail.cancellation_reason.no_cleaner_available` in the five locales of
  `apps/cleansia.app/src/assets/i18n/*.json`; the entry in the reason map in
  `libs/cleansia-customer-features/orders/src/lib/order-detail/order-detail.component.ts`.
- **Android customer** — `order_cancelled_reason_no_cleaner_available` in the five `strings.xml`; the
  entry in `OrderDetailScreen.kt`'s reason map.
- **iOS customer** — the key in `Localizable.xcstrings` (five locales) and the entry in
  `CancellationReasonCopy.swift`; XCTest written, compiled on the owner's Mac (memory: *"Owner compiles iOS
  on request"*).
- **The gate** — remove the key from `REASONS_NOT_YET_RENDERED` so the allow-list is empty and the gate
  fails on the next unrendered key with nothing carried.
- **Copy.** Tender-neutral like the wind-down sentence (T-0762 review #8): *no cleaner was available for
  this booking* — the refund and the apology credit are stated by their own lines, not by the reason.

## NOT

No backend change; no change to the sweep, the credit or the push. No new reason key.

## Acceptance criteria

- [ ] AC1 — a customer whose booking was cancelled by the unfilled-order sweep sees the reason sentence on
      the order detail of customer web, Android and iOS, in each of the five locales.
- [ ] AC2 — `node agents/tools/check-booking-policy-parity.mjs` exits 0 with *"4 cancellation reason(s)
      render on every client"* and an empty `REASONS_NOT_YET_RENDERED`.

## Status log

- 2026-09-16 — filed `todo` by the docs lane from the parity gate's allow-list; waiting on the owner to
  open it.
