---
id: T-0928
title: "Cleaner pay, phase 6: DEV deploys, reset and proof"
size: M
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: [T-0923]
blocks: []
stories: []
adrs: []
layers: [infra]
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

Every step runs only with Mike's explicit OK, and every command is reported.

- [ ] **P6.1** After PR 1 merges: deploy DEV, with no reset.
- [ ] **P6.2** After PR 4: reset DEV through `execute-sql.yml`, deploy, bootstrap and seed, and set the 3-day DEV pay period length (P1.9).
- [ ] **P6.3** Prove a 3-day period on DEV: one invoice with all the jobs, the cash set off, the PDF and e-mail, and the next period open; then cancel and reissue, approve and mark paid.
- [ ] **P6.4** On an iPhone: the invoice list survives airplane mode, the dates match the admin, there is no Next payout, and counts read "1 job".
- [ ] **P6.5** After PR 5 deploys without a reset: login, the critical badge and type colours, dispute evidence, the Marketing notice, a promo push, and Change end date.
- [ ] **P6.6** Prove Close now on DEV right after P6.3.
- [ ] **P6.7** After PR 5, with Mike's OK for the DEV `refund.failed` subscription: a bounced refund, Needs action, and a recorded repayment.

## Done

Every DEV step run with Mike's OK and reported command by command; P6.3 and P6.6 pass.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started.
- 2026-11-10 — P6.1 skipped, Mike's answer "A": master already holds Phases 1 to 3 and the Phase 2 Initial, so DEV gets one reset and deploy after Phase 4 (P6.2), with his OK.
- 2026-11-10 — P6.2 done with Mike's OK: execute-sql.yml (environment DEV) ran reset-database.sql (run 42779493328); deploy-dev.yml mode=deploy forceProvision=true (run 42779626756) hit the known scm-auth-preflight 503 on all six app legs and was recovered by diagnose-dev.yml and two `gh run rerun --failed`, with only Warm dev sites left failed; then prod-bootstrap.sql (42787166737), insert_seed_data.sql (42787272419) and insert_local_dev_admin.sql (42787419822). All five APIs answer /health 200 and the customer web 200. DEV runs master with Phases 1 to 4 and 3-day pay periods.
- 2026-11-10 — P6.5 done with Mike's OK: deploy-dev.yml mode=deploy (run 42816658096) hit the scm-auth-preflight 503 on the customer-mobile leg and was recovered by diagnose-dev.yml (run 42819417689) and one `gh run rerun --failed`. All five APIs answer /health 200 and the customer web 200. DEV runs master with Phase 5 (#376).
- 2026-11-10 — P6.7 run on DEV with Mike's OK, after he added `refund.failed` to the DEV test-mode webhook endpoint. The bounce handling passes end to end on ORD-3B4A5F88, paid with the 5126 test card and cancelled by the customer on the customer web host: `refund.failed` arrived about 2 minutes later, the refund read Bounced under Needs action with its Stripe id, the critical `refund_bounced` alert appeared, the order stayed Refunded, the bank-transfer repayment read Repaid without moving the order, and both the order's and the customer's Refunds lists showed it. The administrator's path fails: an administrator's cancellation of ORD-B77780DB left refund `01M7305D5FCS95CBE3PVJ0Q517` (300 CZK) Not sent, its one Retry answered 400 `refund.failed`, and the hourly re-drive did not send it, because the Bicep gave the Stripe key to the two customer hosts only. Filed as T-0930.
