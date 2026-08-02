---
id: T-0522
title: Rebuild the payout invoice to the owner's CZ specimen — parties, VAT statement, bank block, dates, line items
status: blocked
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0508, T-0519]
blocks: [T-0523]
stories: []
adrs: []
layers: [db, backend]
security_touching: false
manual_steps: [ef-migration]
sprint: 15
---

## Context

The build behind **T-0508**'s specification. The owner supplied a real Czech ISDOC invoice as the spec;
T-0508 maps it onto the model; this ticket makes the platform emit it.

**The change is not "add fields to the PDF".** The current document has **Cleansia in the header as
issuer and the cleaner under "Billed To"** (`DefaultInvoiceLayoutBuilder.cs:29-31`, `:73-81`) —
**the two parties are the wrong way round relative to the specimen.** This ticket inverts that, which
is why it is `blocked` on the owner rather than merely dependent on a spec.

### Why it is `blocked`

Two owner answers, both `blocking: yes`:

- **`Q-PAYOUT-02`** — employee or OSVČ, and **who issues** the document. The specimen (cleaner as
  *Dodavatel*) points at self-billing, but self-billing has its own requirements including the
  supplier's prior agreement. **If the answer is "employee", this is a payslip and not this ticket at
  all.**
- **`Q-PAYOUT-03`** — the VAT-status branch: how does the platform know whether a cleaner is a *plátce
  DPH*, and what does each variant state?

**Guessing either produces a document of the wrong legal category, which no amount of field-adding
repairs.**

## Acceptance criteria

- [ ] **AC0 — `Q-PAYOUT-02` and `Q-PAYOUT-03` are answered.** The ticket does not move until both are
      in `answered.md`. Evidence: the answers.
- [ ] **AC1 — the parties run in the specimen's direction.** Supplier block = the cleaner (name /
      street / postcode + city / country / IČ / VAT statement); customer block = Cleansia (name /
      address / IČ / DIČ). Evidence: the rendered PDF beside the specimen.
- [ ] **AC2 — the VAT statement renders BOTH variants correctly.** A non-registered cleaner shows the
      *"Nejsme plátci DPH"*-equivalent; a registered one shows DIČ and the VAT lines. **A blank VAT
      field, or one that implies registration where there is none, is wrong in a way that matters.**
      Evidence: both variants rendered.
- [ ] **AC3 — the payment block carries the CLEANER's details, not Cleansia's.** Local account number
      in Czech form, IBAN, SWIFT — sourced from **T-0519**'s contract. **Today the only bank block on
      any of these documents is the company's** (`DefaultReceiptLayoutBuilder.cs:167-168`); shipping
      that on a payout invoice tells the cleaner to pay us. Evidence: the rendered block plus the
      source at file:line.
- [ ] **AC4 — variabilní symbol, konstantní symbol, payment method, amount due are all present.**
      **VS already exists and renders** (`EmployeeInvoice.cs:72`, `:331`, layout `:38-39`) — reuse it,
      and per the specimen **VS equals the invoice number**; state whether the existing
      `GenerateVariableSymbol(employeeId, payPeriodId)` satisfies that or must change. Evidence: the
      block plus the VS ruling.
- [ ] **AC5 — issue date AND due date are both present.** **The due date does not exist today** — no
      field on `EmployeeInvoice`, none on `InvoicePdfData`. The rule that computes it comes from T-0508
      AC7. Evidence: the field, the rule, and the rendered dates.
- [ ] **AC6 — line items match the specimen's shape:** description, quantity, unit, unit price, line
      total. `OrderLineItem` is a **pay breakdown** today (order no., date, base/extras/expenses/total)
      and cannot be relabelled into an invoice line. Implement T-0508 AC6's decision exactly. Evidence:
      the rendered table plus a worked example against a real pay period.
- [ ] **AC7 — "Celkem k úhradě" is the total, and it reconciles to the line items and to
      `EmployeeInvoice.TotalAmount`.** A document whose printed total disagrees with the stored amount
      is worse than no document. Evidence: a test asserting the equality.
- [ ] **AC8 — the late-payment notice renders from the existing mechanism.**
      `CountryInvoiceContext.LegalDisclaimerTemplate` → `data.LegalDisclaimer` is already wired
      (`FileExtensions.cs:61`, layout). **Its CONTENT is the owner's, not an agent's** — carry the text
      the specimen uses, attributed. Evidence: the rendered notice plus the attribution.
- [ ] **AC9 — the hardcoded `VatAmount = 0` is removed.** `FileExtensions.cs:48` hardcodes it, which is
      right for a non-payer **by accident**. Evidence: the diff plus a test for each VAT variant.
- [ ] **AC10 — the schema delta is written and the migration is FLAGGED, not run.**
      `manual_steps: ef-migration`, owner-only (`CLAUDE.md`). Evidence: the flag plus the owner's
      confirmation.
- [ ] **AC11 — Czech labels where the specimen has them**, via the existing i18n/country-context path
      rather than hardcoded strings. The current layout hardcodes English (*"Billed To"*, *"Payment
      Period"*, *"Invoice"*, *"Variable Symbol"*). **Note `T-0506`: the same document is emailed in a
      language the cleaner may not read** — do not solve that here, but do not make it worse. Evidence:
      the label source.
- [ ] **AC12 — the existing layout-builder seam is used, not bypassed.** `LayoutBuilderFactory` +
      `IInvoiceLayoutBuilder` + `DefaultInvoiceLayoutBuilder` already support per-country layouts via
      `CountryInvoiceContext`. **A CZ-specific layout is what that seam is for.** Evidence: the
      implementation plus the seam citation.
- [ ] **AC13 — already-issued invoices are handled per T-0508 AC10's ruling.** Evidence: the
      implementation of whichever ruling landed.
- [ ] **AC14 — a test that goes red against the pre-change code (Gate 0.5 leg 1)** — e.g. asserting the
      supplier block contains the cleaner's IČ. Re-run **un-cached** by the verifier.
- [ ] **AC15 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The QR Platba code and the barcode** — **T-0523**, deliberately separate: it needs an encoder
  dependency and a format spec, and it must not hold up a correct document.
- **The payout-details schema and validation** — T-0517 / T-0518 / T-0519.
- **SK.** CZ first, per the owner. T-0508 AC11.
- **The customer receipt.** Different document, different regime.
- **The delivery email's language** — **T-0506**.
- **Renaming `EmployeeInvoice`.** The name is the two models collided; **if `Q-PAYOUT-02` says
  "employee", the rename is the least of it.** Name it, do not do it.
- **Running the migration.** AC10.

## Implementation notes

**Sequenced after T-0519** because AC3 needs the cleaner's bank fields to exist. **Sequenced after
T-0508** because everything else needs the mapping.

**If T-0508 AC12 sizes this an `L`, it must be split before it goes `ready`** — the likely seam is
(a) parties + identity + dates, (b) the line-item reshape. **The PM will not let an `L` run.**

**Read first:** the T-0508 spec, `DefaultInvoiceLayoutBuilder.cs` in full, `InvoicePdfData.cs`,
`FileExtensions.cs:28-90`, `EmployeeInvoice.cs`, `LayoutBuilderFactory.cs`, `CountryInvoiceContext`,
and the owner's specimen annotation from T-0508 AC1.

## Status log
- 2026-08-02 — **draft → `blocked` (created by pm from the owner's 2026-08-02 invoice specimen).**
  Filed as the build behind T-0508's spec. **`blocked` on `Q-PAYOUT-02` and the new `Q-PAYOUT-03`**,
  both `blocking: yes` — the direction of the document and the VAT branch are legal-category questions,
  and the PM's own grounding found the current PDF runs in the **opposite direction** from the owner's
  specimen, which is exactly the failure mode those questions guard against. **Two claims corrected
  against the sprint-15 filing:** the variable symbol **is** present and rendered, and IČ/DIČ **already
  exist** as validated columns — so the gap is narrower than "no IČ, no VAT, no bank details, no
  variable symbol" and sharper: wrong parties, wrong bank, no due date, wrong line-item shape.

## Review
