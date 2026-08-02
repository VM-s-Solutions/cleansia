---
id: T-0508
title: LEGAL — the cleaner's invoice has no IČO, no VAT and no bank details; it is not a valid CZ/SK supplier document
status: blocked
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0504]
blocks: [T-0509]
stories: []
adrs: []
layers: [analyst, db, backend]
security_touching: false
manual_steps: [ef-migration]
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"The cleaner's invoice carries no
IČO, no VAT, no bank details — it is not a valid CZ/SK supplier document and cannot be used to pay
them."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Why this is `blocked` on the owner from the moment it is filed

**Two of the required inputs cannot be guessed by anyone in this repository:**

1. **What a CZ/SK supplier invoice must legally contain.** The candidate list — IČO, DIČ/VAT (and the
   *not* VAT-registered case, which is the common one for a cleaner), bank account, variable symbol,
   issue date, taxable-supply date, sequential numbering, the supplier's and customer's legal names
   and addresses — is a plausible list, **and a plausible list is exactly what must not be shipped
   on a tax document.** This is a question for the owner's accountant, not for an analyst.

2. **Is the cleaner an employee or a self-employed supplier (OSVČ / živnostník)?** This determines
   **who issues the document**. If the cleaner is a supplier, either they invoice the platform or the
   platform issues a **self-billing** document on their behalf — and self-billing has its own legal
   requirements including the supplier's prior agreement. If the cleaner is an employee, this is not
   an invoice at all; it is a payslip, with entirely different content and entirely different law.
   **The current artifact is called `EmployeeInvoice`, which is the two models' names collided into
   one and is itself a signal that this was never decided.**

**Building against a guess here does not produce a slightly-wrong document. It produces a document
of the wrong legal category.** No amount of adding fields fixes that.

### The consequence, stated plainly

**A cleaner cannot be paid against this document today.** Whatever the platform generates, an
accountant cannot book it and a bank transfer has no reference to key on. The pay-period machinery
(`PayPeriod`, `EmployeeInvoice`, the PDF generation, the emails) is **built and running** — and its
output is not usable for its one purpose. **T-0506 compounds it:** that document is emailed in a
language the cleaner may not read and cannot change.

### And a related dead end, filed separately

**The IBAN collected at onboarding has no downstream consumer** — **T-0509**. It is stored and never
read. **T-0509 `depends_on` this ticket**, because "where should the IBAN be consumed" is answered by
"what must the payment document contain".

## Acceptance criteria

- [ ] **AC0 — the owner answers the two legal questions.** Filed as **`Q-PAYOUT-01`** (what a CZ/SK
      supplier invoice must contain) and **`Q-PAYOUT-02`** (employee or OSVČ; who issues the
      document), both `blocking: yes`, `resolve-by: pre-prod`. **The ticket does not move until both
      are answered.** The story frames them precisely enough to take to an accountant.
- [ ] **AC1 — RE-ESTABLISH the finding.** Produce a **rendered sample** of the current document and
      annotate what is present and what is absent against AC0's answer. **A rendered PDF, not a field
      list** — the question is what a human receives. Evidence: the sample plus the annotation.
      **This is dispatchable NOW, before AC0**, and it halves the remaining work.
- [ ] **AC2 — the data model gap is enumerated.** For each legally required field: does the platform
      hold it, and if not, where would it be captured? IČO and DIČ are onboarding fields that
      **probably do not exist**; the bank details exist as an unused IBAN (T-0509). Evidence: the
      field-by-field table.
- [ ] **AC3 — the capture path is specified for every missing field.** A required field with no
      capture point is a document that still cannot be issued. **This will extend partner onboarding**
      — coordinate with T-0504's rewrite evaluation so the fields land in one flow change, not two.
      Evidence: the specification.
- [ ] **AC4 — the not-VAT-registered case is handled explicitly.** Most individual cleaners in CZ/SK
      are not VAT-registered. A document with an empty VAT field, or one that implies registration
      where there is none, is wrong in a way that matters. Evidence: both variants rendered.
- [ ] **AC5 — numbering is specified and is gapless if the answer requires it.** Sequential invoice
      numbering is a common legal requirement and it interacts with the **existing fiscal
      machinery**: `Q-REFUND-01`'s answer established that this platform already models per-country
      fiscal regimes (`None` / `AsyncBackground` / `BlockingOnline`) and that CZ/SK are `None` for
      *receipts*. **Whether supplier invoices are in the same regime is a separate question and must
      not be assumed from the receipt answer.** Evidence: the numbering rule plus the regime check.
- [ ] **AC6 — the schema change is SPECIFIED, and the migration is FLAGGED, not run.**
      `manual_steps: ef-migration`, owner-only. Evidence: the spec plus the owner's confirmation.
- [ ] **AC7 — the existing issued documents are addressed.** Invoices already generated are wrong.
      Are they reissued, superseded, or left? **If real cleaners have been paid against them, say
      so** — that is a fact the owner needs, not a technical detail. Evidence: the count plus the
      ruling.
- [ ] **AC8 — the size is re-checked after AC0.** If the answer is "these are payslips, not
      invoices", this is **not an `M`** — it is a different document and a different feature, and the
      correct output is a re-file. Evidence: the sizing statement.
- [ ] **AC9 (Gate 0.5 leg 3)** — state which legal claims came from the owner and which from an
      agent. **No agent asserts a legal requirement.**

## Out of scope

- **Giving legal or tax advice.** The ticket frames the question; the owner answers it. Every legal
  statement in the final artifact is attributed to the owner.
- **The IBAN's consumer** — **T-0509**, which depends on this.
- **The email's language** — T-0506. Same email, different defect.
- **Running the migration.** AC6.
- **The customer-facing receipt.** A different document under a different regime (ADR-0004 /
  `Q-REFUND-01`). Do not conflate them.

## Implementation notes

**`analyst`-owned, `blocked`.** The panel is **T-0504**, whose decisions 4, 5 and 6 are this ticket's
inputs and which is explicitly barred from defaulting them.

**AC1 is dispatchable today and the rest is not.** Send an instance to render and annotate the current
document while the owner's answers are outstanding. It costs little and it makes the owner's question
concrete — *"here is what your cleaners currently receive"* is a far better prompt than an abstract
question about invoice law.

**Read first:** `Core.Domain` `EmployeeInvoice` + `PayPeriod`, the QuestPDF generation in
`Cleansia.Infra.Services`, the pay-period email templates, and `Q-REFUND-01`'s answer in
`questions/open.md` for how per-country fiscal regimes are already modelled.

## Status log
- 2026-08-02 — **draft → `blocked` immediately (created by pm from the partner-onboarding
  investigation).** Finding marked RELAYED. **Filed `blocked` rather than `draft`** because two
  inputs — CZ/SK invoice contents, and employee-vs-OSVČ — are legal questions no agent may answer, and
  guessing them produces a document of the **wrong legal category** rather than a slightly wrong one.
  **The PM's own observation, added to the ticket:** the artifact is named `EmployeeInvoice`, which is
  the two competing models' names collided into one — evidence that the question was never settled.
  `Q-PAYOUT-01` and `Q-PAYOUT-02` filed `blocking: yes`. **AC1 carved out as dispatchable today** so
  the owner's question arrives with a rendered sample attached.

## Review
