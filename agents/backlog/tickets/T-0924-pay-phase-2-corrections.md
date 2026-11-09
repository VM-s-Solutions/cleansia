---
id: T-0924
title: "Cleaner pay, phase 2: corrections and one open period"
size: L
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: [T-0923]
blocks: []
stories: []
adrs: []
layers: [db, backend, frontend]
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

Branch `fix/pay-corrections`. One migration regen.

- [ ] **P2.1** Indexes and refund columns in one Initial regen: the invoice unique index ignores Cancelled invoices, one open period per tenant, and `FailedOn`, `RepaidOn`, `RepaymentMethod` and `RepaymentReference` on Refund, plus any column from P4.4's design.
- [ ] **P2.2** $ Cancel frees its jobs and the safety net reissues the invoice within minutes; Cancel is offered for Pending, Approved and Disputed invoices.
- [ ] **P2.3** $ `GenerateInvoice` e-mails the invoice with its PDF, under the same rules as the night run.
- [ ] **P2.4** Approve accepts Disputed, Adjust is allowed only for Pending and Disputed and re-renders the PDF, Reject is removed, and Regenerate PDF is hidden on Paid and Cancelled.
- [ ] **P2.5** $ Mark period as paid refuses while any pay is un-invoiced or any invoice is neither Paid nor Cancelled.

## Done

One migration regen verified by the integration suite; the DEV reset it owes waits for P6.2 and Mike's OK; PR 2 merged with green CI.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started.
