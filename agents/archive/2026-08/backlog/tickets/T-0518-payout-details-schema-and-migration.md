---
id: T-0518
title: Partner payout details — schema, entity configuration and migration (CZ first)
status: done
size: M
owner: db
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0517]
blocks: [T-0519]
stories: []
adrs: [0034]
layers: [db]
security_touching: true
manual_steps: [ef-migration]
sprint: 15
---

## Context

The persistence half of **T-0517**'s ADR. Separate ticket because it carries an **owner-only EF
migration** and a **backfill of live DEV rows** — neither belongs in the same PR as validators and DTOs.

**Nothing here is designed by this ticket.** Shape, field set, nullability, the governing-country rule
and the backfill strategy all come from the ADR (AC1, AC2, AC4, AC8).

## Acceptance criteria

- [ ] **AC1 — the schema matches the ADR exactly, field for field, including nullability and lengths.**
      Any departure is a note in `## Review` citing the ADR line and the reason. Evidence: the entity/
      configuration plus the citation.
- [ ] **AC2 — `Employee.IBAN`'s fate is explicit.** Kept, renamed, moved to a child entity, or dropped
      — **and whichever it is, the existing DEV rows land somewhere valid.** Evidence: the mapping,
      including what happens to values that pass `Length(15,34)` and are not IBANs.
- [ ] **AC3 — multi-tenancy is honoured** per `CLAUDE.md` and matched to how `Employee` does it today.
      Evidence: the config compared against `EmployeeEntityConfiguration.cs`.
- [ ] **AC4 — the entity configuration follows the existing archetype.** Mirror
      `EmployeeEntityConfiguration.cs` and `CompanyInfoEntityConfiguration.cs` (`:30` is the
      `Iban` precedent — `HasMaxLength(50)`). Do not invent conventions. Evidence: the file plus the
      mirrored file named.
- [ ] **AC5 — encryption-at-rest per ADR AC7 is implemented or its explicit absence is recorded here
      too.** A decision recorded only in the ADR and not visible at the column is a decision that will
      be lost. Evidence: the column comment or the converter.
- [ ] **AC6 — the anonymization path still works.** `Employee.cs:262` sets
      `IBAN = AnonymizationMarker.Value`, and `EmployeeUserAuditCoverageTests.cs:301` asserts the IBAN
      does **not** appear in audit JSON. **New payout columns must be covered by both.** Evidence: the
      updated anonymization plus that test extended and green.
- [ ] **AC7 — the profile-completeness gate still means something.** `Employee.cs:283` and `:313` use
      `IBAN` for `hasEmployeeInfo` / `missingFields` (`"profile.fields.iban"`). **If the field changes
      shape, this gate and its i18n key change with it** — a cleaner blocked from taking orders by a
      completeness rule pointing at a field that no longer exists is a hard outage for them. Evidence:
      the updated gate plus the key.
- [ ] **AC8 — the migration is WRITTEN-UP AND FLAGGED, NOT RUN.** `manual_steps: ef-migration`,
      owner-only (`CLAUDE.md`). State the exact command and what it generates, **and confirm with the
      owner whether this project is still folding schema changes into the single `Initial` migration** —
      the instruction differs from a normal additive migration. Evidence: the flagged note plus the
      owner's confirmation before T-0519 starts.
- [ ] **AC9 — the backfill is a written script the owner runs, not code that runs on startup.**
      Evidence: the script plus its dry-run output description.
- [ ] **AC10 — the SECURITY gate runs.** `security_touching: true` — financial account identifiers at
      rest. Evidence: the security verdict.
- [ ] **AC11 — the suites are green.** `Cleansia.Tests` / `Cleansia.IntegrationTests` /
      `Cleansia.HostTests` **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **Validators, commands, DTOs** — **T-0519**.
- **Any client** — T-0520 / T-0521.
- **Running the migration or the backfill.** AC8 / AC9. Owner-only.
- **Designing the shape.** T-0517. If the ADR is ambiguous, this ticket stops and says so.

## Implementation notes

**Archetype:** `EmployeeEntityConfiguration.cs` and `CompanyInfoEntityConfiguration.cs`.

**AC6 and AC7 are the two that will bite.** The IBAN is not "unused" the way the sprint-15 ticket
assumed — it is read by the **profile-completeness gate** that decides whether a cleaner may take
orders (`Employee.cs:283`) and by the **GDPR export** (`GdprExportDto.cs:41`), and it is asserted
**absent** from audit JSON by an existing test. Three live couplings, none of them the payout document.

**Read first:** the T-0517 ADR, `Employee.cs:255-320`, `EmployeeUserAuditCoverageTests.cs`,
`GdprExportDto.cs`, and `agents/knowledge/patterns-backend.md`.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's 2026-08-02 bank-details answer).** Split from
  the backend leg so the **owner-only migration and the live-row backfill** are reviewable on their
  own. **The PM's finding, written into AC6/AC7:** `Employee.IBAN` has three couplings nobody listed —
  the profile-completeness gate at `Employee.cs:283`/`:313`, the GDPR export, and an audit test that
  asserts it never appears in audit JSON. Changing its shape without those is a cleaner locked out of
  the job board.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Shipped in `7e1cf7f5`. **Verified at HEAD:**
  `EmployeePayoutDetails` appears 13 times in `20260723182623_Initial.cs` and `Employee.HasPayoutDetails`
  once — the latter is the panel's own fix for *"a missing `.Include` 403s every cleaner"* (ADR-0034 §D7).
  The entity, its configuration and its repository all exist:
  `Core.Domain/Users/EmployeePayoutDetails.cs`, `Infra.Database/EntityConfigurations/
  EmployeePayoutDetailsEntityConfiguration.cs`, `Infra.Database/Repositories/
  EmployeePayoutDetailsRepository.cs`. `LegacyRawValue` was struck because the database is being dropped —
  the agent said so rather than building a population that will not exist.
- 2026-08-04 — **two judgement calls it flagged rather than made silently, both correct and both still
  open by design:** it did **not** flip `IsProfileComplete()` onto `HasPayoutDetails` (no writer existed at
  the time, so flipping it would have made every cleaner permanently incomplete — the exact failure
  ADR-0034 D7 exists to prevent), and it did **not** add the 4th `UpdateBenefits` parameter for the express
  quota. The first was subsequently satisfied by T-0519's write path.
- 2026-08-04 — ⚠️ same `ef-migration` caveat as T-0512: **the regenerated `Initial` will not apply to an
  already-migrated database.** Owner action required.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD: the migration, the entity, the entity
configuration and the repository. Commit `7e1cf7f5` records a new model-metadata test pinning
`.AreNullsDistinct(false)` on all four sole-arbiter indexes **using an unset index as a CONTROL proving the
assertion can fail** — without that control the option is one invisible builder call no SQLite test could
catch. **`manual_steps` OPEN for the owner: the database drop.**

