---
id: T-0517
title: ADR — partner payout details, CZ first and built to extend (shape, country rule, encryption, migration)
status: done
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: [T-0518]
stories: []
adrs: [0034]
layers: [architect, db, backend]
security_touching: true
manual_steps: []
sprint: 15
---

> **ARCHITECT PANEL REQUIRED (author + 2–3 challengers + lead) — `agents/process/deliberation.md`.**
> **The owner explicitly asked for this panel before any code.** Deliverable is an ADR + the living
> decision doc. `git diff --stat -- src/` must be empty.

## Context

**Owner decision, 2026-08-02:** *"Let's start with CZ but in a way that's easy to expand in the future
— like Bank Account, Card number, and whatever else is needed to make a payment to the employee."*

That is a **shape** instruction, not a field list. It closes the "which countries" half of the question
sprint-15 §4 posed (*"IBAN is European… this decides whether the stored field is an IBAN or bank
details with a scheme discriminator"*) — **the answer is: bank details, CZ shapes first, more later** —
and opens the design question this ADR answers.

### What exists today, PM-verified first-hand at `master` 2026-08-02

| Thing | State |
|---|---|
| `Employee.IBAN` | **one nullable string**, `Employee.cs:24`, no `MaxLength` attribute (`EmployeeEntityConfiguration.cs:30` sets it) |
| Its validation | `ValidationExtensions.cs:122-130` — `NotEmpty()` + **`Length(15, 34)`**. **No checksum. No country rule.** A 20-character sentence passes |
| Its write path | `UpdateBankDetails.cs` (self-service, one `Iban` field), `UpdateEmployee.cs:127`, `AdminUpdateEmployee.cs:73` |
| Its readers | **`Employee.cs:283` (profile-completeness gate) and `GdprExportDto.cs:41` — and nothing else.** It is **not** on the invoice: `FileExtensions.cs:38-42` sends only name, a flattened address string and email |
| `CompanyInfo` — the richer shape that already exists | `bankName`, `bankAccountNumber`, `iban`, `swift` (`CompanyInfo.cs:67`, `UpdateCompanyInfo.cs:154`, rendered at `DefaultReceiptLayoutBuilder.cs:167-168`) |
| Per-country field machinery that already exists | `CountryConfiguration` carries `RegistrationNumberLabel/Format/Required` and `VatNumberLabel/Format/Required` (`:43-57`) — **and `TaxIdLabel`/`TaxIdFormat`. There is no bank equivalent** |
| Per-country **validation** machinery that already exists | `ITaxIdValidator` → `TaxIdValidator` with `ValidateRegistrationNumberAsync` / `ValidateVatNumberAsync`, consumed by `UpdateIdentificationInfo.cs:73-96`. **This is the archetype for a country-aware bank validator** |
| Employee already has the identity fields | `RegistrationNumber` (IČO), `VatNumber` (DIČ), `LegalEntityName` — `Employee.cs:15-23`. **They exist; they are simply not on the invoice** |

### The decision space the panel must resolve

1. **Shape.** Three candidates, and the trade-off is real:
   - **(a) more columns on `Employee`** — mirrors `CompanyInfo` exactly, cheapest, and **caps us at one
     payout method per cleaner**;
   - **(b) an `EmployeePayoutMethod` child collection** with a scheme discriminator — extends to
     SEPA/SWIFT/local-clearing/PSP-token without a migration each time, costs a join and a "which one
     is primary" rule;
   - **(c) country-keyed config only** — `CountryConfiguration` grows bank label/format/required and
     the value stays one string. Cheapest to extend, worst to *use* (you cannot render a Czech account
     number's parts from an opaque string).
2. **Which country governs the format.** The **cleaner's** country (their bank), the **tenant's**, or
   the **order's**? `Employee.Address` exists; `CountryConfiguration` is keyed by country. **These
   diverge for a Slovak cleaner working in CZ, which is a real case in this market.**
3. **Does `CountryConfiguration` grow bank fields?** It already does exactly this for registration and
   VAT numbers. Consistency says yes; **the counter-argument is that a bank *scheme* is not a
   per-country label, it is a per-scheme structure**, and conflating them is how the VAT-label pattern
   would be misapplied.
4. **Encryption at rest.** Financial account identifiers. Column-level encryption, the platform's
   existing approach (if any), or an explicit "the database is the boundary" with the reasoning
   written down. **AC-level, not a hand-wave.**
5. **The migration for existing rows.** `Employee.IBAN` has values in DEV today. Whatever the shape,
   the existing strings must land somewhere valid — including the ones that pass `Length(15,34)` and
   are not IBANs.

### The one thing that is already decided and is not open

**Card numbers are NOT a column.** The owner's phrasing mentions "Card number"; a PAN is a **tokenised
PSP object**, never a column in this database. The ADR records this as a constraint, names the
mechanism if card payouts are ever wanted (a Stripe Connect / PSP payout token reference — an id, not
a number), and **the panel does not relitigate it.** Storing a PAN would put this platform in PCI-DSS
scope, which is a business decision several orders of magnitude larger than a payout field.

## Acceptance criteria

- [ ] **AC1 — the shape is chosen from (a)/(b)/(c) (or a fourth, argued) with the alternatives and
      why-not recorded.** The deciding question is stated explicitly: **what does adding the second
      country cost under each option?** Evidence: the decision plus the alternatives table.
- [ ] **AC2 — the CZ field set is enumerated exactly**, including the local account-number form
      (`prefix-number/bankcode`, e.g. `5885638003/5500`), IBAN, SWIFT/BIC and bank name — and **which
      of them are required vs optional for a CZ payout**. Evidence: the field table.
- [ ] **AC3 — "easy to expand" is made concrete, not asserted.** Show what adding **SK** and then a
      **non-IBAN market** costs under the chosen shape: how many migrations, how many code changes,
      how many client changes. **An ADR that claims extensibility without pricing the second country
      does not pass this AC.** Evidence: the worked example.
- [ ] **AC4 — the governing-country rule is decided** (cleaner's / tenant's / order's) with the
      SK-cleaner-working-in-CZ case worked through. Evidence: the rule plus the worked case.
- [ ] **AC5 — the `CountryConfiguration` question is answered either way, with the VAT/registration
      precedent cited.** If yes, the new properties are named; if no, the reason it differs from the
      `RegistrationNumberLabel/Format/Required` precedent is stated. Evidence: the ruling at file:line.
- [ ] **AC6 — the validation contract is specified and it is REAL.** IBAN carries an ISO 13616 mod-97
      checksum; the CZ local account number carries a mod-11 weighted check. **`Length(15,34)` is not
      validation** — a typo here is a transfer to a stranger or a failed payroll run. State whether
      validation extends `ITaxIdValidator`'s per-country pattern or gets its own service. Evidence:
      the contract plus the archetype citation.
- [ ] **AC7 — encryption at rest is DECIDED, with the platform's current posture stated first.** What
      does this repo do today for sensitive columns? The answer may legitimately be "nothing, and here
      is why that is acceptable for an account number that appears on an invoice the cleaner receives"
      — **but it must be written down, not omitted.** Evidence: the decision plus the current-state
      finding.
- [ ] **AC8 — the migration path for existing `Employee.IBAN` rows is specified**, including rows that
      pass `Length(15,34)` and are **not** valid IBANs. Backfill, or park-and-revalidate, or ask the
      cleaner again. **`manual_steps: ef-migration` is flagged for T-0518; the ADR does not run it.**
      Evidence: the path.
- [ ] **AC9 — the invoice's needs are satisfied by the shape.** **T-0522 renders the cleaner's bank
      block on the payout document** — local account number, IBAN, SWIFT, all three visible on the
      owner's own specimen invoice. A shape that cannot produce that block has failed before it ships.
      **Coordinate with T-0508's field spec; do not let the two answer the same question differently.**
      Evidence: the cross-check against T-0508.
- [ ] **AC10 — "no PAN column" is recorded as a constraint with its reasoning** (PCI-DSS scope), plus
      the named mechanism if card payouts are ever wanted. Evidence: the constraint section.
- [ ] **AC11 — the generality question is answered consistently with T-0511 AC5.** Both ADRs land in
      the same week and both ask *"one general table or one specific set of columns?"*. **Two opposite
      answers by accident is a worse outcome than either answer.** Evidence: the cross-reference.
- [ ] **AC12 — the ADR is written to `docs/decisions/00NN-*.md` and the living decision doc under
      `agents/architecture/decisions/` is updated in the same step.** Evidence: both files.
- [ ] **AC13 — the deliberation trail (`## Challenge` / `## Defense` / `## Verdict`) stays in the
      artifact**, and a challenger that finds nothing names what it checked. Evidence: the sections.
- [ ] **AC14 — the implementation is split into sized tickets that already exist** (T-0518 db, T-0519
      backend, T-0520 web/admin, T-0521 mobile). **If the ADR's shape makes any of them an `L`, the ADR
      splits it** rather than leaving the PM to discover it. Evidence: the sizing statement.
- [ ] **AC15 (Gate 0.5 leg 3)** — state what the panel did not examine and which claims are reads
      rather than runs. **No agent asserts a banking or PCI legal requirement** — cite the standard or
      attribute it to the owner.

## Out of scope

- **Writing the entity, the migration, the validators or any UI** — T-0518 / T-0519 / T-0520 / T-0521.
- **Storing card numbers.** AC10 — decided, closed.
- **Building a payout/transfer integration.** Moving money is a different epic. This ADR shapes the
  **data the invoice needs**, not a payment rail.
- **What the invoice must legally contain** — **T-0508**. AC9 is the seam between them; the ADR does
  not decide invoice law.
- **The IBAN's current exposure in logs / GDPR export** — **T-0509**, dispatchable independently.

## Implementation notes

**`architect`-led. One challenger should come at it from the `security` angle** (AC7, AC10) and one
from the `db` angle (AC1, AC8) — the shape decision and the encryption decision fail in different ways.

**Read first:** `Employee.cs:1-40` + `:130-180` + `:255-320`, `EmployeeEntityConfiguration.cs`,
`ValidationExtensions.cs:122-130`, `UpdateBankDetails.cs` (in full — it is 65 lines and it is the
extension point), `CompanyInfo.cs:60-110`, `CountryConfiguration.cs` (in full),
`ITaxIdValidator.cs` + `TaxIdValidator.cs`, `UpdateIdentificationInfo.cs:60-100`,
`DefaultReceiptLayoutBuilder.cs:160-175`, `docs/architecture/security-rules.md` (S1–S10), and
`CLAUDE.md`'s note that `Address.State` is retained *"for US/CA when we launch there"* — the roadmap
signal that makes AC3 load-bearing.

## Status log
- 2026-08-02 — **draft → `ready` (created by pm from the owner's 2026-08-02 answer: *"start with CZ but
  in a way that's easy to expand"*).** **Filed as an architect panel at the owner's explicit
  instruction — no code before it.** The PM ground-truthed the whole area first-hand and the picture is
  better than sprint-15 assumed in one way and worse in another: **better**, because `Employee` already
  carries `RegistrationNumber`/`VatNumber`/`LegalEntityName` and the platform already has per-country
  label/format/required machinery plus an `ITaxIdValidator` to copy; **worse**, because the IBAN's only
  validation is `Length(15, 34)` — a 20-character sentence is a valid IBAN to this platform today.
  `ready`: passes DoR, no unmet dependency, panel is step 1.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). The deliverable is **ADR-0034**
  (`docs/decisions/adr-0034.md`), drafted `b855d758`, challenged by two lanes
  in `eee24957`, **accepted `7fc2935e`** with 8 blocking findings folded in. **Verified at HEAD:** the ADR
  file's header reads `- **Status:** \`accepted\` — **2026-08-02, by panel verdict.**` The panel changed the
  design rather than ratifying it: D1's first stated reason is struck, the completeness gate reads a
  column (`Employee.HasPayoutDetails`) instead of a hand-written include list, erasure became an id-keyed
  set-based write, and the CZ mod-11 **direction** defect was corrected in the ADR text before any
  implementer could copy it.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read the ADR header at HEAD and confirmed the
`accepted` status line plus the named challenger lanes. This is a deliberation ticket: consensus IS the
acceptance criterion, and `agents/process/deliberation.md` is satisfied (author + 2 challengers + lead
verdict). Downstream implementation is tracked separately (T-0518/T-0519/T-0520/T-0521). **No
`manual_steps` on this ticket.**

