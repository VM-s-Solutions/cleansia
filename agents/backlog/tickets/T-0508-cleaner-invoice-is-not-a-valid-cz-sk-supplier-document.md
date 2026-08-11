---
id: T-0508
title: SPEC — map the owner's real CZ invoice onto the platform's data model, field by field
status: superseded
size: M
owner: pm
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: [T-0522]
stories: []
adrs: []
layers: [analyst, backend]
security_touching: false
manual_steps: []
sprint: 15
---

> **REWRITTEN 2026-08-02 — the owner supplied the specification.** The original ticket was `blocked`
> on `Q-PAYOUT-01` (*"what must a CZ/SK supplier invoice legally contain? Not guessable; needs an
> accountant"*), and its own AC said *"what would help most: the field list, plus **one real example**
> of an invoice your accountant accepts."* **The owner sent exactly that** — a photo of a Czech ISDOC
> invoice they issued themselves. **`Q-PAYOUT-01` is answered for CZ.** The ticket moves from `blocked`
> to `ready` and becomes the spec that maps the specimen onto the model.
> **`depends_on: [T-0504]` removed** — the specimen supplies what that panel's decisions 4/5/6 were to
> supply for this document.

## Context

### The specification, as the owner supplied it

A Czech invoice (ISDOC), issued by the owner, containing:

| Block | Contents |
|---|---|
| **Dodavatel** (supplier — **this is the cleaner**) | name, street, postcode + city, country, **IČ**, and a **VAT statement**. The specimen reads *"Nejsme plátci DPH"* (not VAT registered). A VAT-registered supplier shows **DIČ** instead |
| **Kontaktní údaje** | e-mail, telephone |
| **Odběratel** (customer — **this is Cleansia**) | name, address, **IČ**, **DIČ** |
| **Header** | **Faktura** number (top right) + a **barcode** |
| **Dates** | **Datum vystavení** (issue) and **Datum splatnosti** (due) |
| **Payment block** | bank account in **Czech local format** (`5885638003/5500`), **IBAN**, **SWIFT**, **variabilní symbol** (= the invoice number), **konstantní symbol**, payment method, amount due, and a **QR Platba+F** code |
| **Line items** | description, quantity, unit, unit price, line total |
| **Footer** | late-payment interest notice |
| **Total** | **Celkem k úhradě** |

### What the platform emits today — PM-verified first-hand at `master` 2026-08-02

**The headline finding, and it is not "missing fields":**

> **The current document runs in the OPPOSITE DIRECTION from the owner's specimen.**
> `DefaultInvoiceLayoutBuilder.cs:29-31` puts **CLEANSIA** in the gradient header as the issuer, and
> `:73-81` puts the **cleaner under "Billed To"** — name, email, a flattened address string. **The
> platform currently issues a document that says Cleansia is the supplier and the cleaner is the
> customer.** The owner's specimen is the reverse. **No amount of adding fields fixes a document whose
> two parties are the wrong way round**, which is precisely why `Q-PAYOUT-02` (employee vs OSVČ; who
> issues) is still open and still blocking the *build*.

Field by field against the specimen:

| Specimen block | Platform today | Verdict |
|---|---|---|
| Supplier = cleaner, with IČ + VAT statement | Cleaner is `EmployeeName` / `EmployeeEmail` / `EmployeeAddress` under **"Billed To"** (`:79-81`). `FileExtensions.cs:38-42` sends only those three | **direction inverted; IČ and VAT statement absent from the PDF** |
| Supplier's IČ / DIČ | **The columns EXIST**: `Employee.RegistrationNumber`, `Employee.VatNumber`, `Employee.LegalEntityName` (`Employee.cs:15-23`), captured by `UpdateIdentificationInfo.cs` with a **real per-country validator** (`ITaxIdValidator`) | **data exists, is simply not on the document** — this is much cheaper than the investigation assumed |
| Customer = Cleansia with IČ + DIČ | `CompanyInfo` has `RegistrationNumber` + `VatNumber` and they are on `CompanyInfoData` (`InvoicePdfData.cs:37-38`) | **data exists** |
| Supplier's bank block | The PDF's bank fields are **Cleansia's** (`CompanyInfoData.BankName/BankAccountNumber/Iban/Swift`, rendered on the **receipt** layout at `DefaultReceiptLayoutBuilder.cs:167-168`). The **cleaner's** bank details are **not on `InvoicePdfData` at all** | **the wrong party's bank details** — and the cleaner's shape is **T-0517**'s ADR |
| **Variabilní symbol** | **PRESENT.** `EmployeeInvoice.VariableSymbol` (`:72`), `GenerateVariableSymbol(employeeId, payPeriodId)` (`:331`), rendered at `DefaultInvoiceLayoutBuilder.cs:38-39`. `PaymentReference` defaults to the invoice number (`:126`) | ✅ **the "no variable symbol" claim is WRONG — correct it wherever it is repeated** |
| Invoice number | `EmployeeInvoice.InvoiceNumber` + `GenerateInvoiceNumber(prefix "EMP")` (`:321`), rendered top-right | ✅ present. **Gaplessness/sequence is AC5** |
| Issue date | `GeneratedAt`, rendered as "Date" | ✅ present, needs the Czech label |
| **Due date** (Datum splatnosti) | **ABSENT.** No due-date field on `EmployeeInvoice`, none on `InvoicePdfData` | ❌ |
| **Konstantní symbol**, payment method | **ABSENT** | ❌ |
| Line items (description, qty, unit, unit price, total) | `OrderLineItem` = order number, date, base/extras/expenses/total (`:52-60`). **There is no quantity, no unit and no unit price** — it is a pay breakdown, not an invoice line | ❌ **shape mismatch, not a missing column** |
| **QR Platba +F**, barcode | **ABSENT** | ❌ → **T-0523** |
| Late-payment interest notice | `LegalDisclaimer` from `CountryInvoiceContext.LegalDisclaimerTemplate` — a **generic** slot that exists | ⚠️ mechanism present, content unverified |
| VAT | `VatAmount = 0` **hardcoded** (`FileExtensions.cs:48`) | ⚠️ correct for a non-VAT-payer **by accident**, wrong the moment a cleaner registers |

### What is still owner-blocked, and it is now only two things

1. **`Q-PAYOUT-02` — employee or OSVČ, and who issues the document.** The specimen has the cleaner as
   supplier, which points at **OSVČ + self-billing** (the platform issuing on the cleaner's behalf) —
   **and self-billing carries its own requirements including the supplier's prior agreement.** The
   entity is still named `EmployeeInvoice`, the two models' names collided into one.
2. **`Q-PAYOUT-03` (new) — the VAT-status branch.** The specimen says *"Nejsme plátci DPH"*. A
   VAT-registered cleaner needs DIČ and VAT lines instead. **How does the platform know which a cleaner
   is?** `Employee.VatNumber` is nullable — is "null" the answer, or is a cleaner's VAT status a thing
   we must ask and record?

**Neither blocks this ticket.** They block **T-0522** (the build). This ticket produces the spec that
makes both questions answerable in one sitting.

## Acceptance criteria

- [ ] **AC1 — the current document is RENDERED and annotated against the specimen.** A **PDF a human
      can look at**, not a field list, saved under `agents/backlog/attachments/`. Annotate every block
      the specimen has. **Lead with the direction inversion** — that is the finding the owner needs to
      see, and it makes `Q-PAYOUT-02` concrete instead of abstract. Evidence: the rendered file plus
      the annotation.
- [ ] **AC2 — a field-by-field mapping table: specimen block → platform source → gap.** Extend the
      table in the Context above; **verify every row rather than inheriting it**, and correct the PM
      where wrong. Evidence: the completed table.
- [ ] **AC3 — the party-direction question is stated as a one-line decision for the owner.** *"Who
      issues this document, and therefore whose name goes in the header?"* — with the two consequences
      spelled out (self-billing obligations vs the cleaner issuing to us, which means the platform
      generates a **draft** the cleaner adopts). Evidence: the sentence, filed into `Q-PAYOUT-02`.
- [ ] **AC4 — the VAT-status branch is specified for BOTH cases**, and it names how the platform
      determines which. Both variants must be describable well enough for T-0522 to render them.
      Evidence: the two variants plus the determination rule → filed as `Q-PAYOUT-03`.
- [ ] **AC5 — numbering is examined against the legal requirement and against the existing generator.**
      `GenerateInvoiceNumber(prefix "EMP")` and `GenerateVariableSymbol(employeeId, payPeriodId)` —
      read them and state whether the result is sequential and **gapless**, and whether it must be.
      **Do not assume the receipt regime answers it:** `Q-REFUND-01` established CZ/SK are
      `FiscalEnforcementMode.None` **for receipts**; whether supplier invoices sit in the same regime is
      a separate question. Evidence: the generator analysis plus the regime check at
      `CountryConfiguration.cs:65-71`.
- [ ] **AC6 — the line-item shape gap is specified.** The specimen wants description / quantity / unit
      / unit price / line total; `OrderLineItem` carries a **pay breakdown** (base/extras/expenses).
      **Decide what one line represents** — one cleaning job? one pay component? — and how quantity and
      unit price are derived. **This is the single largest piece of design in the whole invoice change
      and it is the one most likely to be waved through.** Evidence: the line-item specification with a
      worked example from a real pay period.
- [ ] **AC7 — the missing fields' capture paths are named.** Due date (a policy — issue + N days? whose
      N?), konstantní symbol (a constant per payment type?), payment method. **A required field with no
      capture point is a document that still cannot be issued.** Evidence: the capture table.
- [ ] **AC8 — the bank block's source is the T-0517 ADR, and the two are cross-checked.** The specimen
      needs local account number **and** IBAN **and** SWIFT on one document. **If T-0517's shape cannot
      produce that block, say so on both tickets before either builds.** Evidence: the cross-check.
- [ ] **AC9 — the schema delta is SPECIFIED and the migration is FLAGGED, not run.** Due date and any
      other new column. **Do not carry `manual_steps: ef-migration` on this ticket** — it produces a
      spec and no schema; the flag belongs on T-0522 (or a db ticket split from it) so it is attached
      to the change that actually needs the owner. Evidence: the spec plus the named ticket.
- [ ] **AC10 — the already-issued documents are addressed.** How many `EmployeeInvoice` rows exist? Are
      they reissued, superseded, or left? **If real cleaners have been paid against them, say so** —
      that is a fact for the owner, not a technical detail. Evidence: the count plus the ruling.
- [ ] **AC11 — SK is explicitly OUT and said so.** The owner ruled **CZ first**. `Q-PAYOUT-01` stays
      open for SK. **Naming the boundary is part of the spec**, so nobody later reads a CZ document as
      a CZ/SK one. Evidence: the statement.
- [ ] **AC12 — the size of T-0522 is stated after the mapping.** If the direction inversion plus the
      line-item reshape makes it an `L`, **this ticket splits it** rather than leaving the PM to
      discover it mid-flight. Evidence: the sizing.
- [ ] **AC13 — `git diff --stat -- src/` is empty.** This ticket produces a specification and a rendered
      sample. It builds nothing.
- [ ] **AC14 (Gate 0.5 leg 3)** — **every legal claim is attributed.** The specimen is the owner's;
      inferences from it are the analyst's and are labelled as such. **No agent asserts a tax-law
      requirement.**

## Out of scope

- **Building the document** — **T-0522**.
- **The QR Platba code and the barcode** — **T-0523**.
- **The payout-details schema** — **T-0517** / **T-0518** / **T-0519**. AC8 is the seam.
- **SK.** AC11.
- **The customer-facing receipt.** A different document under a different regime (ADR-0004,
  `Q-REFUND-01`). **Do not conflate them** — note that the receipt layout is where the *company's*
  bank block already renders, which is easy to mistake for the invoice's.
- **The email that delivers it, and its language** — **T-0506**.
- **Giving legal advice.** The specimen is evidence of what the owner's accountant accepts; it is not
  a statute. AC14.

## Implementation notes

**`analyst`-owned with a `backend` instance for AC1/AC2/AC5's code archaeology** (rendering the current
PDF and reading the two generators). **No panel of its own:** the owner supplied the specification, so
there is no story to defend — but **AC6's line-item decision should be challenged by one reviewer
before it reaches T-0522**, because it is a design choice wearing a mapping's clothes.

**Read first:** `Cleansia.Infra.Services/Pdf/Layouts/DefaultInvoiceLayoutBuilder.cs` **in full**,
`Pdf/Models/InvoicePdfData.cs`, `Core.AppServices/Extensions/FileExtensions.cs:28-90`,
`Core.Domain/EmployeePayroll/EmployeeInvoice.cs` (especially `:60-130`, `:200-230`, `:315-340`),
`PayPeriod`, `CompanyInfo.cs`, `CountryConfiguration.cs:65-71`, `Employee.cs:1-40`, the T-0517 ADR,
and `Q-REFUND-01`'s answer in `questions/answered.md`.

## Status log
- 2026-08-02 — **draft → `blocked` immediately (created by pm from the partner-onboarding
  investigation).** Two legal inputs no agent may answer; `Q-PAYOUT-01` + `Q-PAYOUT-02` filed
  `blocking: yes`.
- 2026-08-02 — **REWRITTEN → `ready`. The owner supplied the real invoice**, which is exactly what the
  original ticket asked for (*"one real example of an invoice your accountant accepts"*).
  **`Q-PAYOUT-01` is answered for CZ**; SK stays open and is carved out by AC11. **`depends_on:
  [T-0504]` removed.** The PM verified the whole document path first-hand and found **three things the
  investigation had wrong**: (1) **the direction is inverted** — the current PDF makes Cleansia the
  supplier and the cleaner the "Billed To" (`DefaultInvoiceLayoutBuilder.cs:29-31`, `:73-81`), which is
  a wrong-legal-category problem, not a missing-field one; (2) **the variable symbol IS present** and
  rendered (`EmployeeInvoice.cs:72`, `:331`, layout `:38-39`) — the "no variable symbol" claim is
  false; (3) **IČ/DIČ already exist as columns** with a real per-country validator
  (`Employee.cs:15-23`, `ITaxIdValidator`), so the identity half is a rendering job, not a capture
  job. **The remaining owner blockers are down to two** — `Q-PAYOUT-02` (direction/self-billing) and
  the new `Q-PAYOUT-03` (VAT-status branch) — and **both block T-0522, not this ticket.**

- 2026-08-05 — **`ready` → `superseded` by **T-0522** (PM reconciliation pass 4).** Per
  `ticket-lifecycle.md`, `superseded` is for a ticket whose **question** was answered by other shipped
  work — which is exactly what happened here. T-0508 was the **spec**; T-0522 was the **build**; the build
  ran first, answered the spec's questions in the course of shipping, and is now `in_review` with
  **AC0–AC15 all checked**. Producing the spec now would document a thing that already exists.
  **Mapped AC by AC, against the tree — not against T-0522's ticket text:**
  - **AC3 (the party-direction decision) → answered by the owner and shipped.** `Q-PAYOUT-02`:
    *"he's not an employee but signs a B2B contract with us and all of the invoices will be generated by us
    but actually invoiced for employees"* = **self-billing**. At HEAD `InvoicePdfData.Supplier` is an
    `InvoiceSupplierData` (the cleaner) and `Company` is the `Odběratel` — the file's own comment says so —
    and `InvoiceLabels` carries `Supplier = "Dodavatel"` / `Customer = "Odběratel"`. The inversion T-0508
    was written to surface is gone.
  - **AC4 (the VAT branch) → answered and shipped as a rule, not a hardcode.** `Q-PAYOUT-03`: the cleaner is
    a non-payer. `InvoiceSupplierData.IsVatPayer` exists and `QuestPdfService.cs:80` computes
    `context.VatWithinGross(data.TotalAmount, data.Supplier.IsVatPayer)`, so a registered cleaner stays a
    **data** change. The old hardcoded `VatAmount = 0` is gone.
  - **AC6 (the line-item shape — "the single largest piece of design") → decided and shipped.**
    `Pdf/Models/InvoiceLineItem.cs` now carries `Quantity` / `UnitPrice` / `LineTotal` beside
    `OrderNumber` / `PerformedOn`. It is an invoice line, not a pay breakdown.
  - **AC7 (capture paths for the missing fields) → all three exist.** `InvoicePdfData.DueDate` (derived from
    the immutable issue date, so no column), `ConstantSymbol` (on `CountryInvoiceConfig`), payment block.
  - **AC5 (numbering), AC8 (the bank block vs T-0517), AC9 (the schema delta)** → settled inside T-0522
    (VS stays its own property per the owner's ruling that it must **not** equal the invoice number; the
    supplier bank block is sourced from `EmployeePayoutDetails`; the delta is one nullable `varchar(4)`,
    **flagged**).
  - **AC1/AC2 (render + annotate the OLD document against the specimen)** → **moot.** The document they
    would annotate no longer exists.
  - **AC10 (what happens to already-issued invoices)** → **moot by the database drop**, and T-0522's AC13
    carries whatever ruling applies afterwards.
  - **AC11 (SK is explicitly out) → the boundary survives outside this ticket**, which is the only reason
    superseding it loses nothing: `Q-PAYOUT-01` is recorded in `questions/open.md` as *answered for CZ,
    still open for SK*. **Nobody may read the shipped document as a CZ/SK document.**
  - **AC12/AC13 (size T-0522, empty `src/` diff)** → moot; T-0522 is built and is an `M`.
  **T-0508's `blocks: [T-0522]` is discharged.** T-0522 is **not** closed by this and is **not** unblocked
  by this — it is `in_review` and holds a real `manual_steps: ef-migration`.

## Review
