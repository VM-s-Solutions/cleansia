---
id: T-0282
title: AdminActionAudit entity + EF config (TenantId + global filter + 4 indexes) + migration
status: done
size: M
owner: db
created: 2026-06-22
updated: 2026-06-23
depends_on: []
blocks: [T-0283, T-0285]
stories: []
adrs: [0012]
layers: [db, backend]
security_touching: false
manual_steps: [ef-migration]
sprint: 10
---

## Context

ADR-0012 (accepted, 2026-06-22) — the admin action audit log. This is **piece 1 of 5** and the
**spine**: the `AdminActionAudit` table is what every other Wave-9 audit ticket writes to or reads
from, so it lands FIRST/ALONE on the schema. Greenfield — `Grep AuditLog|ActionLog|AdminActivity|
AuditTrail|ActivityLog` over `.cs` returns zero files (no collision).

This ticket adds **only** the entity, its EF configuration, and flags the owner migration. No behavior,
no read surface — those are T-0283 / T-0285. `security_touching: false` here because the schema change
alone exposes no endpoint/authz/DTO; the security gate fires on the *behavior* (T-0283/T-0284) and the
*query+policy* (T-0285).

## Acceptance criteria

- [ ] **AC1 — Entity exists with the frozen ADR-0012 D1 shape.** `AdminActionAudit : BaseEntity,
  ITenantEntity` (NOT `Auditable`) in `Cleansia.Core.Domain/Auditing/AdminActionAudit.cs` with **init-only**
  setters and exactly the D1 fields: `TenantId?`, `ActorId`, `ActorEmail?`, `ActorProfile` (UserProfile),
  `Action`, `ResourceType?`, `ResourceId?`, `Success`, `ErrorCode?`, `OccurredOn` (DateTimeOffset),
  `Reason?`, `BeforeJson?` (jsonb), `AfterJson?` (jsonb), `CorrelationId?`.
- [ ] **AC2 — Append-only by construction.** Every settable property is `init`-only (no public setter);
  the entity has no mutation method. Reviewer check #3 (no `Modified`/`Deleted` path) is satisfiable
  against this type.
- [ ] **AC3 — Config explicitly configures TenantId + global query filter (the D1 caveat).**
  `AdminActionAuditConfiguration : BaseEntityConfiguration<AdminActionAudit, string>` **explicitly**
  configures `TenantId` (`HasMaxLength(26)`, `IsRequired(false)`) and registers the entity for the
  EF global query filter — copied from `AuditableEntityConfiguration`, NOT inherited from
  `BaseEntityConfiguration` (which configures only the key). `BeforeJson`/`AfterJson` mapped as **jsonb**.
  Mirrors `RefundEntityConfiguration` for `.ToTable`/`.HasMaxLength`/`.HasIndex`.
- [ ] **AC4 — The 4 D6 indexes are configured.** `(TenantId, OccurredOn DESC)` (paged feed),
  `(ResourceType, ResourceId)` (per-resource history), `(ActorId, OccurredOn DESC)` (per-actor history),
  `(Action, OccurredOn DESC)` (per-action filter). Verified in the config (and will land in the migration).
- [ ] **AC5 — Migration flagged owner-only, NOT run.** The schema delta (new `AdminActionAudit` table +
  the 4 indexes) is described for the owner; `manual_steps: [ef-migration]`. The agents do **not** run
  `dotnet ef`. This ticket does not reach `done` until the owner confirms the migration is applied
  (per quality-gates §owner-only), and it is part of the Wave-9 manual-steps bundle.
- [ ] **AC6 — Config registered + builds.** `AdminActionAuditConfiguration` is discovered/registered like
  the other `IEntityTypeConfiguration`s; `dotnet build` succeeds. (Integration/host suites that assert no
  pending model changes run **after** the owner migration — bundled.)

## Out of scope
- The behavior, `IAuditContext`, the failure sink (T-0283), the snapshots (T-0284), the query + view
  policy (T-0285), the admin UI (T-0286). This ticket is entity + config + migration-flag only.
- Any retention/cleanup logic — D6 default is append-only, no auto-delete; the cleanup-Function MUST NOT
  touch these rows (the retention-prune ticket T-0288 explicitly excludes the audit table).

## Implementation notes
Read ADR-0012 **D1** (entity), **D1 caveat** (BaseEntity does not configure TenantId — only the key;
copy the TenantId lines out of `AuditableEntityConfiguration`, `EntityConfiguration.cs:20-51`), and
**D6** (storage/indexing/append-only). Follow `RefundEntityConfiguration` as the config archetype.
Current migration snapshot is `Migrations/20260620160737_Initial.cs`. **TDD per the ADR test list:**
the config/append-only assertions belong here (TC-AUDIT-ATOMIC's "exactly one row" and the append-only
reviewer check #3 are exercised by the consumers, but the entity-shape + tenant-filter + index config
tests are written test-first against this ticket). Owner-only: **ef-migration** (the table + 4 indexes).

## Status log
- 2026-06-22 — draft → ready (created by pm). Wave-9 piece 1/5 (ADR-0012 D1/D6). FIRST/ALONE on the
  schema — `blocks: [T-0283, T-0285]`. DoR: AC observable; sized **M**; deps none; `manual_steps:
  [ef-migration]` (table + 4 indexes — Wave-9 bundle); `security_touching: false` (schema only — the
  gate fires on the behavior/query); archetype = `RefundEntityConfiguration` (config) + `Auditable`-vs-
  `BaseEntity` choice frozen in D1. No panel (ADR-0012 is the accepted decision; this is its consumer).
- 2026-06-23 — ready → review (db). Implemented entity `AdminActionAudit : BaseEntity, ITenantEntity`
  (sealed, init-only, no mutators) in `Cleansia.Core.Domain/Auditing/AdminActionAudit.cs` with the exact
  D1 field set; `AdminActionAuditConfiguration : BaseEntityConfiguration<…>` explicitly maps `TenantId`
  (`HasMaxLength(26)`, `IsRequired(false)`), `BeforeJson`/`AfterJson` as `jsonb`, and the four D6 indexes
  (`(TenantId,OccurredOn DESC)`, `(ResourceType,ResourceId)`, `(ActorId,OccurredOn DESC)`,
  `(Action,OccurredOn DESC)`). The global query filter is the generic `ITenantEntity` one in
  `CleansiaDbContext.ApplyTenantQueryFilters` (verified present on the entity type). `DbSet` registered
  on `CleansiaDbContext`; config auto-discovered via `ApplyConfigurationsFromAssembly`. **AC1–AC4, AC6
  met.** `Cleansia.Infra.Database` (and transitively `Core.Domain`) build with 0 errors. TDD:
  `Cleansia.Tests/Features/Auditing/AdminActionAuditModelMetadataTests.cs` — 27/27 green (entity shape /
  append-only init-only / sealed / no-mutator / table / tenant-filter / nullable jsonb / required cols /
  exactly-4-indexes). **AC5 — migration NOT run (owner-only):** `AdminActionAudit` is absent from every
  migration and the model snapshot (current snapshot `20260620160737_Initial.cs`) → genuinely pending;
  `manual_step: ef-migration` stands (CREATE TABLE `AdminActionAudits` + the 4 indexes). Ticket does not
  reach `done` until the owner applies it and the bundled IntegrationTests/HostTests pending-model check
  runs green post-migration. Note for the orchestrator: an untracked **other-lane** file
  (`src/Cleansia.Tests/Dispatch/PruneOutboxHandlerTests.cs`, T-0287) currently fails to compile against
  `CleansiaDbContext` (references a not-yet-added `ProcessedMessages` DbSet) — **not this ticket**; my
  tests were run with that one file excluded via a build-scoped targets file (no source touched).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
