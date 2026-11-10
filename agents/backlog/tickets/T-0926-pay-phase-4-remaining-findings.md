---
id: T-0926
title: "Cleaner pay, phase 4: the remaining findings"
size: M
owner: Michael Chaban
created: 2026-11-09
updated: 2026-11-10
depends_on: [T-0925]
blocks: []
stories: []
adrs: []
layers: [backend]
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

Branch `fix/pay-findings`.

- [x] **P4.1** A rejected pay calculation logs at Error with the order, the employee and the reason; the admin-override question is noted for Mike.
- [x] **P4.2** Period-end reminders go only to cleaners with work in the period.
- [x] **P4.3** Blob paths use a fixed date format, and e-mails format the period in the recipient's language.
- [x] **P4.4** $ A dispute charge on an already-invoiced job becomes a deduction on the cleaner's next invoice. Its design, including what happens when that invoice would go negative, is written in this ticket and approved by Mike before Phase 2 starts (P1.8, D12).
- [ ] ~~**P4.5** Optional: a QR Platba code in the payment block of CZK invoice PDFs.~~ Dropped: Mike accepted the leaner Phase 4 on 2026-11-10.
- [x] **mrc-1** An invoice gets a concurrency token, so two admin actions on one invoice cannot both commit (a Mark paid racing a Cancel would set the cash off twice). Folded in by Mike on 2026-11-10.
- [x] **F4** Partner web's Cash I hold text says the cash is set off when the next invoice is issued, not against the next payout. Folded in by Mike on 2026-11-10.
- [x] **F5** Android's partner invoice list keeps its rows when the employee-id lookup or a refresh fails, as iOS does since P3.6. Folded in by Mike on 2026-11-10.
- [x] **F6** Close now is not offered on a period that has not started yet. Folded in by Mike on 2026-11-10.

## Design (P1.8)

Written on 9 November 2026 for Mike's OK before Phase 2 starts (D12). Design only: nothing here is built in Phase 1.
Line numbers are at `c321e816d`.

**In short**
- A charge on a job that is already invoiced becomes a new pay row of the cleaner in the open pay period: a *dispute
  charge* line on the disputed order, linked to the dispute, with the reason the cleaner reads. The issued invoice and
  its job row are never written.
- The next invoice takes it like any other pay row, as a deduction. No invoicing path needs a new reader.
- No invoice goes below zero. A charge that does not fit whole waits in the open period for the next invoice.
- **No new column.** A pay row already holds the dispute link (`DeductionDisputeId`), the reason (`DeductionReason`),
  the amount (`DeductionPay`) and its kind (`LineType`). One index change rides P2.1's regeneration.
- **No new contract field.** One new value of a field the cleaner apps already read, `PayLineType.DisputeCharge = 3`,
  rides P3.1's regeneration and mobile spec re-dump.

### Today

- `ResolveDispute` refuses the whole resolution, refund included, before any money moves when the cleaner's pay row
  on the order is missing, invoiced, already charged or smaller than the charge (`ResolveDispute.cs:134-142`,
  `OrderEmployeePay.cs:241-242`, `dispute.cleaner_charge_not_chargeable`). Otherwise it adds the charge to that row's
  `DeductionPay` with the dispute and the reason (`ResolveDispute.cs:197-200`, `OrderEmployeePay.cs:244-255`).
- For an invoiced job the only route is Adjust (`UpdateInvoiceAmounts.cs:13-17`): an invoice-level deduction with no
  dispute link, no reason the cleaner can read and no cap. That is D19's workaround, and a paid invoice has none.
- The framework contract (2026-10-27, §9) allows exactly this deduction: a complaint charge "is linked to the
  complaint, never exceeds your reward for that job, and you can read its reason in the app". §10 promises an invoice
  for every closed period and sets cash off "up to its total". The self-billing agreement (§3) lists "any bonus and
  deduction". No legal text changes.
- A pay row is unique per order and cleaner (`OrderEmployeePayEntityConfiguration.cs:132-133`). Two readers rely on
  it: `GetByOrderAndEmployeeAsync` takes the first row it finds (`OrderEmployeePayRepository.cs:9-17`), and
  `GetTotalPayByOrderIdsAsync` builds a dictionary keyed by order, which throws on a second row (`:19-33`, read by
  `GetDashboardStats.cs:170` and `GetPagedOrders.cs:200`).
- Both invoice writers go through `EmployeeInvoice.CreateFromOrderPays`: `GenerateInvoice.cs:152` for the safety net
  and the admin endpoint, and `PayPeriodBackgroundService.cs:418` for the night run and the wind-down. Its total is
  the sum of the rows' `TotalPay` (`EmployeeInvoice.cs:219-240`), floored at zero (`:121-126`).

### 1. Where the charge lives until the next invoice

When the job row is already invoiced, `ResolveDispute` adds a new pay row instead of refusing:

| Field | Value |
|---|---|
| `OrderId`, `EmployeeId`, `CurrencyId` | copied from the invoiced job row |
| `PayPeriodId` | the company's open period (`IPayPeriodRepository.GetActivePeriodAsync`) |
| `LineType` | `DisputeCharge` (3, new) |
| `BasePay` | 0 |
| `DeductionPay` | the charge |
| `TotalPay` | minus the charge |
| `DeductionDisputeId`, `DeductionReason` | the dispute and the reason, as on a direct charge |
| `EmployeeInvoiceId` | null until an invoice takes it |

- One domain factory builds the line from the job row, so the copied fields cannot drift. Only this line type has a
  negative `TotalPay`: `Create` and `RecomputeTotalPay` keep flooring every other row at zero, and no production code
  changes a row's amounts except `ChargeForDispute`.
- This is R2's rule for late pay: the charge joins the open period and is invoiced with it.
- The refusals stay before any money moves, under the same key: no pay row; the row already charged for a dispute; a
  carried charge already on that order for that cleaner; a charge above the job row's `TotalPay` (the contract's
  cap); and, new, no open period to carry it to. The rest of D19 stands: a refused charge still refuses the whole
  resolution.
- A job not yet invoiced is still charged directly, as today. `ChargeForDispute` keeps refusing an invoiced row.
- The cleaner sees the line at once in My Pay for the open period, with its order and reason, and the period's figure
  drops by the charge.

### 2. How the next invoice picks it up

- The night run (P1.3), the safety net, the admin endpoint and the wind-down close all read the closed period's
  un-invoiced rows (`GetUnassignedForEmployeePeriodAsync`) and group them by currency. The charge line is one of them.
- `SumPayAmounts` adds nothing to the subtotal and the charge to the deduction. `TotalAmount` still equals the sum of
  `TotalPay`, which the period-pay preview also shows.
- Example: job J1 was invoiced at 600 in period 1. A dispute on J1 is resolved in period 2 with a 150 charge. The
  cleaner earns 800 in period 2, so period 2's invoice reads subtotal 800, deduction 150, total 650.
- The PDF keeps listing jobs and fee shares only. A charge line would print as a 0.00 cleaning line
  (`FileExtensions.cs:98-99`, `DefaultInvoiceLayoutBuilder.cs:228`), so `CreatePdfData` skips it
  (`FileExtensions.cs:62`). The deduction shows in the summary, as a direct charge's does today. The apps list the
  line with its order and reason. `TotalOrders` keeps counting rows, so the line counts as one, as a fee share does.
- P2.2's Cancel frees the charge line with the other rows, and the corrected invoice takes it again: same total, same
  set-off.

### 3. When it would push the invoice below zero

- An invoice takes whole charges only, oldest first, each one that still fits. A charge that does not fit moves to the
  open period and waits for the next invoice. Nothing is lost, and no invoice goes below zero.
- Both writers run this step per currency, before payout references are claimed, through one helper on
  `EmployeeInvoice`. The move is a new `MoveToPeriod`, allowed only on an un-invoiced charge line.
  `CreateFromOrderPays` throws when its rows net below zero, as it already throws on mixed currencies, so a writer
  that skips the step fails loudly instead of flooring.
- The move commits with the invoice. When nothing is left to invoice, `GenerateInvoice` returns success with no
  invoice, so the pipeline commits the move (it commits nothing on a failure), and the night run commits it itself. A
  closed period therefore ends fully invoiced, and P2.5 can mark it paid.
- Examples, pay first, then charges oldest first:
  - 100 of pay and a 150 charge: an invoice of 100; the charge waits.
  - 1 000 of pay and charges of 600, 700 and 100: 600 fits (400 left), 700 does not, 100 does (300 left); an invoice
    of 300; the 700 waits.
  - No pay and a 150 charge: no invoice and no e-mail; the charge waits.
- The cleaner is paid the period's pay in full, and the charge comes off a later invoice. Q2 asks whether you would
  rather take part of it now.

### 4. Cash set-off

- No code changes. The charge sits inside `TotalAmount`, and `SetOffCash` takes cash up to that reduced total
  (`EmployeeInvoice.cs:246-255`). The charge comes off first and the cash second, as §10 says.
- Example: 800 of pay, a 150 charge and 1 000 of cash held. The total is 650, 650 is set off, nothing is transferred,
  and the cleaner still holds 350 for the next invoice or a handover. Without the charge, 800 would be set off and 200
  held.
- A charge that waits does not touch the set-off of the invoice it skipped.
- Adjust is unchanged. It pre-fills the invoice's current deduction, carried charges included
  (`admin-payroll-ops.component.ts:79`).

### 5. A cleaner with no next invoice

- The charge waits in the open period, and every close in which it does not fit moves it on, so it is never stranded
  in a closed period. It waits only for pay in its own currency.
- It stays visible: the cleaner sees it with its reason, and admins see it in the open period's un-invoiced count
  (P3.1, P3.2).
- While it waits, it blocks that cleaner's erasure: `GdprDeletionService.cs:244-248` counts un-invoiced rows
  (`gdpr.deletion_blocked_by_unsettled_pay`). No admin action can clear it. Q3.
- A wound-down company's last close opens no next period. A charge that does not fit stays un-invoiced on that closed
  period, and the archive refuses while it is there (`ArchiveCompany.cs:71`). If the cleaner had no other pay in that
  currency, the safety net also re-queues the pair every 5 minutes. Also Q3.

### 6. Idempotency per dispute

- A resolved dispute cannot be resolved again (`ResolveDispute.cs:116-122`).
- A second charge on the same order and cleaner is refused before money moves: `CanTakeDisputeCharge` checks the job
  row, and one new repository check finds a carried line.
- `GetForUpdateAsync` takes no lock and a dispute has no concurrency token, so a double click can run two resolves of
  one dispute past those checks. The refund, the credit and the points clawback are already keyed on the dispute; the
  charge line would be the one insert that doubles. A unique index on charge lines (section 7) makes the second commit
  fail instead.
- The writers stay idempotent per pair: the move commits with the invoice, so a second delivery finds nothing
  un-invoiced in that period.

### 7. Schema and contract: P2.1 or P3.1?

- **No new column.** The link to the dispute already exists (`OrderEmployeePay.cs:80-84`).
- **P2.1: one index change, in the one regeneration of Initial.**
  - The unique index on `OrderEmployeePays (OrderId, EmployeeId)` gets `WHERE "LineType" <> 3`, so today's guard on
    job and fee-share rows is unchanged.
  - A second, named unique index on the same columns gets `WHERE "LineType" = 3`: at most one carried charge per order
    and cleaner.
  - The filter names 3 before the enum does; P3.1 names it, with identical SQL. A Postgres case in P2.1 checks that a
    job row and a type-3 row coexist on one order and cleaner, and that a second of either is refused. So the
    regeneration is proven before it is spent.
- **P3.1: one enum value, no new field.**
  - `PayLineType.DisputeCharge = 3` is appended. The enum is `[SwaggerEnumAsInt]` and append-only.
  - Both cleaner apps decode `lineType` strictly (`PartnerPayrollModels.swift:160`), and an unknown value fails the
    whole response. So the apps must know the value before the server writes it.
  - Every client maps the value exhaustively: `PeriodPayContent.swift:178`, `PeriodPayScreen.kt:324-327`,
    `invoice-detail.models.ts:6-10` and `period-pay.models.ts:11-15`. Each fails to build until the value has a
    label. The mobile builds generate from the committed spec, so all four labels, five locales each, land in P3.1's
    commit.
  - Nothing writes the value until P4.4.
- The apps need nothing else: the line's order number, deduction, reason and total are existing fields.
- P3.1's current-period figure can go below zero while a charge waits. P3.4, P3.5 and P3.7 check that it prints with
  its minus sign.

### 8. What P4.4 rewrites

Backend:
- `OrderEmployeePay.cs:241-255`:
  - `CanTakeDisputeCharge` drops its invoice condition.
  - `ChargeForDispute` keeps refusing an invoiced row.
  - New: the charge-line factory, and `MoveToPeriod`.
- `ResolveDispute.cs:134-142` and `:197-200`: the direct or carried branch, and the new refusals. The handler gains
  `IPayPeriodRepository`, its tenth collaborator.
- `EmployeeInvoice.cs`: the fit helper, and the negative backstop in `CreateFromOrderPays`.
- `GenerateInvoice.cs` and `PayPeriodBackgroundService.GenerateInvoicesForEmployeeAsync`: the move step, per currency.
- `PayPeriodBackgroundService.SendEmployeePeriodClosedEmailAsync`: no "period closed" e-mail when every row of the
  cleaner moved on (D7). P1.3's loop reaches every cleaner with an un-invoiced row, a charge-only cleaner included,
  and today it sends a plain e-mail when invoicing yields no attachment.
- `OrderEmployeePayRepository.cs`:
  - `GetByOrderAndEmployeeAsync` and `GetTotalPayByOrderIdsAsync` read the job or fee-share row only.
  - New: an exists-check for a carried line.
- `FileExtensions.cs:62`: the PDF skips charge lines.

Admin web, wording only, five locales, no new key:
- `pages.disputes_management.resolve.charge.hint`: when this pay is already invoiced, the amount comes off their next
  invoice.
- `api.dispute.cleaner_charge_not_chargeable`: drop "already invoiced", and add "no open pay period". The key and its
  CONTRACT row are unchanged.

Other slices:
- P4.2 counts work, not a carried charge, when it picks the cleaners to remind.

Docs, in Phase 7's single pass. Each of these says an invoiced row is refused:
- `business-rules.md:983-996`
- `flows/cancellation-refund-dispute.md:563-567`
- `flows/pay-and-payouts.md:334`
- `admin-app/order-management.md:166-170`

Neither contract text changes.

### 9. Tests

Unit tests:
- `DisputeSettlementAndCleanerChargeTests`:
  - `:408` becomes "an invoiced job's charge is carried". The resolve succeeds, and one charge line lands in the open
    period: `DeductionPay` 150, `TotalPay` -150, the dispute, the reason, the job's currency, no invoice. The job row
    is unchanged (600, its invoice kept), the refund moves, and the dispute is Resolved.
  - New refusals, each with no money moved: no open period; a carried line already on the order; 650 against an
    invoiced 600.
  - `:390` stays as it is.
- Domain:
  - The factory sets the table's fields.
  - `MoveToPeriod` refuses an invoiced row and a job row.
  - `ChargeForDispute` still throws on an invoiced row.
  - `CreateFromOrderPays` over 800 of pay and a 150 charge gives 800 / 150 / 650, equal to the sum of `TotalPay`,
    and it throws when the rows net below zero.
  - The fit helper on section 3's examples.
- `GenerateInvoice`:
  - 100 of pay and a 150 charge: one invoice of 100, one reference claimed, the charge moved.
  - A charge alone: success, no invoice, no reference, the charge moved.
  - 800 of pay, a 150 charge and 1 000 of cash held: set-off 650, transfer 0, 350 still held.
- Night run (`PayPeriodRolloverTests`, `PayPeriodMixedCurrencyInvoicingTests`):
  - The same cases through the close.
  - A CZK charge leaves the EUR invoice alone.
  - A cleaner with only a charge gets no e-mail, and the move is committed.
- `GetPeriodPays`: the grand total nets the charge.
- `PayoutInvoicePdfDataTests`: no charge line, the lines sum to the subtotal, and the deduction includes the charge.
- P2.2's cancel test, extended: the corrected invoice takes the charge again.

Integration (Postgres):
- `GetByOrderAndEmployeeAsync` returns the job row, and `GetTotalPayByOrderIdsAsync` returns the job's pay without
  throwing.
- Resolve a charge on an invoiced job and close the period: the invoice deducts it.
- Close now a period whose only row for a cleaner is a charge: the safety net moves it.

Review:
- The money reviewer re-derives every example by hand, and shows each new test failing with the change reverted.
- No HostTests change, because no endpoint changes.

### Alternatives considered

- **The cash ledger.** It already carries what a cleaner owes from invoice to invoice. Rejected, because a charge is
  not cash held:
  - It would count against the cash float cap, which hides cash jobs.
  - It would prompt a request to hand cash over.
  - It would show under Cash I hold.
  - It would cut the transfer, not the reward, and §9 deducts from the reward.
  - Its one-entry-per-order index would collide with a cash order's collection.
- **A pending charge on the dispute, applied as an invoice-level deduction.** Rejected:
  - It needs four or five new columns.
  - Every writer, Cancel and the apps would need a new reader.
  - The invoice total would stop equalling the sum of its rows.
  - The cleaner could see the reason only through a new field.
- **Charging another un-invoiced job row.** That row often does not exist yet, the charge would show on an unrelated
  order, and the cap is per job.
- **Changing the original invoice while it is unpaid.** Changing an issued invoice is on the plan's Not-doing list.
  By hand, Cancel and reissue (P2.2) followed by a direct charge already does it.
- **Splitting a charge to bring an invoice to exactly zero.** It recovers more, sooner. It costs a split step and a
  second line per charge, and the charge-line index then also needs `AND "EmployeeInvoiceId" IS NULL`. Q2.

### Questions for Mike

1. Do you approve this design, so that P2.1 takes the index change and P3.1 the new line type before they start?
2. When the next invoice cannot take a charge whole, which do you want?
   - The cleaner is paid that period in full, and the charge waits for a later invoice (this design).
   - The invoice takes part of the charge now, down to zero, and carries the rest. This costs more code: a charge can
     split across two invoices.
3. A charge that later pay never covers blocks the cleaner's erasure, and a wound-down company's archive. Which do you
   want?
   - An admin write-off action: a new small slice, not in this plan.
   - The charge is dropped automatically at erasure or archive.
   - It stays blocked until you decide each case (this design, no code).

Next: with your answers, P2.1 and P3.1 take their parts before they start, and P4.4 is built in Phase 4.

## Done

PR 4 merged with green CI.

## Out of scope

The plan's "Not doing" list. Anything found outside these slices is reported to Mike, not fixed.

## Work record

- 2026-11-09 — Filed from the plan page. Not started. P4.4's design is due before Phase 2.
- 2026-11-09 — P4.4's design written under Design (P1.8): no new column, one index change for P2.1, one enum value for
  P3.1. Waiting for Mike's OK and his answers to its three questions before Phase 2 starts.
- 2026-11-09 — Mike: "Yup, I like the recommended variants. Go with them". Q1: the design is approved, so P2.1 takes
  the index change and P3.1 the new line type. Q2: a charge the next invoice cannot take whole waits whole for a later
  invoice. Q3: a charge later pay never covers stays blocked, and Mike decides each case; no code. Section 8 now also
  names `SendEmployeePeriodClosedEmailAsync`, which P1.3's loop reaches: P4.4 skips its e-mail when every row of the
  cleaner moved on (D7).
- 2026-11-10 — Mike answered "A" to folding mrc-1, F4, F5 and F6 into this phase; P4.5 is dropped. Phase 4 starts on `fix/pay-findings`, run with the lean workflow.
- 2026-11-10 — mrc-1 built: `EmployeeInvoice` carries the Postgres `xmin` token, and a commit that loses on it comes back from the unit of work as `payroll.invoice.changed_concurrently` (admin app, five locales; partner and customer specs say why they cannot reach it). Race test on real Postgres: a cancel that read the invoice before a mark paid committed is refused, and the paid invoice keeps its jobs and its single cash set-off. `Initial` regenerated once, `20261109143607` to `20261109234823`; MS-2's one owed DEV reset moves to the new id.
- 2026-11-10 — P4.4 built as the approved design: a charge on an invoiced job becomes a `DisputeCharge` line in the open period (`CarryDisputeCharge`), refused before any money moves when the job row is already charged, a line is already carried, the charge is above the job's pay, or no period is open. Both invoice writers take whole charges oldest first (`EmployeeInvoice.FitDisputeCharges`), move the rest to the open period (`MoveToPeriod`) and commit the move with the invoice or alone; `CreateFromOrderPays` throws below zero; a cleaner whose only rows wait gets no period-closed e-mail; the PDF skips the line; the job-row readers skip it. Admin hint and `api.dispute.cleaner_charge_not_chargeable` reworded in five locales. Postgres cases: the carried charge deducted by the next close and retaken after a cancel, the whole-charge wait, one charge per job under a double resolve, and the e-mail skip. No schema or contract change. Waits for the money review.
- 2026-11-10 — P4.1 built: a pay calculation the validator rejects logs at Error with the order, the employee and the
  reason key, so the alerting fires; a pay row that already exists is a redelivery and logs at Information. **Question
  for Mike, not built:** an admin override to Completed (`AdminOverrideOrderStatus`) does not pass through
  `CompleteOrder`, so it calculates no pay for the crew. The one exception is an override from In progress on an unpaid
  cash order, whose door debt queues the crew's pay. Should every override to Completed pay the crew? Today it stays as
  it is.
- 2026-11-10 — P4.2 built: the period-end reminder goes only to a cleaner with a job or fee-share pay row in the period;
  a carried dispute charge alone is not work.
- 2026-11-10 — P4.3 built: both invoice-PDF writers store under `PayPeriod.BlobFolder` (`yyyy-MM-dd_yyyy-MM-dd`, invariant
  culture); the period-closed and period-end e-mails write the period, its start and its end in the recipient's
  language, and `IEmailService` no longer takes a period label. Invoices already stored keep their old address, which
  the row holds; a regenerate writes the new one. The DTO `PayPeriodLabel` the apps receive is unchanged.
- 2026-11-10 — F5 built: Android's partner invoice list keeps its rows when the employee-id lookup fails and shows
  the generic error snackbar; with nothing loaded the empty list stays pullable. The list is never read without the
  caller's own id. A failed refresh already kept its rows. Unlike iOS there is no full-screen error with retry on a
  first-load failure; the pull is the retry.
- 2026-11-10 — F6 built: admin's Close now is offered on an open period only once its start date is today or earlier
  (UTC), the same check `ClosePayPeriod` makes on the server.
- 2026-11-10 — F4 built: partner web's Cash I hold text says the cash is set off when the next invoice is issued, in
  all five locales and in the mobile apps' words.
- 2026-11-10 — Review fixes. P4.1: the handler read the validation pipeline's generic sentinel instead of the refusal
  keys, so a redelivery logged at Error and every rejection logged "A validation problem occurred."; it now reads the
  validation errors (`AuditErrorCode.Resolve` for the reason), and its tests return the pipeline's real shape. mrc-1
  gains the swapped race (a mark paid losing to a cancel) and P4.4 a mixed-currency case; both passed before, as the
  code was already right. Not built, by proportionality: an admin action that commits between the night run's invoice
  commit and its PDF stamp makes the stamp lose on the token, leaving the invoice without its PDF until Regenerate PDF.
- 2026-11-10 — Defaults kept as PM, reported to Mike: admin overrides to Completed still do not log a rejected pay calculation (P4.1's open question), and R3 is declined (an admin action in the seconds between the night run's invoice insert and its PDF stamp leaves no PDF link; Regenerate PDF recovers it).
- 2026-11-10 — Shipped: #375 merged to master as 5625fa386. Migration 20261109234823_Initial; DEV gets it with the P6.2 reset (Mike's OK, 2026-11-10).
