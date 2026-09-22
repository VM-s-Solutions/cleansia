---
id: T-0771
title: The revenue report — completed and paid, by completion date, minus both refund legs (D3)
status: done
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-19 on D3 (the revenue report semantics — T-0702 AC3 unmet though its row reads done),
verbatim: *"paid and completed orders, minus refunds, per currency, by completion date."* Orchestrator
default applied: an order counts in the period of its **completion date**; its refunds are subtracted from
that order **whatever their date** (net per order); cancelled and unpaid orders are excluded. No ADR — no
seam moves, no schema moves; the design was argued by the batch-6 panel (challenge D1–D7) and is recorded
here.

**Ground truth at filing** (`2b9cd268`, re-checked 2026-09-19). `GetRevenueReport.Handler`
(`GetRevenueReport.cs:33-153`) selects **every** order in the currency whose `CleaningDateTime` is in the
range (`OrderRepository.cs:272-287`, `GetOrdersByDateRangeAsync`), sums `TotalPrice` gross (`:52`), buckets
by `CleaningDateTime` (`:63-75`) and subtracts no refund. One currency per report is already the design
(`ReportFilter.CurrencyId`, `ReportFilter.cs:5-9`). `Order.CompletedAt` (`Order.cs:92-101`) is stamped only
inside `Order.CompleteOrder` (`:903`), whose one production caller is `CompleteOrder.cs:253` —
**`AdminOverrideOrderStatus` to `Completed` appends the track and stamps nothing** (`:125-126`), so every
"the cleaner never tapped" repair is a `Completed` order with a null `CompletedAt` (panel D1: real revenue,
not a fixture artefact). Refunds are two legs: the card share is a `Refund` row (`Refund.cs:10-56`;
`Succeeded` = money moved, `:92-96`), the credit share goes back through
`ICreditAccountRepository.ReturnCreditAsync` as a `CreditTransaction` with `Reason == OrderPaymentReturned`
(`RefundService.SplitAcrossTenders`, `RefundService.cs:259-279, 286-298`; `CreditAccountRepository.cs:143-156`)
— netting the card leg only leaves 500 of revenue on a 2 000 order the customer has entirely back (panel
D2). A lost chargeback writes no money row (`HandlePaymentNotification.cs:519-521, 544-549`; panel D3 →
O-D3-2). An abandoned card checkout appends `Cancelled` without `order.Cancel(...)` and carries no
`CancelledAt` (`HandlePaymentNotification.cs:329-330`; panel D4). `PaymentStatus.Disputed` has no
production writer (panel D5).

## Doing (backend)

- `Order.MarkCompletedAt(DateTime)` — idempotent, the first stamp wins; `AdminOverrideOrderStatus` calls it
  when the target is `Completed`, before the append. It does not run the completion pipeline (pay, receipt,
  fiscal) — the override's pre-existing shape; it only makes the completion *dated*.
- `IOrderRepository.GetCompletedPaidOrdersByCompletionDateAsync(startUtc, endUtc, currencyId)` **replacing**
  `GetOrdersByDateRangeAsync` (one caller; the old name says the wrong axis): `CurrencyId == currencyId &&
  CurrentStatus == Completed && CompletedAt != null && CompletedAt in [start, end] && PaymentStatus in
  {Paid, PartiallyRefunded, Refunded, Disputed}` (`= ANY(@p)`), services/packages included,
  `OrderStatusHistory` no longer included, `AsSplitQuery()`. `CountCancelledBookingsInPeriodAsync(startUtc,
  endUtc, currencyId)` by `CancelledAt` (so an abandoned checkout is not counted).
- `IRefundRepository.GetSucceededRefundTotalsByOrderAsync(orderIds)` (`Succeeded` only, any date, grouped by
  order); `ICreditAccountRepository.GetReturnedTotalsByOrderAsync(orderIds)` (`OrderPaymentReturned` rows on
  those orders, grouped by order).
- `GetRevenueReport.Handler`: net per order over **both legs** (`NetOf = TotalPrice − card − credit`);
  `TotalRevenue` stays gross; daily by `CompletedAt` with a net `Amount` and a `Refunded` figure; by-service
  and by-package over net (same even split); by-tender gains `RefundedToCard` / `ReturnedToCredit` and the
  derived `NetOnTender = SettledOnTender − RefundedToCard` (the line that matches the gateway statement —
  the gateway never saw the credit); `TotalRefundedToCard` + `TotalReturnedToCredit` on the DTO with derived
  `TotalRefunded` / `NetRevenue`; `AverageOrderValue` net; `CancelledOrders` from the separate count; growth
  unchanged in shape, now over net daily amounts.
- DTO additions (additive, defaulted): `RevenueReportDto.TotalRefundedToCard`, `.TotalReturnedToCredit`,
  computed `TotalRefunded`, `NetRevenue`; `DailyRevenue.Refunded`; `RevenueByPaymentType.RefundedToCard`,
  `.ReturnedToCredit`, computed `NetOnTender`. The admin NSwag client regenerated and committed.
- Tests — unit `GetRevenueReportHandlerTests` (the separate cancelled count; 1 000 + 2 000 with a 300 card
  refund → 3 000 / 300 / 0 / 2 700 / 1 350; mixed-tender full refund → 0, partial 1 000 → 1 000; daily by
  `CompletedAt`, net; a fully-refunded order counts, contributes 0, shows under `Refunded`; by-service net
  split; the by-tender row of AC5; growth over net). Integration `RevenueReportPredicateTests`
  (Testcontainers: the status/payment predicate; the `endUtc` boundary and the null-`CompletedAt`
  exclusion; the override-completed order on its override date; the currency exclusion and the refund
  currency pin; `Pending`/`Failed` refund rows excluded, an out-of-period refund included, two refunds sum;
  only `OrderPaymentReturned` rows count and an expired-checkout return is out; the cancelled count by
  `CancelledAt` skipping an abandoned checkout).

## Doing (web)

- `reports.component.html` / `reports.facade.ts`: headline card → `netRevenue` (`pages.reports.net_revenue`)
  with the sub-line `pages.reports.gross_and_refunds` = *"{gross} gross − {refunded} refunded (incl.
  {credit} returned as credit)"*; the *Completed orders* card removed (it would always equal the total);
  the total-orders label → `pages.reports.completed_paid_orders`; the cancelled card's tooltip
  `pages.reports.cancelled_orders_hint` = *"Cancelled bookings in this period, by cancellation date;
  abandoned card checkouts are not bookings; not part of revenue"*; the by-tender columns *Refunded to
  card*, *Returned as credit*, *Net on tender* with `pages.reports.revenue_by_payment_type_hint` = *"Net on
  tender is the figure to reconcile against the gateway statement"*; the page description
  `pages.reports.description` = *"Revenue is completed and paid orders by completion date, in one currency,
  minus every refund on those orders — card refunds and credit returned — whatever the refund's date. A
  refund therefore reduces the month the order completed in, not the month it was issued. Cancelled and
  unpaid orders are not revenue. Lost chargebacks are not subtracted (the platform does not record the
  reversed amount). Cash orders refunded by hand have no refund record and show gross."*; the by-status
  copy names no *Disputed* row; five locales; `reports.facade.spec.ts`.

## NOT

- No multi-currency report; no market-local period; no refund breakdown by reason; no chargeback
  subtraction (O-D3-2); no manual-refund record for cash orders; no dashboard or payroll-report changes; no
  backfill of `CompletedAt` on historic override-completed DEV rows (finding F11).

## Done looks like

For a period, the headline is Σ `TotalPrice` of completed-and-paid orders completed in the period minus
every succeeded card refund and every returned credit on those orders; an override-completed order is
dated; a cancelled order contributes nothing; the by-tender net line matches what a Stripe statement would
show; the page reads the sentence the owner said, plus the three caveats.

## Acceptance criteria

- [ ] **AC1** — *Given* orders completed in March: A 1 000 paid, B 2 000 paid with a 300 card refund issued
      in April, C 500 cancelled (`CancelledAt` in March), D 800 `Confirmed`, *when* the March report is
      requested in their currency, *then* `TotalRevenue = 3 000`, `TotalRefundedToCard = 300`,
      `TotalReturnedToCredit = 0`, `NetRevenue = 2 700`, `TotalOrders = CompletedOrders = 2`,
      `CancelledOrders = 1`, `AverageOrderValue = 1 350`.
- [ ] **AC2** — *Given* a 2 000 order settled 500 credit + 1 500 card and refunded in full (a 1 500 `Refund`
      row and a 500 `OrderPaymentReturned` credit transaction), *then* it counts in `TotalOrders`,
      contributes **0** to `NetRevenue`, and appears under `Refunded` in the by-status table; *given* a
      partial refund of 1 000 on the same order instead, *then* it contributes 1 000.
- [ ] **AC3** — *Given* an order booked (`CleaningDateTime`) on 31 March and completed on 1 April, *then* it
      is in April's report and not March's; the daily bucket is 1 April.
- [ ] **AC4** — *Given* a paid order completed by the administrator's override on 5 March, *then* it is in
      March's report on 5 March; `CompleteOrder`'s own stamp is unchanged.
- [ ] **AC5** — *Given* a card order of 1 000 with 100 credit and a 200 refund (180 card + 20 credit), *then*
      the Card row reads `TotalRevenue 1 000`, `SettledFromCredit 100`, `SettledOnTender 900`,
      `RefundedToCard 180`, `ReturnedToCredit 20`, `NetOnTender 720`.
- [ ] **AC6** — *Given* a `Pending` and a `Failed` refund row on an order in the period, *then* neither
      subtracts.
- [ ] **AC7** — *Given* an abandoned card checkout (`Cancelled` track, `CancelledAt` null) in the period,
      *then* `CancelledOrders` does not count it; *given* a cancelled booking (`CancelledAt` set), *then* it
      does.
- [ ] **AC8** — *Given* an order in another currency completed in the period, *then* it is absent; the
      `CurrencyCode` on the DTO is the requested currency's.
- [ ] **AC9** — The page shows *Net revenue* as the headline with the gross/refunded/credit sub-line, no
      *Completed orders* card, the by-tender table with *Refunded to card*, *Returned as credit* and *Net on
      tender*, and the page description in five locales; the by-status copy names no *Disputed* row.

## Implementation notes

*Why `TotalRevenue` stays gross and `NetRevenue` is derived:* the by-tender table exists to reconcile
against a Stripe statement (`RevenueReportDto.cs:59-63`), which shows gross charges and refunds as separate
lines; the ruling's number is the headline. *Why a lost chargeback is a named gap, not a subtraction:* the
amount is an inference (a charge partly refunded before the dispute breaks the equality) and a wrong number
in a money report is worse than a stated gap — O-D3-2; the fix, if wanted, is `Dispute.ChargebackAmount`
stamped from the `lost` webhook plus one `GroupBy`. *Why `CancelledOrders` moves to its own axis:* the
ruling removes cancelled orders from revenue, not from the admin's sight; counted by `CancelledAt` so an
accountant reading the order list does not file the difference as a bug. Closed months change when a later
refund lands — the ruling's default, said on the page. NSwag emits the computed properties as read-only
members (the existing `SettledOnTender` proves the shape); the admin client regen is the lane's to run and
report.

**Security (Gate 3) — `security_touching: true`, routed to the Security Reviewer beside the code
reviewer.** A response DTO changes (`RevenueReportDto` and its two nested rows gain money members) and
three repository reads are new or replaced; the reviewer checks that every read carries the currency and
stays inside the ambient company (no `IgnoreQueryFilters` — the report is an admin's view of *their*
company's books), that the refund and credit roll-ups group by order id and cannot cross tenants, and
that the DTO carries no per-customer identity (S2/S6). Money math is a Gate 6.5 class: AC1/AC2/AC5 are the
executable assertions and the panel re-reads them (memory: refund money-math needs adversarial review).

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; runs after T-0770 on the backend lane, then
  the admin web part.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet;
  no dependency, so it is ready the moment the backend lane is free. `security_touching` corrected to
  `true` — a response DTO and repository reads change, which is Gate 3's own definition; the routing note
  above says what the Security Reviewer looks at.
- 2026-09-19 — done in c45a4af4 + 1fa06aa8 · web 56884360 + df7c9ba9: completed + paid by CompletedAt, net of both refund legs (card refunds and credit returned), cancelled/unpaid excluded, the admin override stamps CompletedAt and a test goes red for any Completed writer that forgets to; lost chargebacks named on the page (O-D3-2); the admin client regenerated; the report page reads the net figure and names both legs. Verified in the main-session pass: backend unit suite 6 080 green, web 74 projects / 2 893 specs, lint 77, typecheck 3/3; the Postgres and host suites need Docker Desktop, which was down on the machine that day — CI is their first execution.
