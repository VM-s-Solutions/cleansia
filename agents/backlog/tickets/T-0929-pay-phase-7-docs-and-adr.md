---
id: T-0929
title: "Cleaner pay, phase 7: the docs and the decision record"
size: M
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: [T-0927]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
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
- R2: pay that arrives after its period was invoiced goes into the next open period and is invoiced with it.
- R3: payouts are manual bank transfers at launch, automated through the company's bank later, never through Stripe.
  This supersedes decision 48's Stripe Connect Express route. Stripe stays for customer card payments.
- R4: "Next payout" leaves the cleaner apps until payouts are automated.
- R5: the hand-made 12-25 Nov DEV period needs no repair; the rules below stop it from recurring.

The plan's decisions D1 to D39 apply as written on the plan page. A `$` marks a money slice: after it is built, a
separate reviewer re-derives the amounts by hand and checks that each new test fails when the change is reverted.

## Slices

Branch `docs/pay-and-payouts`. Docs come last, in one pass, once everything is green.

- [ ] **P7.1** A new ADR at the next free number (ADR-0082 at filing; ADR-0081 was taken on 2026-11-08) recording R1 to R4, the supersession of decision 48's route, the 8 August manual mark-paid ruling, and the removal of CanOpenPayPeriod and CanReopenPayPeriod.
- [ ] **P7.2** Rewrite the pay and payouts pages, the admin pay-periods page, business rules, infrastructure, features and the partner-app pages; banners on ADR-0002 D3.4, ADR-0046 and ADR-0041; replace "employment contract"; refresh Q-UI-04 and MANUAL_STEPS MS-13 step 5.
- [ ] **P7.3** Docs for Phase 5: notification types and severity, the 2-hour alert, Marketing as push only, the dispute page, row clicks, Close now and Change end date, and mobile labels by value.
- [ ] **P7.4** Docs for refunds: states and repayments, the refund flow, `refund.failed`, `refund_bounced` and the retry timing, the Refunds page, the ADR-0006 banner and Q-UI-05.

## Done

The docs and the ADR merged with green CI, and every row of this plan closed.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started.
- 2026-11-10 — Started on `docs/pay-and-payouts`, run with the lean workflow (one docs lane on Sonnet, one reviewer). The new ADR is ADR-0082.
