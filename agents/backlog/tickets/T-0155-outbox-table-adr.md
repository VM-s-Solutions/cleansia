---
id: T-0155
title: "ADR: outbox table design + in-Functions drainer decision (resolves ADR-0002 D1.3 / D5)"
status: done
size: M
owner: architect
created: 2026-06-05
updated: 2026-06-06
depends_on: []
blocks: [T-0156, T-0157, T-0158]
stories: []
adrs: [0002, 0008]
layers: [architect]
security_touching: false
manual_steps: []
sprint: 1
source: split of T-0143 (child a); ADR-0002 D5 deferred table design + D1.3 host decision
---

## Context
Split child **(a)** of L-epic **T-0143** (full transactional outbox). T-0143 carries an architect
deliverable, a db migration, a backend backing swap + drainer, an in-Functions host decision, and a
Bucket-B migration — too large to run as one ticket. Per the §1 proposal in `status/sprint-3.md` it
splits a→b→c→d, **strictly serial** (same dispatch/pipeline surface). This child is the **ADR-only**
deliverable and the gate for (b) table+EF, (c) backing+drainer+host, and (d) Bucket-B.

ADR-0002 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`) freezes the dispatch contract but
**explicitly defers two decisions** to this ticket:
- **The outbox table design** (D5: "the outbox table design is deferred to F2-FULL's own ADR; this ADR
  only guarantees it slots under the frozen `IPendingDispatch` seam").
- **The in-Functions-host drainer question** (D1.3: "the Wave-1 ADR (F2-FULL) MUST decide whether the
  Functions host gets the post-commit behavior, the drainer, both, or neither").

## Acceptance criteria
- [ ] **AC1 (ADR exists & accepted)** — Given ADR-0002 defers the table design (D5) and the
  in-Functions drainer (D1.3) to this ticket, When this child completes, Then a new ADR (next free
  number, supersedes nothing) is `Status: accepted`, follows the project ADR template, cites ADR-0002
  as the contract it backs, and is reviewer-reconciled with zero blocking challenges.
- [ ] **AC2 (table schema frozen)** — The ADR defines the outbox row schema: at minimum a stable id,
  `(QueueName, MessageKey)` with the **uniqueness** that realizes ADR-0002 D1.1's in-request idempotency,
  `Body`, `TenantId`, created/claimed/dispatched timestamps, attempt count, and status.
- [ ] **AC3 (drainer semantics frozen)** — The ADR specifies the drainer's claim/lease, ordering, and
  retry/back-off semantics (so two drainer instances cannot double-send the same row; at-least-once).
- [ ] **AC4 (D1.3 answered)** — The ADR gives an explicit **answer** to ADR-0002 D1.3: does the
  Functions worker get the post-commit behavior, the drainer, both, or neither — with the reason cited.
  This answer governs T-0157 (AC8).
- [ ] **AC5 (traceability)** — The ADR number is recorded back into this ticket's `adrs:` frontmatter
  and into T-0156/T-0157/T-0158; the "Consequences / rollout" lists the `ef-migration` (table) as
  owner-only and confirms no NSwag change (the contract is internal).

## Out of scope
- Any code, EF config, or migration — those are T-0156 (table+EF) / T-0157 (backing+drainer+host) /
  T-0158 (Bucket-B).
- The Wave-0 seam itself (`IPendingDispatch`, `PostCommitDispatchBehavior`, `QueueEnvelope<T>`, the
  D2.1 MessageKey formulas, Bucket-A call-site migration) — delivered by T-0118; this chain swaps the
  backing, it does not redesign the seam.
- Redefining ADR-0002's contract — cite, don't redo.

## Implementation notes
- **Architect authors; reviewer runs in parallel.** No db/backend/functions work in the chain starts
  until this ADR is `accepted`. `manual_steps: []` (ADR-only); `security_touching: false`; no QA gate.
- Load-bearing ADR-0002 clauses: **D1.1** (in-request idempotency the `(QueueName, MessageKey)`
  uniqueness realizes), **D1.3** (the host decision this ADR must answer), **D5** (swap backing only,
  zero handler/consumer churn, table design deferred here).
- Code anchors for the architect: `QueueNames.cs:5-9` (the 5 queues),
  `AzureStorageQueueClient.cs:14-27` (the unchanged client the drainer calls),
  `UnitOfWorkPipelineBehavior.cs:19-20` (the commit the outbox row rides on),
  `Cleansia.Functions/Program.cs` (the D1.3 host surface),
  `CalculateOrderPayFunction.cs:43` (the in-Function command-send).

## Status log
- 2026-06-05 — draft (created by pm; split of T-0143 child a)
- 2026-06-05 — ready (Batch 1A promoted; owner authorized the split + confirmed architect owns the
  ADR-0002 D1.3 decision; no deps; routed to architect)
- 2026-06-06 — in_review (architect authored **ADR-0008** `0008-outbox-table-and-drainer.md`,
  Status: accepted, via deliberation panel; zero blocking; cross-checked against ADR-0002 — backs, does
  not contradict). Resolves both deferred ADR-0002 decisions: **D5** → `OutboxMessages` table schema
  with **UNIQUE (QueueName, MessageKey)** (realizes D1.1 in-request idempotency; reasoned S8 exception
  like ADR-0004); **D1.3** → the Functions host **keeps** `PostCommitDispatchBehavior` (in-Function side
  effects stay durable) but is **NOT** the per-instance drainer — the drainer is a **single dedicated
  host** (recommended: a singleton timer Function), governs T-0157 AC8. Drainer claims under a lease
  (`FOR UPDATE SKIP LOCKED`), at-least-once, dead-letters via the existing ADR-0002 D3 store; backing swap
  is DI-only (`InMemoryPendingDispatch` → `OutboxPendingDispatch`), zero Bucket-A churn. `ef-migration`
  owner-only; no NSwag. AC1-AC5 satisfied; no owner question. Reviewer to reconcile, then PM → done +
  unblock T-0156/157/158 (strictly serial).
- 2026-06-06 — done (reviewer reconciled: AC1-AC5 satisfied; ADR-0008 resolves BOTH ADR-0002 deferrals —
  D5 `OutboxMessages` table schema with UNIQUE (QueueName, MessageKey) realizing ADR-0002 D1.1 in-request
  idempotency, and D1.3 the in-Functions-host answer (Functions host KEEPS `PostCommitDispatchBehavior`
  so in-Function side effects stay durable, but is NOT the per-instance drainer — exactly one dedicated
  drainer, recommended a singleton timer Function, governs T-0157 AC8). Drainer claim/lease via
  `FOR UPDATE SKIP LOCKED`, at-least-once, dead-letters via the existing ADR-0002 D3 store; backing swap
  is DI-only. Backs, does not contradict, ADR-0002 (zero Bucket-A churn). S8 unique-key exception reasoned
  like ADR-0004. `adrs:[0002,0008]` wired; `ef-migration` owner-only flagged. Zero blocking; ADR
  `accepted`). **Unblocks Batch 1B: T-0156 then T-0157 then T-0158 (strictly serial).**

## Review
- **reviewer (2026-06-06): APPROVE.** ADR-0008 is decision-complete: full table schema (D1), durable
  `OutboxPendingDispatch` backing writing into the pipeline's scoped DbContext (D2), drainer
  claim/lease/ordering/backoff/dead-letter semantics (D3), and an explicit, reasoned answer to ADR-0002
  D1.3 (D4). Confirmed it does not redefine the frozen `IPendingDispatch` seam — the whole-point "zero
  call-site churn" promise holds (DI registration swap only). The at-most-once Wave-0 gap is closed,
  which ADR-0002 explicitly said Wave-1 would do (backing, not contradicting). Test contract
  (TC-OUTBOX-ATOMIC/DRAIN/LEASE/DEADLETTER-0) handed to T-0157 red-first. **No gaps.**
- PM reconciled reviewer verdict → `done`.
