---
id: T-0512
title: Membership benefit usage — entity, configuration and migration
status: done
size: S
owner: db
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0511]
blocks: [T-0493]
stories: []
adrs: [0035]
layers: [db]
security_touching: false
manual_steps: [ef-migration]
sprint: 15
---

## Context

The persistence half of **T-0511**'s ADR. Filed separately because it carries an **owner-only EF
migration** and must not be bundled into a ticket that also changes pricing — a migration and a money
change in one PR is a review nobody can do well.

**Nothing here is designed by this ticket.** The shape, the period key, the uniqueness constraint and
the reversal semantics all come from T-0511. If the ADR concludes *"derive it from `Order` rows, no
new table"* (T-0511 AC10 forces that alternative to be argued), **this ticket closes with an empty
diff and that is a successful outcome.**

## Acceptance criteria

- [ ] **AC1 — the entity matches the ADR exactly, field for field.** Any departure is a note in
      `## Review` naming the ADR line it departs from and why. Evidence: the entity plus the citation.
- [ ] **AC2 — the uniqueness/concurrency guarantee from T-0511 AC4 is expressed in the DB, not only in
      code.** A quota enforced solely by a `SELECT` then an `INSERT` is not enforced. Evidence: the
      index/constraint in the entity configuration.
- [ ] **AC3 — multi-tenancy is honoured.** `TenantId` + the global query filter, per the platform rule
      in `CLAUDE.md`, matched to how `UserMembership` does it. A benefit counter that leaks across
      tenants is a billing defect. Evidence: the config, compared against the sibling entity.
- [ ] **AC4 — the entity configuration follows the existing archetype.** Mirror
      `MembershipPlanEntityConfiguration.cs` / the `UserMembership` configuration; do not invent
      conventions. Evidence: the file, with the mirrored file named.
- [ ] **AC5 — the migration is WRITTEN-UP AND FLAGGED, NOT RUN.** `manual_steps: ef-migration`, owner
      only (`CLAUDE.md` — Claude never runs `dotnet ef migrations add`/`database update`). State the
      exact command the owner runs and what it will generate. Evidence: the flagged note plus the
      owner's confirmation before T-0493 starts.
- [ ] **AC6 — existing rows need no backfill, or the backfill is specified.** A brand-new table needs
      none; say so explicitly rather than leaving it unsaid. If the ADR put a column on an existing
      table, the default for existing rows is stated. Evidence: the statement.
- [ ] **AC7 — the solution builds and the suites are green.** `Cleansia.Tests` /
      `Cleansia.IntegrationTests` / `Cleansia.HostTests` run **locally**, baselines
      **2295 / 108 / 75**.

## Out of scope

- **The pricing change and the consumption call** — **T-0493**.
- **Running the migration.** AC5. Owner-only.
- **Any client** — T-0513 / T-0514.
- **Designing the shape.** T-0511. If this ticket finds the ADR ambiguous, it stops and says so rather
  than deciding.

## Implementation notes

**Archetype:** `src/Cleansia.Infra.Database/EntityConfigurations/MembershipPlanEntityConfiguration.cs`
and the `UserMembership` configuration alongside it.

**Read first:** the T-0511 ADR, `agents/knowledge/patterns-backend.md` (entity + configuration
conventions), and the `Initial` migration note in the user's memory — schema changes on this project
have historically been folded into the single `Initial` migration; **confirm with the owner which mode
applies before writing the migration note**, because the instruction differs from a normal additive
migration.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's 2026-08-02 express-upgrade answer).** Split out
  of T-0493 so the **owner-only migration** is not bundled with a money-path change. `depends_on:
  [T-0511]` — this ticket writes what the ADR decides and designs nothing itself. **An empty diff is a
  legitimate close** if T-0511 AC10 rules that the count is derivable from `Order` rows.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Shipped in `7e1cf7f5` *"feat(db): all six pending
  schema changes, folded into one regenerated Initial"*. **Verified at HEAD:** `MembershipBenefitUsage`
  appears 20 times in `src/Cleansia.Infra.Database/Migrations/20260723182623_Initial.cs`, alongside
  `UserMembership.TrialEndsAtUtc` (ADR-0035 AM-18, the owner's trial ruling). The filtered partial unique
  slot index carries `WHERE "IsActive" = TRUE` and the emitted DDL carries `NULLS NOT DISTINCT`. There is
  still exactly **one** migration in the repo — the owner authorised regenerating `Initial` rather than
  stacking four.
- 2026-08-04 — ⚠️ **`manual_steps: [ef-migration]` is DISCHARGED-BUT-NOT-APPLIED.** The migration exists
  and the timestamp was preserved, so `20260723182623_Initial` is **already in `__EFMigrationsHistory`** on
  any migrated environment. `MigrationService/Program.cs:31` reads `GetPendingMigrationsAsync()` and `:39`
  calls `MigrateAsync()` — pending only — so the in-place column additions are **skipped silently** and the
  service exits 0. **This schema is not real on DEV until the owner drops the database.** See the owner
  list in `status/sprint-15.md § ADDENDUM C`.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD: `20260723182623_Initial.cs` (grep counts
per entity), `MigrationService/Program.cs:25-45`. Commit `7e1cf7f5` records integration 117/117 (was 60
passing / 57 failing on the old Initial, every failure a missing column) and unit 2594/2594. **Both test
fixtures build fresh schemas, so those numbers prove nothing about a deployed database** — ADR-0040's
challenger (`44d1b64d` CH-W3) established that explicitly. **`manual_steps` OPEN for the owner: the
database drop.**

