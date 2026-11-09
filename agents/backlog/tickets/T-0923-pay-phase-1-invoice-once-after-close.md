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
- R2: pay that arrives after its period was invoiced goes into the next open period and is invoiced with it.
- R3: payouts are manual bank transfers at launch, automated through the company's bank later, never through Stripe.
  This supersedes decision 48's Stripe Connect Express route. Stripe stays for customer card payments.
- R4: "Next payout" leaves the cleaner apps until payouts are automated.
- R5: the hand-made 12-25 Nov DEV period needs no repair; the rules below stop it from recurring.

The plan's decisions D1 to D39 apply as written on the plan page. A `$` marks a money slice: after it is built, a
separate reviewer re-derives the amounts by hand and checks that each new test fails when the change is reverted.

## Slices

Branch `fix/pay-invoice-after-close`. No schema change.

- [ ] **P1.1** $ The 5-minute sweep invoices only closed periods, aged from `ClosedAt`; its stale comments go; the SQLite tests that pin today's behaviour are rewritten, and a Postgres case is added.
- [ ] **P1.2** $ `GenerateInvoice` refuses an open period with `pay_period.not_closed`, which also guards the admin generate-invoice endpoint.
- [ ] **P1.3** $ The night close commits the close and its successor before invoicing, loops only over cleaners with un-invoiced pay, and e-mails no cleaner without pay; the wind-down close gets the same order.
- [ ] **P1.4** $ Close now, with a required reason kept in the audit log, replaces the admin Close; Reopen and Open, their two policies and their callers go; the admin client is regenerated.
- [ ] **P1.5** One open period, never in the future: Create refuses while a period is open or for a start after today, and `EnsureOpenPeriodAsync` seeds from max(today, latest end + 1).
- [ ] **P1.6** The Close now panel on the open period's page, in five locales, built with the cleansia-design skill.
- [ ] **P1.7** $ `UpdatePayPeriod` changes only the end date, from tomorrow to the last allowed day, with the new key `pay_period.end_too_soon`.
- [ ] **P1.8** Before Phase 2, no code: P4.4's design is written in T-0926 and approved by Mike (D12).
- [ ] **P1.9** The period length is one setting, `PayPeriodClosing:LengthInDays` (default 14; 3 on DEV only, through the dev bicep), read by every path that creates or extends a period; Create takes only a start date.

## Done

The five money slices reviewed by a separate reviewer; Cleansia.Tests, Cleansia.IntegrationTests and Cleansia.HostTests green; the admin client regenerated with its DTOs and both mobile specs unchanged; PR 1 merged with green CI.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Waits for Mike's go on the Phase 1 scope.
