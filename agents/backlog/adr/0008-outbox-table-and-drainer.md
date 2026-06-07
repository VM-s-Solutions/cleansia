# ADR-0008 — Transactional outbox: table schema, durable `IPendingDispatch` backing, a single dedicated drainer host, and the in-Functions-host answer (resolves ADR-0002 D1.3 / D5)

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-06
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | functions | cross-cutting
- **Backs / extends:** ADR-0002 (this is the deferred D5 table design + the D1.3 host answer; it does **not** redefine the frozen `IPendingDispatch` contract)
- **Ticket:** T-0155 (outbox ADR) · **Consumers (strictly serial):** T-0156 (table + EF), T-0157 (backing + drainer + host), T-0158 (Bucket-B migration)

> This ADR is **ADR-OUTBOX-TABLE**. ADR-0002 froze the dispatch *contract* and explicitly deferred **two**
> decisions to this ticket: the **outbox table design** (D5) and the **in-Functions-host drainer
> question** (D1.3). This ADR answers both. It changes **no command-handler call site** (the whole point
> of ADR-0002's seam) and ships **no code** — the table+EF, the backing swap+drainer, and Bucket-B
> migration are T-0156/157/158, strictly serial. Once `accepted` it is immutable — supersede, never edit.

---

## Context

ADR-0002 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`) is **accepted and landed in Wave-0**:
- `IPendingDispatch` (`src/Cleansia.Core.Queue.Abstractions/IPendingDispatch.cs`) is the frozen
  handler-facing seam; handlers call `Enqueue(queueName, message, messageKey)`, not `IQueueClient`.
- The Wave-0 backing is **`InMemoryPendingDispatch`**
  (`src/Cleansia.Infra.Azure.Storage.Queues/InMemoryPendingDispatch.cs`) — a pure in-memory, scoped
  buffer. `PostCommitDispatchBehavior` (`src/Cleansia.Core.AppServices/Behaviors/PostCommitDispatchBehavior.cs`)
  drains it post-commit and calls the unchanged `AzureStorageQueueClient.SendAsync`
  (`src/Cleansia.Infra.Azure.Storage.Queues/AzureStorageQueueClient.cs:14-27`).
- The commit the outbox row must ride on is `UnitOfWorkPipelineBehavior` (`:19-20`).
- The five queues are `QueueNames.cs:5-9`. The `-poison` consumers, the `DeadLetter` store, and the D3.4
  fiscal reconciliation timer are **all already deployed** (verified in `Cleansia.Functions/Program.cs:46-74`).

ADR-0002 is **honest** that Wave-0 dispatch is **at-most-once**: a crash between the commit and the
in-memory drain **loses the send** (recovered durably only in Wave-1; detected in Wave-0 on the two
fiscal queues by the D3.4 reconciliation backstop). ADR-0002 deferred exactly two things to this ADR:

- **D5:** *"the outbox table design is deferred to F2-FULL's own ADR; this ADR only guarantees it slots
  under the frozen `IPendingDispatch` seam."*
- **D1.3:** *"the Wave-1 ADR (F2-FULL) MUST decide whether the Functions host gets the post-commit
  behavior, the drainer, both, or neither."* — because `Cleansia.Functions/Program.cs` wires the **full
  MediatR pipeline** into the Functions worker, so `PostCommitDispatchBehavior` runs there too (e.g.
  `CalculateOrderPayFunction` → `mediator.Send(CalculateOrderPay.Command)`). In Wave-1 an in-Function
  command would **write outbox rows**, raising the "nested outbox" surprise ADR-0002 named.

This ADR makes Wave-1 land with **zero Bucket-A handler/consumer churn** — the expensive part was paid in
Wave-0 (the call-site migration). What remains is: a durable table, a backing that writes to it atomically
with business state, a drainer that puts rows on the wire at-least-once, and the host answer.

This is **one decision** — "the durable backing for the frozen seam" — because the table schema, the
drainer's claim/ordering/retry semantics, and the host that runs it are inseparable: the schema's
uniqueness *is* the idempotency the drainer relies on; the drainer's lease *is* what makes the
"two instances can't double-send" guarantee; and the host choice determines where the drainer (and the
nested-outbox risk) lives.

---

## Decision

> **Contract principle (backs, does not change, ADR-0002).** The durable **outbox row** is the Wave-1
> backing of `IPendingDispatch`. `Enqueue` writes a row into the **same scoped `DbContext`** the pipeline
> commits — so a message exists **iff** the business state committed (the dual-write is gone, not merely
> relocated). A **single dedicated drainer** claims unsent rows under a lease, sends them via the
> unchanged `AzureStorageQueueClient`, and marks them dispatched — **at-least-once**, deduped downstream
> by ADR-0002 D2.2's idempotent consumers on the **same** deterministic `MessageKey`. **No command-handler
> call site and no queue consumer changes.**

### D1 — The outbox table schema (resolves ADR-0002 D5) (AC2)

One table, `OutboxMessages`, in `Cleansia.Infra.Database` (EF config in T-0156):

| Column | Type | Purpose |
|---|---|---|
| `Id` | ULID/string PK | stable row identity (sortable → insertion order, D3) |
| `QueueName` | string | one of `QueueNames.cs:5-9` |
| `MessageKey` | string | ADR-0002 D2.1 deterministic key |
| `Body` | text | the already-serialized `QueueEnvelope<T>` wire body (`PendingMessage.Body` verbatim — D2.1) |
| `TenantId` | string? | explicit (the drainer has no JWT; mirrors `QueueEnvelope.TenantId`, D2.1) |
| `Status` | smallint enum | `Pending` \| `Dispatched` \| `Failed` |
| `AttemptCount` | int | drainer retry/backoff (D3) |
| `CreatedOn` | timestamptz | enqueue time (ordering tiebreak, retention) |
| `ClaimedOn` | timestamptz? | lease timestamp (D3 claim/lease) |
| `ClaimedBy` | string? | drainer instance/lease token (D3) |
| `DispatchedOn` | timestamptz? | set on successful send |
| `NextAttemptAt` | timestamptz? | backoff schedule (D3) |
| `LastError` | text? | last send failure (ops/forensics) |

**Indexes / constraints:**
- **`UNIQUE (QueueName, MessageKey)`** — this is what realizes **ADR-0002 D1.1's in-request idempotency**
  in the durable backing: a double-`Enqueue` with the same key in one request collapses to **one** row
  (insert-or-ignore on the unique key), exactly as the in-memory buffer collapses today. **A migration is
  required** → `manual_step: ef-migration` (owner-only, T-0156).
- **Partial index on `(NextAttemptAt) WHERE Status = Pending`** — the drainer's claim query is cheap and
  does not scan dispatched rows.
- **`TenantId` is NOT part of the unique key.** The `MessageKey` is already globally unique per logical
  effect (it embeds `OrderId`/`PayPeriodId`/etc., all globally-unique ULIDs) — adding `TenantId` would
  *weaken* dedup for null-tenant single-tenant rows. This is the **same reasoned S8 exception** ADR-0004
  recorded for `IX_OrderReceipts_OrderId`; recorded here explicitly so the S8 tenant-scoped-unique-index
  grep finds the justification and does **not** "fix" it to `(TenantId, QueueName, MessageKey)`.

**Retention:** `Dispatched` rows are pruned by a timer (e.g. older than 7 days) so the table stays small;
the prune cadence is config-driven and is not load-bearing for correctness.

### D2 — The durable `IPendingDispatch` backing (swap only — ADR-0002 D5) (AC5 traceability)

`Enqueue` writes an `OutboxMessage` row into the **same scoped `DbContext`/UoW** the pipeline commits
(`OutboxPendingDispatch : IPendingDispatch`, registered scoped, replacing `InMemoryPendingDispatch` in DI
— a **one-line registration swap** in T-0157). Because the row rides the **same** commit:
- a committed success → the row is durable → the drainer will send it (at-least-once);
- a validation/handler failure or early-return-without-commit → **no row** (nothing committed) — exactly
  ADR-0002 D1.2's "buffer discarded on non-success," now realized as "no row written."

`Drain()` semantics change subtly but **within the frozen contract**: in Wave-1 the dispatch behavior
does **not** put bytes on the wire itself; the durable row IS the dispatch record. `PostCommitDispatchBehavior`
on a committed success **nudges the drainer** (an optional fast-path "drain now" signal) and returns —
the drainer is the authoritative sender. **Crucially: the contract that `Enqueue` is infallible/non-network
and dispatch never throws into the response (ADR-0002 D1) is preserved** — `Enqueue` is now a tracked
insert on the existing DbContext (no network), and the row's *sending* is fully decoupled from the request.
**No Bucket-A handler changes; no consumer changes** (the consumers already dedup on `MessageKey`).

### D3 — Drainer semantics: claim/lease, ordering, retry/backoff (AC3)

A single drainer loop (host in D4) repeatedly:

1. **Claim under a lease (no double-send).** Atomically claim a batch of `Pending` rows whose
   `NextAttemptAt <= now` (or null), ordered by `CreatedOn, Id` (D3 ordering), via a single SQL statement:
   ```sql
   UPDATE "OutboxMessages" SET "Status" = ..., "ClaimedBy" = @token, "ClaimedOn" = now()
   WHERE "Id" IN (
     SELECT "Id" FROM "OutboxMessages"
     WHERE "Status" = Pending AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now())
     ORDER BY "CreatedOn", "Id"
     LIMIT @batch
     FOR UPDATE SKIP LOCKED            -- two drainer instances never grab the same row
   ) RETURNING ...;
   ```
   `FOR UPDATE SKIP LOCKED` + the lease columns guarantee **two drainer instances cannot claim the same
   row** — so even if D4's "single host" is briefly violated (deploy overlap), correctness holds. The
   claim is its **own committed transaction** before any send.
2. **Send** each claimed row's `Body` verbatim via `AzureStorageQueueClient.SendAsync(QueueName, Body)`
   (the unchanged client; `Body` is the already-serialized `QueueEnvelope<T>` — D1/D2.1).
3. **On send success:** mark `Status = Dispatched`, set `DispatchedOn`. (A crash *after* the Azure send
   but *before* this mark re-sends on the next claim — **at-least-once**; the downstream consumer dedups
   on `MessageKey` per ADR-0002 D2.2. This is the accepted at-least-once shape, not a bug.)
4. **On send failure:** increment `AttemptCount`, set `NextAttemptAt = now + backoff(AttemptCount)`
   (exponential + jitter), record `LastError`, release the lease (`Status` back to `Pending`). After a
   max-attempts ceiling, mark `Status = Failed` and record a **`DeadLetter` row + alert** through the
   **existing** ADR-0002 D3 `IDeadLetterStore` — so a permanently-unsendable outbox row surfaces the same
   way a poisoned queue message does (no new dead-letter mechanism).

**Guarantees, stated precisely:**
- **At-least-once delivery** to the queue (the row is durable before any send; a crash re-sends). This is
  the durability ADR-0002 Wave-0 lacked — it **removes** the at-most-once dispatch gap and therefore makes
  the D3.4 fiscal reconciliation a *backstop* rather than the primary net (reconciliation stays — it is
  cheap insurance and ADR-0004 C-B still relies on it for the `FiscalCode == null` case).
- **No double-send across instances** (lease + `SKIP LOCKED`).
- **Ordering is best-effort by `CreatedOn, Id`** within a queue, **not** a strict global order — the
  queues are not order-guaranteed anyway, and every consumer is idempotent and order-independent by
  ADR-0002 design. We do **not** promise strict ordering (it would force single-threaded draining and a
  serialization bottleneck for no consumer benefit).

### D4 — The host: a single dedicated drainer; the Functions host gets the behavior but NOT the drainer (resolves ADR-0002 D1.3) (AC4)

**The drainer runs as ONE dedicated host: a `BackgroundService` (`IHostedService`) co-located with the
APIs' shared infra, OR a single timer-triggered Azure Function — exactly one, not both.** This ADR's
**answer to D1.3**, point by point:

> *Does the Functions worker get the post-commit behavior, the drainer, both, or neither?*

- **Post-commit behavior in the Functions host: YES (unchanged).** The Functions host already wires the
  full MediatR pipeline (`Cleansia.Functions/Program.cs`), so an in-Function command (e.g.
  `CalculateOrderPayFunction` → `CalculateOrderPay.Command`) runs `PostCommitDispatchBehavior` and, in
  Wave-1, **writes outbox rows into its own scoped DbContext commit** — which is **correct**: a side
  effect triggered from inside a Function is just as durable as one from an API. We do **not** strip the
  behavior from the Functions host (stripping it would make in-Function side effects silently
  non-durable — the exact gap we are closing).
- **The drainer in the Functions host: NO — the drainer runs in EXACTLY ONE place, and it is a single
  dedicated host, not "every Functions instance."** Reasoning:
  - The Functions host **scales to N instances**; if each ran a drainer, N drainers would compete (the
    lease + `SKIP LOCKED` makes that *safe*, but it is wasteful and harder to reason about). A **single**
    drainer is the clean model.
  - The "nested outbox surprise" ADR-0002 named is resolved cleanly: the in-Function command **writes** a
    row (producer), and the **one** drainer **sends** it (consumer of the table) — producer and drainer
    are separated, exactly as for API-written rows. There is no nesting because draining is not coupled to
    writing.
- **Recommended concrete host: a single timer-triggered Azure Function (`OutboxDrainerFunction`)** in the
  existing `Cleansia.Functions` host, **guarded so only one instance drains** (a singleton timer / a
  leader lease) — this reuses the existing Functions deployment, the existing `IDeadLetterStore`, and the
  existing reconciliation-timer pattern (`FiscalReconciliationFunction`). A `BackgroundService` in a pinned
  single-instance API host is the acceptable alternative if the team prefers the API process. **T-0157
  (AC8) implements whichever; this ADR fixes "exactly one drainer, the Functions host keeps the behavior
  but is not the per-instance drainer."** The lease (D3) is the safety net if "exactly one" is briefly
  violated.

**This governs T-0157's AC8** (the host decision) per the ticket.

### D5 — Migration path off the in-memory backing (the three serial consumers) (AC5)

ADR-0002 D5 already paid the call-site cost; Wave-1 is a **backing swap + a drainer + Bucket-B**:

- **T-0156 (table + EF):** add `OutboxMessage` entity + EF config + the unique `(QueueName, MessageKey)`
  index + the partial pending index. `manual_step: ef-migration` (owner-only). **No NSwag change** (the
  outbox is internal — no DTO/endpoint surface).
- **T-0157 (backing + drainer + host):** add `OutboxPendingDispatch : IPendingDispatch` (writes the row in
  the scoped DbContext); **swap the DI registration** `InMemoryPendingDispatch` → `OutboxPendingDispatch`
  (one line); add the `PostCommitDispatchBehavior` drainer-nudge; add the single drainer host (D4) with
  claim/lease/backoff/dead-letter (D3). **Zero Bucket-A handler edits, zero consumer edits.**
- **T-0158 (Bucket-B migration):** ADR-0002 D5 Bucket B (sweeps/called-services that loop-and-enqueue
  per-iteration — `AutoCancelStaleRecurringOrders.cs:87`, `SendRecurringOrderReminders.cs`,
  `SendMembershipLifecycleNotifications.cs`, `NewJobsDigestService.cs`, `SendSitewidePromo.cs`,
  `LoyaltyService.cs`) move from their Wave-0 direct-`IQueueClient` carve-out onto the outbox row written
  **inside each per-iteration commit** — the correct shape (each commit drains its own row). The Bucket-C
  fan-out producer (`SendSitewidePromoFanoutFunction.cs`) **stays direct** (ADR-0002 D2.3 — no commit to
  gate; unchanged).

**Cutover safety:** during the swap deploy, in-flight in-memory buffers are request-scoped and drain (or
are discarded) within their own request — there is no in-flight in-memory *durable* state to migrate. The
fiscal reconciliation (D3.4) covers any send lost in the swap window, as it does today.

---

## Alternatives considered

- **Run the drainer in-process via a pipeline behavior (drain synchronously right after commit).**
  Rejected as the *durability* mechanism: that is essentially what Wave-0 already does (in-memory drain
  after commit) and it is **at-most-once** — a crash between commit and the in-process send loses the row.
  The whole point of Wave-1 is to **decouple** sending from the request so a crash re-sends. A post-commit
  *nudge* to the drainer (D2) keeps the latency benefit without coupling durability to the request.
- **Drainer in every Functions instance (no single-host constraint).** Rejected as the default: N
  competing drainers waste work; even though the lease makes it *safe*, "exactly one drainer" is simpler to
  operate and reason about. The lease remains as the safety net for deploy-overlap.
- **Strip `PostCommitDispatchBehavior` from the Functions host (so it writes no rows).** Rejected: it
  would make side effects triggered from inside a Function silently non-durable — reintroducing the gap on
  the in-Function command path. The Functions host **keeps** the behavior (writes rows); it just isn't the
  per-instance drainer.
- **A per-queue outbox table or a column-per-queue.** Rejected: one table with a `QueueName` column is
  simpler, keeps the unique `(QueueName, MessageKey)` dedup uniform, and matches the single-drainer model.
- **Strict global ordering (single-threaded drain).** Rejected: the Azure queues are not order-guaranteed
  and every consumer is idempotent + order-independent by ADR-0002 design; strict ordering buys nothing and
  serializes the drainer. Best-effort `CreatedOn, Id` ordering within a queue is sufficient.
- **`TenantId` in the unique key.** Rejected (same as ADR-0004's S8 exception): `MessageKey` already embeds
  globally-unique ULIDs; adding `TenantId` weakens dedup for null-tenant rows.
- **Drop the D3.4 fiscal reconciliation once the outbox is durable.** Rejected: ADR-0004 C-B still needs it
  for the `FiscalCode == null` claimed-but-unregistered case (a *different* gap from dispatch loss), and it
  is cheap insurance against an outbox bug. Reconciliation becomes a backstop, not the primary net.

---

## Consequences

**Cheaper / safer:**
- Dispatch is **durable and at-least-once** — the ADR-0002 Wave-0 at-most-once gap is **closed** (a crash
  between commit and send re-sends; the downstream consumer dedups on `MessageKey`).
- **Zero Bucket-A handler/consumer churn** — the seam (paid for in Wave-0) makes Wave-1 a backing swap +
  a drainer. The "change command-handler call sites once" promise is honored.
- Two drainer instances cannot double-send (lease + `SKIP LOCKED`); a permanently-unsendable row
  dead-letters through the **existing** ADR-0002 D3 mechanism (no new dead-letter path).
- In-Function side effects become durable too (the Functions host keeps the behavior, writes rows).

**More expensive (new obligations):**
- A new `OutboxMessages` table + unique index → **`manual_step: ef-migration` (owner-only)**, flagged in
  T-0156. **No NSwag change** (internal contract).
- A single drainer host must exist and be guarded "exactly one" (singleton timer / leader lease); the
  lease backs up the guarantee.
- A retention prune for `Dispatched` rows (config-driven).
- The drainer's send is **at-least-once** — consumers MUST stay idempotent (already true per ADR-0002
  D2.2; this ADR adds no new consumer obligation, only relies on the existing one).

**Rollout (strictly serial, per the ticket):** T-0156 (table+EF) → T-0157 (backing swap + drainer +
host, governs D1.3) → T-0158 (Bucket-B onto the outbox). Each test-first.

---

## How a reviewer verifies compliance

**Mechanical (the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **The seam is unchanged.** No command-handler call site changed between Wave-0 and Wave-1 — grep that
   `IPendingDispatch.Enqueue` call sites are identical; only the DI registration
   (`InMemoryPendingDispatch` → `OutboxPendingDispatch`) and the new drainer host differ.
2. **Unique `(QueueName, MessageKey)`** index exists on `OutboxMessages` (EF config) — realizes D1.1.
3. **`Enqueue` writes to the scoped DbContext, not the network** — `OutboxPendingDispatch.Enqueue` does a
   tracked `Add`, no `IQueueClient`/HTTP call.
4. **The drainer claims under a lease** — the claim query uses `FOR UPDATE SKIP LOCKED` (or an equivalent
   atomic claim) and commits the claim before sending; no `SELECT` then unguarded `UPDATE`.
5. **Functions host keeps the behavior, is not the per-instance drainer** — `PostCommitDispatchBehavior`
   is registered in `Cleansia.Functions/Program.cs` (writes rows); the drainer is a single guarded host,
   not one-per-instance.

**Test contract (T-0157, red first):**
6. **TC-OUTBOX-ATOMIC-0.** `Enqueue` + a failing commit → **no** outbox row (atomic with business state);
   `Enqueue` + a successful commit → exactly **one** row (double-`Enqueue` same key → one row, D1.1).
7. **TC-OUTBOX-DRAIN-0.** A `Pending` row is claimed, sent via `AzureStorageQueueClient`, marked
   `Dispatched`; a crash after the Azure send but before the mark → re-claim re-sends (at-least-once),
   and the downstream consumer dedups on `MessageKey` (exactly-one effect — reuses ADR-0002 TC-IDEMP-0).
8. **TC-OUTBOX-LEASE-0.** Two concurrent drainer claims never grab the same row (`SKIP LOCKED`).
9. **TC-OUTBOX-DEADLETTER-0.** A row exceeding max attempts → `Status = Failed` + a `DeadLetter` row +
   alert (existing ADR-0002 D3 store).

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`pending-dispatch.md` (existing, ADR-0002)** — updated: the Wave-1 backing (`OutboxPendingDispatch`)
  now *records as an outbox row in the pipeline's DbContext* (the ADR-0002 role file already anticipated
  this). *Still does NOT know* whether/when the commit happened or how a row reaches Azure.
- **`outbox-drainer.md`** (new) — *responsibility:* claim `Pending` outbox rows under a lease, send each
  via `AzureStorageQueueClient`, mark `Dispatched`, retry/backoff on failure, and dead-letter a
  permanently-unsendable row. *Collaborators:* `OutboxMessage` repo, `AzureStorageQueueClient`,
  `IDeadLetterStore`. *Does NOT know:* message contents/meaning, who enqueued the row, consumer behavior,
  or any domain rule. (This separation is why the in-Function producer + single drainer has no nesting —
  the drainer is purely a row→wire mover.)
- **`outbox-message.md`** (new, entity CRC) — *responsibility:* be the durable record of one intended
  post-commit send, unique per `(QueueName, MessageKey)`. *Collaborators:* the scoped `DbContext`, the
  drainer. *Does NOT know:* how it is sent or what the body means.

Catalog edit (same change): `agents/knowledge/patterns-backend.md` records the outbox as the durable
backing of the `IPendingDispatch` seam (the handler-facing rule is unchanged — ADR-0002 still owns it).

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (distributed-systems, pragmatic, test-architecture) attacked; the Lead
re-verified every citation against the real code and ADR-0002, and adjudicated. **Verdict: all challenges
RESOLVED; zero blocking; does NOT contradict ADR-0002; consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 | "Drainer as a pipeline behavior" (the ticket's first D1.3 option) would still be **at-most-once** (crash between commit and in-process send) — defeats the whole Wave-1 purpose (CRITICAL) | CONCEDE + REVISE | D2/D4 decouple sending from the request; the behavior only *nudges*; the drainer is the authoritative at-least-once sender |
| CH-2 | Multiple drainer instances (every Functions instance) → double-send (MAJOR) | CONCEDE + REVISE | D3 lease + `FOR UPDATE SKIP LOCKED`; D4 single dedicated drainer (lease as the safety net) |
| CH-3 | Stripping the behavior from the Functions host would make in-Function side effects silently non-durable (MAJOR) | DEFEND | D4 — Functions host KEEPS the behavior (writes rows), just is not the per-instance drainer; resolves the D1.3 nesting cleanly |
| CH-4 | Does this contradict ADR-0002's "zero call-site churn" or its at-most-once framing? (MAJOR — cross-ADR) | DEFEND | D2/D5 — zero Bucket-A churn (DI swap only); the at-most-once gap is now *closed*, which ADR-0002 explicitly said Wave-1 would do — backing, not contradicting |
| CH-5 | `TenantId` not in the unique key — S8 tenant-scoped-index rule violated? (MODERATE) | DEFEND | D1 reasoned S8 exception (same as ADR-0004 `IX_OrderReceipts_OrderId`); `MessageKey` embeds globally-unique ULIDs; recorded greppably |
| CH-6 | Crash after Azure send, before `Dispatched` mark → double-send (MAJOR) | DEFEND | D3 at-least-once is intended; the downstream consumer dedups on `MessageKey` (ADR-0002 D2.2) → exactly-one effect |
| CH-7 | Does the outbox make D3.4 fiscal reconciliation dead code? (MODERATE) | DEFEND | D3 — reconciliation stays as backstop; ADR-0004 C-B still needs it for `FiscalCode == null` (a different gap) |
| CH-8 (test) | "Drainer test" must prove the lease + at-least-once, not just a happy send (MODERATE) | CONCEDE + REVISE | TC-OUTBOX-LEASE-0 + TC-OUTBOX-DRAIN-0 (crash-then-redeliver) |

**Affirmed unchallenged:** one `OutboxMessages` table with a `QueueName` column; unique `(QueueName,
MessageKey)` realizing D1.1; the DI-swap-only backing change; best-effort (not strict) ordering;
reusing the existing `IDeadLetterStore`/reconciliation rather than inventing new ones.

**Lead re-verification (against current code + ADR-0002):** `IPendingDispatch.cs` frozen seam +
in-/Wave-1 backing note; `InMemoryPendingDispatch` is the Wave-0 backing
(`Cleansia.Infra.Azure.Storage.Queues`); `PostCommitDispatchBehavior.cs` drains post-commit;
`AzureStorageQueueClient.cs:14-27` the unchanged sender; `UnitOfWorkPipelineBehavior.cs:19-20` the commit
the row rides on; `QueueNames.cs:5-9` the five queues; `Cleansia.Functions/Program.cs:46-74` full pipeline
+ all `-poison` handlers + `FiscalReconciliationTimerHandler` already deployed (so the D3 dead-letter +
D3.4 reconciliation this ADR reuses are real, not aspirational); ADR-0002 D1.1/D1.3/D5 deferral clauses
verbatim.

**Escalations to the owner:** none. The single new table is an owner-only `ef-migration` (flagged, not a
decision); drainer batch size / backoff / retention cadence are operator-tunable config; the host choice
(timer Function vs `BackgroundService`) is an implementer decision within "exactly one drainer."
