---
id: T-0758
title: TenantId is a real foreign key on every stamped table and leaves Auditable
status: done
size: M
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0722]
blocks: [T-0757, T-0759]
stories: []
adrs: [ADR-0061]
layers: [backend, db]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-TENANCY-04**: *"Do the recommended step"* — the foreign key to
`Tenants` on every stamped table, and the column off the 21 tenantless tables. ADR-0061 D1 had
declined the FK on 2026-09-13 (*"a tenant id enters the system at exactly two points, both already
bound to the registry"*) and D8 had left the dead `Auditable.TenantId` on tenantless tables as a
finding. The two-points premise held for production and not for the tests: eleven Postgres tests
stamped rows with ad-hoc ids (`tenant-A`, `some-tenant`, `tenant-payroll-A`, …) no registry held, and
T-0759 was about to add a third writer.

## Doing

- **`TenantAuditable : Auditable, ITenantEntity`** carries `string? TenantId`; `Auditable` carries
  none (`grep -n TenantId src/Cleansia.Core.Domain/Common/Auditable.cs` is empty). Every stamped type
  moved to it; the 21 tenantless types (`Countries` … `Services`) stay plain `Auditable` and lose the
  column and `IX_<T>_TenantId`.
- `TenantAuditableEntityConfiguration<T, string>` (where `TenantIdNullableTypes` now lives) maps the
  column (NOT NULL bar `OutboxMessage` / `DeadLetter`), **`HasOne<Tenant>().WithMany().HasForeignKey
  (e => e.TenantId).OnDelete(DeleteBehavior.Restrict)`** (no navigation) and the index; the two
  `BaseEntity + ITenantEntity` audits (`AdminActionAudit`, `CustomerActionAudit`) map the same by hand.
  Stamp and filter untouched in behaviour.
- Tests: the eleven ad-hoc-id Postgres tests use `TestTenants` / `HostTestTenants` or insert their ids
  into `Tenants` in arrange; `TenantIdRequiredModelTests` gains "every `ITenantEntity` is FK'd to
  `Tenants` with Restrict" and "no non-`ITenantEntity` type has a `TenantId` property";
  `InitialMigrationTenantDdlTests` reads the migration's own operations (one FK per stamped table,
  2 nullable, no `IX_<tenantless>_TenantId`, counts pinned by hand); `TenantForeignKeyEnforcedTests`
  — a stamped row under an unknown company fails `23503` naming `FK_Users_Tenants_TenantId`;
  `SeededDatabaseHasNoOrphanTenantRowsTests` still green (the seed inserts `Tenants` first).
- Every stale "single-tenant mode is `TenantId = null`" comment rewritten (`0751c055`); the D9
  rationale lives once behind a pointer.
- `Initial` regenerated → **`20260915172310`** (final id, after T-0757 folded in). The DEV drop is
  owed at deploy (MS-2), never on the branch.

**Exact numbers:** 47 `FK_<T>_Tenants_TenantId` at this commit, **48** after T-0757 made
`PayoutReferenceCounter` stamped (46 `TenantAuditable` + 2 audits; 46 NOT NULL + 2 nullable);
21 `IX_<tenantless>_TenantId` removed; 46 `IX_<T>_TenantId` remain (the audits carry composite
indexes of their own).

## NOT

No change to the stamp/filter rule, the system-job override idiom, or any handler.

## Status log

- 2026-09-15 — shipped in `43157f62` (T-0758) and `0751c055` (comments). Recorded in ADR-0061 D1/D8
  as amended (and the D14, Alternatives (j), Findings and verification notes), CLAUDE.md landmine 1,
  S8, `/domain/model`, `/domain/roles/tenant`, `/flows/cross-cutting`, `consistency.md`,
  `patterns-backend.md`, the tenancy living note, MS-2, CHANGELOG (Changed).
