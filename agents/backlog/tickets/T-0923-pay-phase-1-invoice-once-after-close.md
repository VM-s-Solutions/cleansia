---
id: T-0923
title: "Cleaner pay, phase 1: invoice once, after the period closes"
size: L
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: true
---

## Context

Mike's cleaner-pay fix plan, written on 8 November 2026 after a read-only investigation and handed over on 9 November.
Every slice's files, lines and tests are on the plan page: https://claude.ai/artifact/XvfiW1obDsEqk4E1eNL56C. Read a slice's full entry there before starting it.

**Root cause.** The 5-minute `FiscalReconciliation` sweep is the only producer of generate-invoice messages, and it never
reads `PayPeriod.Status`; neither does `GenerateInvoice`. So each cleaner's first job of a period is invoiced alone
within minutes, and four checks then refuse every later job in that currency: the sweep's anti-join,
`InvoiceAlreadyExists`, the night close's skip, and the unique index on (EmployeeId, PayPeriodId, CurrencyId).
Invoicing at close was the design (`CalculateOrderPayHandler.cs:16-18`, and the self-billing agreement).

**Mike's rulings, 8 November 2026.**
- R1: early close is an exception. A period lasts at most 14 days and closes in the 02:00 UTC run after its end date.
  An admin can end the open period today (Close now, with a reason) or change its end date up to its 14th day. A
  closed period is never reopened.
  - The length became one setting in P1.9, `PayPeriodClosing:LengthInDays`: 14 by default, 3 on DEV only (D39).
- R2: pay that arrives after its period was invoiced goes into the next open period and is invoiced with it.
- R3: payouts are manual bank transfers at launch, automated through the company's bank later, never through Stripe.
  This supersedes decision 48's Stripe Connect Express route. Stripe stays for customer card payments.
- R4: "Next payout" leaves the cleaner apps until payouts are automated.
- R5: the hand-made 12-25 Nov DEV period needs no repair; the rules below stop it from recurring.

The plan's decisions D1 to D39 apply as written on the plan page. A `$` marks a money slice: after it is built, a
separate reviewer re-derives the amounts by hand and checks that each new test fails when the change is reverted.

## Slices

Branch `fix/pay-invoice-after-close`. No schema change.

- [x] **P1.1** $ The 5-minute sweep invoices only closed periods, aged from `ClosedAt`; its stale comments go; the SQLite tests that pin today's behaviour are rewritten, and a Postgres case is added.
- [x] **P1.2** $ `GenerateInvoice` refuses an open period with `pay_period.not_closed`, which also guards the admin generate-invoice endpoint.
- [x] **P1.3** $ The night close commits the close and its successor before invoicing, loops only over cleaners with un-invoiced pay, and e-mails no cleaner without pay; the wind-down close gets the same order.
- [x] **P1.4** $ Close now, with a required reason kept in the audit log, replaces the admin Close; Reopen and Open, their two policies and their callers go; the admin client is regenerated.
- [x] **P1.5** One open period, never in the future: Create refuses while a period is open or for a start after today, and `EnsureOpenPeriodAsync` seeds from max(today, latest end + 1).
- [x] **P1.6** The Close now panel on the open period's page, in five locales, built without the cleansia-design skill on Mike's ruling of 2026-11-09 ("Build without it"), so it follows the existing admin patterns and tokens.
- [x] **P1.7** $ `UpdatePayPeriod` changes only the end date, from tomorrow to the last allowed day, with the new key `pay_period.end_too_soon`.
- [x] **P1.8** Before Phase 2, no code: P4.4's design is written in T-0926 and approved by Mike (D12).
- [x] **P1.9** The period length is one setting, `PayPeriodClosing:LengthInDays` (default 14; 3 on DEV only, through the dev bicep), read by every path that creates or extends a period; Create takes only a start date.

## Done

The five money slices reviewed by a separate reviewer; Cleansia.Tests, Cleansia.IntegrationTests and Cleansia.HostTests green; the admin client regenerated with its DTOs and both mobile specs unchanged; PR 1 merged with green CI.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Waits for Mike's go on the Phase 1 scope.
- 2026-11-09 — Mike's go on the Phase 1 scope ("Yup"). For P1.6 he said "Build without it": the cleansia-design skill
  is not on this Mac, so the panel follows the existing admin patterns and tokens.
- 2026-11-09 — P1.1 done (`8f561470b`, then `23ad5c060`). The 5-minute sweep lists pay only in a period that is not
  `Open`, aged from `ClosedAt`, and the stale stub and fan-out comments are gone. `23ad5c060` widened "Closed only" to
  "not Open" after the money review, so pay in a period marked paid before the sweep reached it is still invoiced.
  Tests: `FiscalReconciliationQueryTests` rewritten and the Postgres `InvoiceReconciliationCandidatesTests` added; one
  unit and one Postgres case failed before the change, and the Paid case failed before `23ad5c060`.
- 2026-11-09 — P1.2 done (`c321e816d`, then `23ad5c060`). `GenerateInvoice` refuses an open period with
  `pay_period.not_closed`, a key already in the admin locales and CONTRACT. Tests: the `GenerateInvoiceValidatorTests`
  Open case failed before the change, and its Paid case, which now passes, failed before `23ad5c060`.
- 2026-11-09 — P1.3 done (`dfdab34fc`). The night close commits the close and the next period before it invoices,
  loops only over cleaners with un-invoiced pay, and sends no "period closed" e-mail to a cleaner without pay; the
  wind-down close takes the same path. Tests: one unit case and five Postgres cases failed before the change.
- 2026-11-09 — P1.4 done (`b03d66404`, `82db30774`, `286d31ea0`, `bcc3b7d81`). Close now takes a required reason, kept
  in the audit log with the old and new end, ends the period today, opens the next one and refuses a period that has
  not started (`pay_period.not_started`). Reopen, Open, their two policies, `pay_period.already_paid` and
  `payroll.unpaid_invoices_exist` are gone. `82db30774` is the one admin client regeneration and removes the old
  callers. Tests: 12 unit cases failed against compile stubs before the change.
- 2026-11-09 — P1.5 done (`e7d2fa6c3`, `4d929d491`, `23cdf3a56`). Create is refused while a period is open and for a
  start after today, the automatic first period starts at today or the day after the latest end, whichever is later,
  and the admin Create dialog defaults its start to the day after the latest end. `23cdf3a56` makes Create return one
  refusal, not two joined keys the admin app could not translate. Tests: two unit, two Postgres and four web cases
  failed before their changes, and one more unit case before `23cdf3a56`.
- 2026-11-09 — P1.6 done (`9a52ee061`). The open period's page has the Close now panel: the hint, a required reason of
  up to 500 characters, loading, the refusal shown inline and a reload on success, under `CanClosePayPeriod`, in five
  locales. Tests: 8 component and facade cases failed against stubs before the change.
- 2026-11-09 — P1.7 done (`ae924e9ca`). Change end date keeps the stored start and takes an end from tomorrow to the
  last allowed day; `pay_period.end_too_soon` is new, `pay_period.invalid_duration` is reworded, and each change of
  the end is audited. Tests: 7 unit cases failed against compile stubs before the change.
- 2026-11-09 — P1.8 done (`1a512aa6b`). P4.4's design is in T-0926: no new column, one index change for P2.1 and one
  new pay line type for P3.1. It waits for Mike's OK before Phase 2.
- 2026-11-09 — P1.9 done (`df1216d42`; its web half is in `82db30774` and `beb710675`). The length is
  `PayPeriodClosing:LengthInDays`, 14 by default and 3 only in the dev bicep, which reaches DEV only at the reset, with
  Mike's OK. Every period-creating path reads it, Create takes only a start date, and `PayPeriodDto` gains
  `LatestEndDate`; the admin and partner web clients are regenerated for it and both mobile specs are unchanged.
  Tests: 7 unit and 2 Postgres cases failed before the change.
- 2026-11-09 — Money review of P1.1 to P1.4 and P1.7: the amounts were re-derived by hand, and the tests that pin each
  change failed with it reverted. P1.1 and P1.2 failed it on one case (Close now, then Mark paid, left the period's pay
  un-invoiced for good), fixed test-first in `23ad5c060`; that fix has had no separate money re-review.
- 2026-11-09 — Gates on the head merged with master (`10b75e186`): Cleansia.Tests 9229/9229, Cleansia.IntegrationTests
  1050/1050, Cleansia.HostTests 518/518, and no pending model change. The admin and partner error-contract parity
  specs, the tests of the 71 affected web projects, both web typechecks and the admin production build pass.
- 2026-11-09 — The money re-review of `23ad5c060` passed: the amounts were re-derived by hand, and every assertion it
  added failed with the change reverted. The security review of #372 at `0907ccaca` passed against S1 to S12.
- 2026-11-09 — Shipped: #372 merged to master as eb9ea0c4a.
