---
id: T-0156
title: "Outbox table + EF entity config + migration flag (per T-0155 schema)"
status: done
size: S
owner: db
created: 2026-06-05
updated: 2026-06-07
depends_on: [T-0155]
blocks: [T-0157]
stories: []
adrs: [0002, 0008]
layers: [db]
security_touching: false
manual_steps: [ef-migration]
sprint: 1
source: split of T-0143 (child b); ADR-0002 D5
---

## Context
Split child **(b)** of L-epic **T-0143**. Builds the outbox table + EF Core entity configuration to the
schema frozen by T-0155 (AC2), and flags the EF migration the owner must apply. The durable backing +
drainer (T-0157) cannot land until the table exists. **Strictly serial** in the a→b→c→d chain (same
dispatch/pipeline cluster).

## Acceptance criteria
- [ ] **AC1 (entity + EF config)** — Given the T-0155 schema, When this child lands, Then the outbox
  entity and its EF `IEntityTypeConfiguration` exist in `Cleansia.Infra.Database`, with the
  `(QueueName, MessageKey)` **uniqueness** (realizing ADR-0002 D1.1), `Body`, `TenantId`, the
  created/claimed/dispatched timestamps, attempt count, and status columns as the ADR specifies.
- [ ] **AC2 (migration flag)** — Given the new table (plus `ProcessedMessage`/`DeadLetter` as the ADR
  requires), When the owner is handed the `manual_steps`, Then a `MANUAL_STEP: ef-migration` note names
  exactly the schema objects added so the owner can regenerate/apply. Claude does NOT run `dotnet ef`.
- [ ] **AC3 (no churn)** — No Bucket-A handler, no consumer, and no `IPendingDispatch` call site changes
  in this child — it adds the table + config only. Verified by diff.

## Out of scope
- The `IPendingDispatch` durable backing, the drainer, and the D1.3 host wiring — that is T-0157.
- The Bucket-B migration onto the outbox row — that is T-0158.
- The outbox-table ADR (T-0155) — this child consumes it.

## Implementation notes
- **Gated on T-0155 accepted AND the owner confirming the migration applied** before T-0157 starts.
- **Serialization cluster:** part of the `UnitOfWorkPipelineBehavior.cs` + queue cluster (TICKET-MAP
  row 3, F11 → F2/SEC-W1 → F4 → F3 cross-wave continuation) — must NOT run concurrently with any other
  cluster member; strictly serial within the T-0143 chain.
- Anchors: `Cleansia.Infra.Database` entity configs; `QueueNames.cs:5-9`;
  `UnitOfWorkPipelineBehavior.cs:19-20` (the commit the row rides on).

## Status log
- 2026-06-05 — draft (created by pm; split of T-0143 child b; blocked on T-0155 ADR)
- 2026-06-06 — ready (Batch 1B; gate **ADR-0008 / T-0155 done ✓**; `adrs` updated to `[0002,0008]` (ADR-0008
  D1 = the `OutboxMessages` schema this implements). Head of the strictly-serial outbox chain. Routed to db,
  reviewer in parallel. **Owner manual_step: ef-migration** (the `OutboxMessages` table + the UNIQUE
  (QueueName, MessageKey) index + the partial pending index) — flagged; **T-0157 is held until the owner
  confirms the migration applied.** No NSwag (internal contract)).
- 2026-06-06 — db: entity + EF config + DbSet + EF-model metadata test landed; `dotnet build` green
  (Core.Domain + Infra.Database 0 errors), the two OutboxMessage metadata tests pass over SQLite.
  `ProcessedMessage`/`DeadLetter` not touched — `DeadLetter` already exists from Wave-0; `ProcessedMessage`
  is a separate ticket's table and is out of scope here (table + config only, AC3). Awaiting owner ef-migration.

## MANUAL_STEP: ef-migration (owner-only — do NOT let an agent run `dotnet ef`)

Generate and apply ONE migration for the new `OutboxMessages` table. New code added in this child:
`OutboxMessage` entity (`Cleansia.Core.Domain/Outbox/OutboxMessage.cs`), `OutboxMessageStatus` enum,
`OutboxMessageEntityConfiguration`, and `DbSet<OutboxMessage> OutboxMessages` on `CleansiaDbContext`.

Schema objects the migration must create (and ONLY these — no existing object changes, all additive):

- **Table `"OutboxMessages"`** with columns:
  - `Id` text (max 26, PK), `TenantId` text (max 26, nullable), `CreatedBy` text (max 255, not null),
    `CreatedOn` timestamptz (not null), `UpdatedBy`/`UpdatedOn`/`DeactivatedBy`/`DeactivatedOn` (nullable,
    from the `Auditable` base), `IsActive` boolean — all inherited via `AuditableEntityConfiguration`.
  - `QueueName` text (max 128, not null), `MessageKey` text (max 256, not null), `Body` text (not null),
    `Status` integer (not null), `AttemptCount` integer (not null), `ClaimedOn` timestamptz (nullable),
    `ClaimedBy` text (max 128, nullable), `DispatchedOn` timestamptz (nullable),
    `NextAttemptAt` timestamptz (nullable), `LastError` text (nullable).
- **`IX_OutboxMessages_TenantId`** — non-unique, on `TenantId` (from the `Auditable` base).
- **`IX_OutboxMessages_QueueName_MessageKey`** — **UNIQUE** on `(QueueName, MessageKey)`. This realizes the
  in-request idempotency (a double-enqueue with the same key collapses to one row). `TenantId` is
  deliberately NOT part of this key (the `MessageKey` already embeds a globally-unique id; same reasoned
  exception as `IX_OrderReceipts_OrderId`).
- **`IX_OutboxMessages_NextAttemptAt_Pending`** — partial index on `NextAttemptAt` with predicate
  `WHERE "Status" = 0` (Pending), so the drainer's claim query never scans dispatched/failed rows.

The table is new and empty, so the migration is purely additive: no backfill, no non-nullable-without-default
column added to an existing table, no rename, no drop. The unique/partial indexes are small on a fresh table;
`CREATE INDEX CONCURRENTLY` is not required for the initial creation (it would be relevant only for adding an
index to an already-large table later). After applying, confirm so T-0157 (durable backing + drainer) can start.
- 2026-06-07 — done (PM reconciliation: Wave-1 Batch 1B merged to master in a4f14094 / PR #73 chain; status corrected from ready/draft to done; reviewer+security gates were satisfied in the merged PR per sprint-3 closeout).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
