---
id: T-0143
title: "[SPLIT] Full transactional outbox: outbox table + dispatcher + post-commit drain across 5 queues — epic, see children"
status: done
size: L
split_into: [T-0155, T-0156, T-0157, T-0158]
owner: â€”
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0118, T-0148]
blocks: []
stories: []
adrs: [0002]
layers: [backend, functions, db]
security_touching: false
manual_steps: [ef-migration]
sprint: 1
source: ADR-0002 Wave-1 build
---

## Context

This is the **Wave-1 backing swap** for the side-effect dispatch contract frozen in
**ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`). Wave-0 (T-0118 / F2 â€” the
`IPendingDispatch` + `PostCommitDispatchBehavior` seam, ADR-0002 D1/D4) made every Bucket-A command
handler **record intent** instead of calling `IQueueClient.SendAsync` directly, and dispatch that
intent **after** the UnitOfWork commit â€” but the buffer is **in-memory**, so dispatch is
**at-most-once**: a crash between commit and drain loses the send (ADR-0002 framing banner + D1
"best-effort in Wave-0"; Consequences "Wave-0 known residual gaps"). On the two fiscal queues that
residual loss is backstopped only by the 15-min reconciliation sweep (D3.4 / FISCAL-RECON).

ADR-0002 D5 names exactly what Wave-1 must do: *"swap **only** `IPendingDispatch`'s backing so
`Enqueue` writes an outbox row in the same DbContext (atomic with business state â€” dual-write gone),
plus a drainer. **No Bucket-A handler and no consumer changes.**"* The handler-facing contract was
designed precisely so this swap touches **zero** command-handler call sites (D1.1 in-request
idempotency invariant; D5 "change command-handler call sites once"). This ticket delivers that durable,
at-least-once backing across all five queues (`QueueNames.cs:5-9`: `generate-receipt`,
`generate-invoice`, `notifications-dispatch`, `sitewide-promo-fanout`, `calculate-order-pay`).

ADR-0002 explicitly **defers two decisions to this ticket's own design** (it freezes the contract, not
the table): the outbox **table design** (D5: *"the outbox table design is deferred to F2-FULL's own
ADR; this ADR only guarantees it slots under the frozen `IPendingDispatch` seam"*) and the
**in-Functions-host drainer question** (D1.3: *"the Wave-1 ADR (F2-FULL) MUST decide whether the
Functions host gets the post-commit behavior, the drainer, both, or neither"*). Bucket-B sweeps/
called-services (the 7 sends carved out in Wave-0 â€” `AutoCancelStaleRecurringOrders.cs:87`,
`SendRecurringOrderReminders.cs:77`, `SendMembershipLifecycleNotifications.cs:87,125`,
`NewJobsDigestService.cs:170`, `SendSitewidePromo.cs:88`, and `LoyaltyService.cs:75`) **migrate here**
onto the per-iteration-commit outbox row (D5 Bucket B: *"they move to the outbox row written inside
each per-iteration commit â€” the correct shape"*).

**This is an `L`.** It carries an architect deliverable (the F2-FULL outbox-table ADR), a DB
migration, a backend backing swap + drainer, a Functions-host decision, and the Bucket-B migration. It
**MUST be split** (suggested seams below) before it can go `ready` â€” an `L` never runs as one ticket.

## Acceptance criteria

> All ACs are grounded in the ADR-0002 contract + real code. The contract's verification gate
> (checks #1â€“#4, TC-IDEMP-0/TC-KEY-0/TC-DISPATCH-0) must stay **green** through the swap â€” Wave-1 must
> not regress any Wave-0 guarantee.

- [ ] **AC1 â€” Outbox-table ADR exists and is accepted (architect deliverable, defers resolved).**
  Given ADR-0002 defers the table design to "F2-FULL's own ADR" (D5) and the in-Functions drainer to
  this ticket (D1.3), When this ticket starts, Then a new accepted ADR (next free number, supersedes
  nothing) defines: the outbox row schema (at minimum a stable id, `(QueueName, MessageKey)` with the
  **uniqueness** that realizes D1.1's in-request idempotency, `Body`, `TenantId`, created/claimed/
  dispatched timestamps, attempt count, status), the drainer's claim/lease + ordering + retry/back-off
  semantics, and an explicit **answer** to D1.3 (does the Functions worker get the post-commit
  behavior, the drainer, both, or neither). The ADR cites ADR-0002 as the contract it backs.

- [ ] **AC2 â€” `Enqueue` writes a durable outbox row in the pipeline's own DbContext (dual-write gone).**
  Given a Bucket-A command handler calls `IPendingDispatch.Enqueue(queueName, message, messageKey)`
  (unchanged signature â€” `Cleansia.Core.Queue.Abstractions/IPendingDispatch.cs`, ADR-0002 D1), When the
  command succeeds, Then `Enqueue` writes an outbox row into the **same scoped `DbContext`** the
  `UnitOfWorkPipelineBehavior` commits, so the row is persisted **atomically** with the business state
  in a single commit (no message exists for a never-committed row, and no committed row lacks its
  intended message). A test proves the row and the business change land in the same transaction (commit
  throws â†’ neither persists).

- [ ] **AC3 â€” In-request idempotency invariant preserved (D1.1).** Given a handler calls `Enqueue`
  twice in one request with the **same** `(QueueName, MessageKey)`, When the request commits, Then
  **exactly one** outbox row exists for that key (collapsed via the table's uniqueness, not two rows) â€”
  matching the Wave-0 in-memory collapse so the swap is behaviorally equivalent (ADR-0002 D1.1, CH-4).
  A handler that early-returns without a commit writes **no** outbox row (D1.1 last bullet). Test proves
  both.

- [ ] **AC4 â€” Drainer delivers each row at-least-once, then marks it dispatched.** Given committed
  outbox rows, When the drainer runs, Then it claims un-dispatched rows (lease/claim so two drainer
  instances do not double-send the same row), calls the existing `IQueueClient.SendAsync` with the
  row's `QueueEnvelope<T>` body, and marks the row dispatched **only after** a successful send;
  a send failure leaves the row claimable for retry (at-least-once). A crash between commit and send no
  longer loses the message â€” the row survives and is re-drained. Test proves a row left undispatched is
  picked up on the next drain.

- [ ] **AC5 â€” Zero Bucket-A handler churn and zero consumer churn (the contract's whole point).**
  Given the Wave-0 Bucket-A call sites (the 14 sends migrated to `pending.Enqueue` in T-0118 â€” D5 step
  3 inventory: `CreateOrder.cs:376`, `CompleteOrder.cs:219,227,266`, `ConfirmRecurringOrder.cs:112,118`,
  `CancelOrder.cs:160`, `TakeOrder.cs:195`, `StartOrder.cs:137`, `NotifyOnTheWay.cs:103`,
  `AddDisputeMessage.cs:67`, `HandlePaymentNotification.cs:241,246,278`), When this ticket lands, Then
  **none of those call sites change** and **no consumer (`generate-receipt` etc.) changes** â€” only the
  `IPendingDispatch` backing + the drainer are added. Verified by diff: the Features/** handlers and
  the Functions consumers are untouched.

- [ ] **AC6 â€” Wave-0 contract gate stays green (no regression).** Given ADR-0002's verification gate
  (check #1 no direct queue send from a Bucket-A handler; check #4 pipeline-order test;
  TC-KEY-0 deterministic keys; TC-DISPATCH-0 dispatch-only-on-committed-success; TC-IDEMP-0 consumers
  safe to run twice), When the backing is swapped, Then **all of those tests still pass** â€” the
  deterministic `MessageKey` formulas (D2.1 frozen table) and the consumer-side dedup are unchanged and
  remain load-bearing for the now-at-least-once redeliveries.

- [ ] **AC7 â€” Bucket-B sweeps/called-services migrate to the per-iteration-commit outbox (D5 Bucket B).**
  Given the 7 Bucket-B sends that loop-and-commit-per-iteration (carved out of Wave-0 under the
  reviewer-check-#1 whitelist), When this ticket lands, Then each writes its message as an outbox row
  **inside its own per-iteration commit** (each commit drains its own row â€” the correct shape ADR-0002
  D5 names), removing the direct `IQueueClient.SendAsync` from those sites. The Wave-0 carve-out
  whitelist entries for these sites are removed. `SendSitewidePromoFanoutFunction.cs:123` (Bucket C, a
  Function with no commit to gate) **stays direct** per D2.3 â€” not migrated.

- [ ] **AC8 â€” In-Functions-host behavior matches the AC1 decision (D1.3).** Given a command invoked
  inside a Function (e.g. `CalculateOrderPay` from `CalculateOrderPayFunction.cs:43`) runs the full
  MediatR pipeline in the Functions worker, When this ticket lands, Then the host behaves exactly as the
  AC1 ADR decides (post-commit behavior / drainer / both / neither) â€” no nested-outbox surprise, no row
  written-but-never-drained inside the worker. Test or wiring assertion proves the decided configuration.

- [ ] **AC9 â€” Test-first, and the outbox is "safe to run twice" end-to-end.** Given `knowledge/testing.md`
  (TDD strict for this transaction-boundary + delivery logic) and ADR-0002's must-cover idempotency (S7,
  testing.md item #6), When the work is done, Then the drainer/backing tests were written **before** the
  implementation (red â†’ green, visible in commit order / status log), and an integration-style test
  drives commit â†’ drain â†’ consumer **twice** for a fiscal queue and asserts the terminal effect happens
  **exactly once** (the at-least-once delivery is absorbed by the unchanged D2.2 consumer dedup).

## Out of scope

- **The Wave-0 seam itself** â€” `IPendingDispatch` interface, `PostCommitDispatchBehavior`, the F11/D4
  pipeline reorder, the `QueueEnvelope<T>` wrapper, the D2.1 `MessageKey` formulas, and the Bucket-A
  call-site migration. All delivered by **T-0118 (F2)** â€” this ticket's dependency. T-0143 swaps the
  *backing* behind that seam; it does not create the seam.
- **Consumer idempotency guards / poison consumers / dual-read** (ADR-0002 D2.2 / D2.1a / D3) â€” those
  are T-0119 (F4), F3, FUNC-CORE. The outbox makes delivery durable; consumer-side safety is unchanged
  and out of scope here.
- **The fiscal reconciliation sweep** (ADR-0002 D3.4 / FISCAL-RECON) â€” Wave-0's backstop for the
  *never-enqueued* gap. Once the durable outbox lands the never-enqueued gap is closed for outbox-backed
  sends; whether to retire the sweep is a separate decision, not this ticket's edit.
- **`generate-invoice` effect** (the stub at `GenerateInvoiceFunction.cs:20-26`) and any new queue â€”
  out of scope per ADR-0002 D2.2; this ticket changes delivery, not consumer effects.
- **NSwag regen** â€” the queue/outbox contract is internal (ADR-0002 Consequences: "No NSwag change").
  `manual_steps: [ef-migration]` only (the outbox table).

## Implementation notes

- **Serialization cluster (IMPORTANT).** This ticket is in the **`UnitOfWorkPipelineBehavior.cs` +
  queue call-sites** cluster (TICKET-MAP Â§Shared-file map, row 3: **F11 â†’ F2/SEC-W1 â†’ F4 â†’ F3**). It
  swaps the `IPendingDispatch` backing that F2 (T-0118) built and touches the same dispatch/pipeline
  surface; it is a **cross-wave continuation** of that cluster and must **not** run concurrently with
  any other cluster ticket. It depends on **T-0118 being `done`** (the seam must exist first).
  The Bucket-B migration (AC7) edits `LoyaltyService.cs` â€” note the separate `LoyaltyService.cs`
  cluster (TICKET-MAP row 7: LG-SEC-06 â†’ LG-01q/LG-03); serialize the `LoyaltyService.cs` edit against
  those if any is concurrently runnable.
- **Governing ADR:** **ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`) is the
  frozen contract. The load-bearing clauses for this ticket: **D1.1** (in-request idempotency invariant
  â€” the table's `(QueueName, MessageKey)` uniqueness realizes it), **D1.3** (the in-Functions drainer
  question this ticket must answer), **D5** (the Wave-1 paragraph: swap backing only, zero handler/
  consumer churn, Bucket B migrates here, table design deferred to this ticket's own ADR), and
  **Consequences â†’ Rollout** (no NSwag; `ProcessedMessage`/`DeadLetter`/outbox tables are
  `ef-migration`, owner-only).
- **Architect first (new pattern / deferred decision):** ADR-0002 explicitly defers the outbox **table
  design** and the **D1.3 host decision** to F2-FULL â€” invoke `architect` to author the new ADR (AC1)
  **before** db/backend work begins. Route per `routing.md`: architect (ADR) â†’ db (migration +
  entity config) â†’ backend (backing swap + drainer + Bucket-B migration) â†’ functions (host decision
  per AC8). Spawn a **reviewer** in parallel with every developer instance.
- **Owner-only manual step:** the outbox table needs an **EF Core migration** â€” `manual_step:
  ef-migration`. Claude does **NOT** run `dotnet ef migrations add` / `database update`. The PM holds
  backend/functions work that depends on the table until the owner confirms the migration is applied.
- **Real code anchors:** `QueueNames.cs:5-9` (the 5 queues), `AzureStorageQueueClient.cs:14-27` (the
  unchanged client the drainer calls), `UnitOfWorkPipelineBehavior.cs:19-20` (the commit the outbox row
  rides on), `Cleansia.Functions/Program.cs` (wires the pipeline into the worker â€” D1.3 surface),
  `CalculateOrderPayFunction.cs:43` (the in-Function command-send), and the Bucket-B sites listed in
  AC7. The `IPendingDispatch` / `QueueEnvelope<T>` / `PostCommitDispatchBehavior` types are introduced
  by T-0118; confirm their final shape from that ticket's merged diff before swapping the backing.
- **TEST-FIRST** per `agents/knowledge/testing.md` â€” the drainer claim/lease, the atomic
  row-with-business-state commit, and the in-request key-collapse are **logic**, so their tests are
  written first (red â†’ green, visible in commit order / status log). The end-to-end "safe to run twice"
  (AC9) reuses the TC-IDEMP-0 shape from ADR-0002 verification check #5. Tests live in `Cleansia.Tests`
  / `Cleansia.IntegrationTests` (the latter for the commitâ†’drainâ†’consumer path).
- **Split before `ready` (this is an `L`).** Suggested seams: (1) the outbox-table ADR (architect);
  (2) the table + EF config + migration flag (db); (3) the `IPendingDispatch` durable backing + drainer
  + D1.3 host decision (backend/functions); (4) the Bucket-B migration onto the outbox row (backend).
  Each child carries its slice of the ACs above and `depends_on` the prior. The PM must not let T-0143
  run un-split.

## Status log
- 2026-06-01 - draft (created by pm)
- 2026-06-05 - SPLIT (owner authorized, sprint-3 section 1). This L epic is split into 4 strictly-serial
  children (a -> b -> c -> d, same dispatch/pipeline surface) and does not run as one ticket:
  T-0155 (a - outbox-table ADR; answers ADR-0002 D1.3 in-Functions-drainer question + table schema;
  architect), T-0156 (b - outbox table + EF config + migration flag; db), T-0157 (c - durable
  IPendingDispatch backing + drainer + host decision; backend+functions; also depends_on T-0118),
  T-0158 (d - Bucket-B migration onto the per-iteration outbox row; backend; depends_on T-0148 for the
  shared LoyaltyService.cs edit). The parent depends_on [T-0118, T-0148] now lives on the children that
  actually need those edges (T-0157 -> T-0118; T-0158 -> T-0148). This epic stays draft as a tracking
  record; work happens in the children. T-0155 promoted to ready (Batch 1A); T-0156/T-0157/T-0158 stay
  draft until their predecessor is done.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
