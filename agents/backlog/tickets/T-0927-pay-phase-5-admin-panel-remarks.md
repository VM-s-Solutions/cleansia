---
id: T-0927
title: "Cleaner pay, phase 5: the admin panel remarks and refunds"
size: L
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-09
depends_on: [T-0926]
blocks: []
stories: []
adrs: []
layers: [backend, frontend, android, ios]
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

Branch `fix/admin-remarks`. No schema change; two admin-only regenerations (P5.16 and P5.34) with both mobile specs unchanged.

- [ ] **P5.1** Table rows open from the keyboard.
- [ ] **P5.2** `showApiError` never shows a raw code.
- [ ] **P5.3** Proof first: a never-focused required field shows its error after `markAllAsTouched`; dropped if the spec passes today.
- [ ] **P5.4** Orders, Receivables, Invoices, Employees, Disputes and Pay periods open on a row click.
- [ ] **P5.5** Customers, Promo codes, Email templates, the three audit tables and Legal documents open on a row click.
- [ ] **P5.6** Row writes in twelve list facades raise the list's own loading.
- [ ] **P5.7** Pending flags on the employee, document, invoice-download, promo and consent buttons; the city dialog closes on success only.
- [ ] **P5.8** Partner web loading and download flags.
- [ ] **P5.9** The admin order detail and reports in words.
- [ ] **P5.10** E-mail type and language names, four missing keys, two loyalty sources, and GDPR self and system.
- [ ] **P5.11** No English close notes; an automatic close reads "Automatically".
- [ ] **P5.12** Customer web: payment type and status in words, and the navbar role line removed.
- [ ] **P5.13** Partner web: ten raw keys, extra labels from slugs, and the chart legend and months in the UI language.
- [ ] **P5.14** Customer Android: dispute reason and status labels by value.
- [ ] **P5.15** Customer iOS: dispute labels by value, payment status 6, and two refusal strings in CleansiaCore.
- [ ] **P5.16** The critical notification catalogue, the admin badge counting critical only, an admin-only critical flag, and the first admin regen.
- [ ] **P5.17** Each notification shows its type in colour, icon and words, with All, Critical and Info tabs (D17).
- [ ] **P5.18** `admin.order.no_cleaner_soon`: an urgent alert inside 2 hours beside the 24-hour one (D15).
- [ ] **P5.20** Dispute reasons by value.
- [ ] **P5.21** Dispute notes required, the incomplete-charge toast, "Reason the cleaner sees", and a cleared refund amount sent as none.
- [ ] **P5.22** The dispute evidence section, with the order number linking to the order page.
- [ ] **P5.23** The settlement choice in the dispute Summary.
- [ ] **P5.24** The Marketing send button submits, with a test that fails on any button with no action.
- [ ] **P5.25** Promo fan-out to active customers only, keyed per campaign.
- [ ] **P5.26** A send that reaches nobody is refused, and "already sent" shows when nothing was enqueued.
- [ ] **P5.27** $ A failed or bounced refund is marked and raises `admin.payment.refund_bounced`; it lands before P5.34.
- [ ] **P5.28** Change end date beside Close now.
- [ ] **P5.29** `loadAdminCodes()` leaves the admin AppComponent.
- [ ] **P5.30** The Marketing page says e-mail campaigns are not available yet (D22).
- [ ] **P5.31** A refused dispute or admin refund alerts after 30 minutes instead of 24 hours (D16).
- [ ] **P5.32** `GetPagedRefunds` with its six states and `CanRetry`.
- [ ] **P5.33** $ `RetryRefund` through `RefundService.RedriveAsync`, which checks Stripe first.
- [ ] **P5.34** $ `RecordRefundRepayment` for Failed and Bounced refunds, then the second admin regen.
- [ ] **P5.35** The Refunds page under Orders, opening on Needs action.
- [ ] **P5.36** Refunds buttons on the order and customer pages.

## Done

PR 5 merged with green CI; no migration, no DEV reset and no mobile re-dump. P5.19 was dropped by D16.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started.
- 2026-11-10 — Started on `fix/admin-remarks`, run with the lean workflow (one reviewer, effort by step, Sonnet for label sweeps). P5.19 stays dropped (D16).
