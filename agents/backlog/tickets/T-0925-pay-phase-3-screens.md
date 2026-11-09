---
id: T-0925
title: "Cleaner pay, phase 3: the screens"
size: L
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: [T-0924]
blocks: []
stories: []
adrs: []
layers: [backend, frontend, android, ios]
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

Branch `feat/pay-screens`. The contract first, then the four clients.

- [ ] **P3.1** $ Contract batch: the dashboard period as DateOnly dates, Next payout removed, current-period earnings from the open period only; invoice and period-pay DTOs gain the period dates; PayPeriodDto gains three counts; payroll report totals exclude Cancelled and Rejected; `PayLineType.DisputeCharge = 3` from the approved P4.4 design (T-0926 section 7), labelled in every client's line-type map in the same change; one regeneration of the admin and partner clients and the partner mobile spec.
- [ ] **P3.2** Admin period page: the heading from the dates, PaidAt and the three counts, a View invoices link, Mark paid enabled from the counts, and permissions on Create and Mark paid. Two additions from Phase 1's review: Create is hidden while a period is open (rev-8; D2 makes Create possible only when none is), and the Close now hint is right for a period already past its end (rev-7).
- [ ] **P3.3** $ Admin invoices: cash set off and amount to transfer on the detail and the list, Mark paid as an in-page confirm with the transfer details, and Approve and Cancel for Disputed.
- [ ] **P3.4** Partner web: set-off and transfer on invoices, dates formatted, two sorts removed, the dashboard label, rows kept on a list error, and My Period Pay preselecting the open period.
- [ ] **P3.5** Android: calendar dates, Next payout removed, the figure's new label and hint, set-off and transfer rows, Total earned from Paid invoices per currency, plurals in five locales, the period card's locale and year, and the cash text.
- [ ] **P3.6** iOS invoice list: rows survive a failed refresh, the employee-id lookup no longer blanks the list, pull-to-refresh always works, and the invoice-paid push replaces the path.
- [ ] **P3.7** iOS Pay and Earnings and the dashboard: calendar dates, Next payout removed, and the figure's label and hint.
- [ ] **P3.8** iOS invoices and plurals: set-off and transfer rows, dates formatted, Total earned from Paid per currency, six plural keys in five locales, and the cash text.
- [ ] **P3.9** Admin invoice and period lists format the new dates and stop offering three sorts.
- [ ] **P3.10** Partner web and Android format the new dates on the invoice detail and period pay.

## Done

One client regeneration and one mobile spec re-dump committed with the DTOs; every client green in CI; PR 3 merged.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started.
- 2026-11-09 — An exception to the P3.1 commit's claim that every new or changed test fails against the code before it: two changed Android tests pass there. InvoicesWireTest.theDetailDtoCarriesEverythingTheListOneDoesPlusItsOwn compares two constants in the test file, and DashboardWireTest.nullableByDesignStatsFieldsStayNullRatherThanBecomingPlaceholders only lost its nextPayoutDate assertion. Neither tests money; the three serial-name tests beside them fail before the change as they should.
- 2026-11-09 — P3.1, backend and contract (with the DisputeCharge label in admin web, partner web, Android and iOS): 7c3072e1a; the cancelled-invoice set-off and transfer fix: 035b6812f.
- 2026-11-09 — P3.2, admin web: 952a28959.
- 2026-11-09 — P3.3, admin web: f231b3518.
- 2026-11-09 — P3.4, partner web: 87562e630.
- 2026-11-09 — P3.5, Android: 25906a200; comment fixes fada8caa8 and 6b8917c0d.
- 2026-11-09 — P3.6, iOS: 1b56370cb.
- 2026-11-09 — P3.7, iOS: 4e708d4fa.
- 2026-11-09 — P3.8, iOS: f1ae3964d.
- 2026-11-09 — P3.9, admin web: 7033116e4.
- 2026-11-09 — P3.10, partner web: 88764e64a; Android: 8910bd5bf.
- 2026-11-09 — The one regeneration: admin and partner web clients and the partner mobile spec, committed in 7c3072e1a; the customer mobile spec was re-dumped and is unchanged.
- 2026-11-09 — Money review of P3.1 and P3.3: amounts re-derived by hand match (M1-M6 pass), and every new money test fails with its change reverted. Verdict changes requested on one must item, the ruling code in a test comment, fixed in fada8caa8. Of the nice items in scope, the cancelled-invoice figures (fixed in 035b6812f), the stale route comment (6b8917c0d) and the two Android tests that pass on the parent (recorded above) are closed; ConstantSymbol on the invoice detail waits on Mike. Four findings outside the branch are reported, not fixed: partner web's Cash I hold text, Android's invoice list blanking on a failed employee lookup, Close now on a period not yet started, and a legacy Rejected invoice blocking Mark paid.
- 2026-11-09 — In review as PR 3.
