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
- 2026-11-09 — P2.1 done (`763dc7ef4`, `e3cd4838f`). `Initial` is regenerated once, from `20261028213040` to
  `20261109143607`. The invoice unique index ignores cancelled invoices (`"Status" <> 6`), and
  `IX_PayPeriods_TenantId_Open` allows one open period per company (`"Status" = 1`, `NULLS NOT DISTINCT`). P4.4's
  design, which Mike approved on 2026-11-09 ("Go with them"), needs no column: `IX_OrderEmployeePays_OrderId_EmployeeId`
  gets `"LineType" <> 3`, beside a new `IX_OrderEmployeePays_OrderId_EmployeeId_DisputeCharge` on `"LineType" = 3`.
  `Refunds` gains `FailedOn`, `RepaidOn`, `RepaymentMethod` (the new `RefundRepaymentMethod`) and
  `RepaymentReference`, all nullable, which nothing writes yet. `EnsureOpenPeriodAsync` takes a unique violation as
  the period another writer has just opened, and a Close now that loses that race fails with
  `payroll.pay_period.not_open` instead of a 500. MS-2 owes one DEV reset for `20261109143607`, at P6.2 and only with
  Mike's OK. Tests: 4 unit cases and 3 Postgres cases (`PayrollUniqueIndexTests`) failed before the change, and no
  model change is pending.
- 2026-11-09 — P2.2 done (`3318b01f7`, `ae9b4bd5e`, and after the money review `0d97a3a54`, `c9fb04bee`, `d42832731`,
  `dee1236b7`). Cancel frees the invoice's pay rows (`OrderEmployeePay.UnassignFromInvoice`), and the sweep's
  anti-join, `GenerateInvoice`'s already-exists check and the night close's skip ignore a cancelled invoice. So the
  safety net issues the corrected invoice, with a new number and variable symbol, within minutes. The admin offers
  Cancel on Pending, Approved and Disputed invoices. Its dialog says that a corrected invoice follows, rebuilt from the
  order payments, so a bonus or deduction set through Adjust must be set again; that amount changes go through Adjust;
  and that an invoice whose transfer was sent is marked paid instead. My Pay links the corrected invoice, and the pay
  report and the cleaner's earnings leave cancelled invoices out of the money (`c9fb04bee`, beyond the plan entry).
  Tests: 3 unit cases, `CancelledInvoiceReissueTests` on Postgres and 1 web case failed before the change, and 3 unit
  cases and 1 Postgres case failed before the two money fixes.
- 2026-11-09 — P2.3 done (`be132ff96`, `f34980e62`). `GenerateInvoice` e-mails each rendered invoice with its PDF after
  the post-commit render, under the night run's rules: only an active cleaner with an active account and an address,
  in the cleaner's language, one e-mail without an attachment when nothing rendered, and a failed send is logged
  without failing the command. Tests: 4 unit cases failed before the change, and the Postgres case in
  `MixedCurrencyPeriodInvoicingTests` failed with the send removed. `f34980e62` adds the inactive-cleaner and
  blank-address cases, which fail with their guards removed.
- 2026-11-09 — P2.4 done (`6cc51aa7e`, `a576162f6`). Approve accepts a Disputed invoice and keeps the note written
  through Adjust or Dispute. Adjust works only on Pending and Disputed invoices and renders the PDF again after
  saving. Reject is gone: the command, its endpoint, `CanRejectInvoice`, `EmployeeInvoice.Reject` and the admin UI.
  The admin client is regenerated in `6cc51aa7e` (the reject removals only), and both mobile specs re-dump unchanged.
  The admin offers Approve on Pending and Disputed invoices and hides Regenerate PDF on Paid, Cancelled and legacy
  Rejected ones. Tests: 5 unit cases, the host test of the removed route and 5 web cases failed before the change.
- 2026-11-09 — P2.5 done (`bf8f844cb`, `b609d7f3f`). Mark period as paid refuses while any pay row in the period has
  no invoice (`pay_period.has_uninvoiced_pay`) or any invoice is neither Paid nor Cancelled
  (`pay_period.has_unpaid_invoices`), read through `CompanySettlementReader.ReadPayPeriodAsync` under the archive
  gate's predicates. Both keys are in the five admin locales and the admin CONTRACT. `b609d7f3f` rewords four comments
  that still said a period could be marked paid before its pay was invoiced. Tests: 3 unit cases, the reader's
  Postgres case and the admin parity spec failed before the change.
- 2026-11-09 — Two fixes to Phase 1's P1.4, carried here as deliberate additions. Close now's free-text reason stays
  out of the request log on all five hosts (`2ed290217`), and Close now opens no next period for a deactivated
  company, by the night run's predicate (`4ffdf51c0`). Tests: `RequestLogAdminFreeTextPathTests` failed on all five
  hosts, and one `ClosePayPeriodTests` case failed, before the change.
- 2026-11-09 — Money review of P2.2, P2.3 and P2.5, and of P2.1's indexes on the money paths: passed. The amounts were
  re-derived by hand and on Postgres, and every new test failed with its change reverted. Pay of 600, 400 and 300,
  with 1,500 of cash held, gives an invoice of 1,300, all of it set off. The cancel enters the 1,300 back and frees
  the three rows, and the corrected invoice sets off the same 1,300, with nothing to transfer, under a new number and
  variable symbol. The review raised seven findings outside the slices' tests, all fixed on this branch: My Pay
  linking the cancelled invoice (`0d97a3a54`), the pay report and earnings counting reissued jobs twice (`c9fb04bee`),
  two warnings for the cancel dialog (`d42832731`, `dee1236b7`), stale comments (`b609d7f3f`), two untested e-mail
  guards (`f34980e62`) and two plan codes in test comments (`8805e2597`). The two logic fixes, `0d97a3a54` and
  `c9fb04bee`, have had no separate money re-review. The general review's one must, the stale comments, is the same
  fix; of its other two findings, one was refuted and one predates this branch.
- 2026-11-09 — Gates at `dee1236b7`, with master unmoved at `eb9ea0c4a`: Cleansia.Tests 9260/9260,
  Cleansia.IntegrationTests 1057/1057, Cleansia.HostTests 519/519, and no pending model change. The admin and partner
  error-contract parity specs (22/22, 21/21), the 5,347 tests of the 71 affected web projects, both web typechecks and
  the admin production build pass, and so do the backlog, docs-refs and catalog-claims checkers.
