---
id: T-0512
title: Membership benefit usage — entity, configuration and migration
status: draft
size: S
owner: db
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0511]
blocks: [T-0493]
stories: []
adrs: []
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

## Review
