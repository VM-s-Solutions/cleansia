---
id: T-0157
title: "Durable IPendingDispatch backing + post-commit drainer + in-Functions host decision"
status: draft
size: M
owner: —
created: 2026-06-05
updated: 2026-06-05
depends_on: [T-0156, T-0118]
blocks: [T-0158]
stories: []
adrs: [0002, 0008]
layers: [backend, functions]
security_touching: false
manual_steps: []
sprint: 1
source: split of T-0143 (child c); ADR-0002 D1.1/D1.3/D5
---

## Context
Split child **(c)** of L-epic **T-0143**. Swaps **only** `IPendingDispatch`'s backing so `Enqueue`
writes an outbox row in the same scoped `DbContext` (dual-write gone), adds the post-commit drainer that
delivers each row at-least-once, and wires the Functions host per the T-0155 D1.3 decision. **Zero
Bucket-A handler churn and zero consumer churn** is the whole point of the contract (ADR-0002 D5).
**Strictly serial** in the a→b→c→d chain and within the `UnitOfWorkPipelineBehavior` + queue cluster.

## Acceptance criteria
- [ ] **AC1 (Enqueue writes a durable row atomically)** — Given a Bucket-A handler calls
  `IPendingDispatch.Enqueue(queueName, message, messageKey)` (unchanged signature), When the command
  succeeds, Then `Enqueue` writes an outbox row into the **same scoped `DbContext`** the
  `UnitOfWorkPipelineBehavior` commits, persisted atomically with the business state in a single commit.
  A test proves commit throws → neither the row nor the business change persists.
- [ ] **AC2 (in-request idempotency preserved, D1.1)** — Given a handler calls `Enqueue` twice in one
  request with the same `(QueueName, MessageKey)`, When the request commits, Then exactly **one** outbox
  row exists for that key (collapsed via the table's uniqueness). A handler that early-returns without a
  commit writes **no** row. Test proves both.
- [ ] **AC3 (drainer at-least-once)** — Given committed outbox rows, When the drainer runs, Then it
  claims un-dispatched rows (lease/claim so two instances do not double-send), calls the existing
  `IQueueClient.SendAsync` with the row's `QueueEnvelope<T>` body, and marks the row dispatched **only
  after** a successful send; a send failure leaves the row claimable for retry. A crash between commit
  and send no longer loses the message. Test proves an undispatched row is picked up on the next drain.
- [ ] **AC4 (zero handler/consumer churn, D5)** — Given the Wave-0 Bucket-A call sites migrated in
  T-0118, When this child lands, Then **none of those call sites change** and **no consumer changes** —
  only the backing + drainer are added. Verified by diff (Features/** handlers and Functions consumers
  untouched).
- [ ] **AC5 (Wave-0 gate stays green)** — Given ADR-0002's verification gate (check #1, check #4
  pipeline-order, TC-KEY-0, TC-DISPATCH-0, TC-IDEMP-0), When the backing is swapped, Then all of those
  tests still pass (no regression of any Wave-0 guarantee).
- [ ] **AC6 (in-Functions host matches the D1.3 decision)** — Given a command invoked inside a Function
  (e.g. `CalculateOrderPay` from `CalculateOrderPayFunction.cs:43`) runs the full MediatR pipeline in
  the worker, When this child lands, Then the host behaves exactly as the T-0155 ADR decided
  (post-commit behavior / drainer / both / neither) — no nested-outbox surprise, no row written-but-
  never-drained inside the worker. Test or wiring assertion proves the configuration.
- [ ] **AC7 (test-first)** — The drainer claim/lease, the atomic row-with-business-state commit, and
  the in-request key-collapse are logic → tests written first (red→green, visible in commit order /
  status log) per `agents/knowledge/testing.md`.

## Out of scope
- The Bucket-B sweeps/called-services migration onto the per-iteration outbox row — that is T-0158.
- The outbox table + EF config (T-0156) and the ADR (T-0155) — consumed here.
- Consumer idempotency guards / poison consumers / dual-read (ADR-0002 D2.2/D2.1a/D3) — T-0119/F3/T-0121.
- The fiscal reconciliation sweep (D3.4/FISCAL-RECON) — separate decision.
- NSwag regen — the contract is internal (ADR-0002 Consequences); `manual_steps: []` (the table
  migration was the T-0156 step, confirmed before this child starts).

## Implementation notes
- **Gated on T-0156 done + owner-confirmed migration AND T-0118 done** (the seam must already exist).
- **Serialization cluster (IMPORTANT):** the `UnitOfWorkPipelineBehavior.cs` + queue cluster (TICKET-MAP
  row 3) — must NOT run concurrently with any other cluster member. Also do **not** run concurrently
  with **T-0151** on the `Cleansia.Functions/Functions/*.cs` files.
- Route: backend (backing swap + drainer) → functions (host decision per AC6). Spawn a reviewer in
  parallel with every developer instance.
- Confirm the final shape of `IPendingDispatch` / `QueueEnvelope<T>` / `PostCommitDispatchBehavior` from
  the merged T-0118 diff before swapping the backing.
- Anchors: `Cleansia.Core.Queue.Abstractions/IPendingDispatch.cs`, `AzureStorageQueueClient.cs:14-27`,
  `UnitOfWorkPipelineBehavior.cs:19-20`, `Cleansia.Functions/Program.cs`, `CalculateOrderPayFunction.cs:43`.

## Status log
- 2026-06-05 — draft (created by pm; split of T-0143 child c; blocked on T-0155→T-0156 + T-0118 done)
- 2026-06-06 — STAYS draft/blocked (Batch 1B; its ADR gate **ADR-0008 / T-0155 is done ✓** and dep
  **T-0118 done ✓**, but it `depends_on: T-0156` which is only `ready` (not `done`) AND its owner
  ef-migration is not yet applied. The outbox chain is **strictly serial** — promote to `ready` once T-0156
  is `done` + the owner confirms the migration. Implements ADR-0008 D2/D3/D4 (governs the single-drainer
  host answer, AC8). `adrs` set to `[0002,0008]`. Note: edits `Cleansia.Functions/Functions/*.cs` →
  serialize against T-0151 (run T-0151 first)).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
