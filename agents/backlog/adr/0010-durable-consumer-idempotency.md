# ADR-0010 — Durable consumer idempotency: DB-backed `IIdempotencyGuard` and `ICampaignProgressStore` (the in-memory backings made durable, no interface change)

- **Status:** proposed   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-09
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | functions | cross-cutting
- **Backs / extends:** ADR-0002 (D2.2 idempotent consumers, D2.3 the promo resume cursor) and ADR-0008 (mirrors the `OutboxMessage` unique-row + DI-swap shape). Does **not** change either frozen contract.
- **Ticket:** T-0186 (this ADR) · **Consumer:** the two backings + EF configs + DI swap + tests (one ticket, the interfaces are unchanged so there is no call-site fan-out)

> **One decision:** make the two process-local consumer-dedup backings **durable (DB rows)**, mirroring
> the `ProcessedStripeEvent` at-most-once idempotency-row precedent **exactly**. The interfaces
> (`IIdempotencyGuard`, `ICampaignProgressStore`) and every consumer call site stay **byte-for-byte
> unchanged** — both in-memory docs already promised "a durable backing closes the gap with no change to
> this interface." Once `accepted` this is immutable — supersede, never edit.

---

## Context

T-0182 makes `IIdempotencyGuard.AlreadyProcessedAsync` the **load-bearing at-most-once control** for
transactional push (the claim is the marker; the FCM send rides after it). The production registration is
**`InMemoryIdempotencyGuard`** (`src/Cleansia.Infra.Azure.Storage.Queues/InMemoryIdempotencyGuard.cs`) — a
`ConcurrentDictionary` — and the promo resume cursor is **`InMemoryCampaignProgressStore`** (same
assembly). Both are registered **singleton** in `QueueExtensions.cs:36,41` and are therefore
**process-local**: a claim does not survive a worker **restart** and is not shared across **scaled-out**
worker instances. With the guard now load-bearing, that means **duplicate pushes after any restart/scale
event** — unacceptable for production. The owner's decision: **make both durable now (DB-backed)**.

The platform already has the exact pattern to mirror — the at-most-once **idempotency-row**:
- `ProcessedStripeEvent` (`src/Cleansia.Core.Domain/Payments/ProcessedStripeEvent.cs`) — `BaseEntity`,
  one unique business key (`StripeEventId`), static `Create`, **tenant-global** by design.
- Its EF config (`…/EntityConfigurations/ProcessedStripeEventEntityConfiguration.cs`) — the **UNIQUE
  index on the key is the load-bearing constraint**; a parallel-retry race surfaces as PG **23505**.
- Its repo (`…/Repositories/ProcessedStripeEventRepository.cs` + `IProcessedStripeEventRepository`) —
  `IgnoreQueryFilters()` belt-and-braces on the cross-tenant read.
- The webhook handler converts a `DbUpdateException`(23505) into an "already processed" **success**.

A second precedent is `DeadLetterStore` (`src/Cleansia.Infra.Database/DeadLetterStore.cs`): a
**consumer-side store that owns its own commit** because the `<queue>-poison` consumer has no MediatR
`UnitOfWork` behavior wrapping it. The idempotency claim has the **same** shape — it must commit in its
**own** unit of work so the marker persists even if the terminal effect later crashes (at-most-once *after
the marker*).

This is **one decision** because the entity shape, the unique index, and the "claim commits in its own
scope" algorithm are inseparable: the index *is* the dedup, and the own-scope commit *is* what makes the
marker survive a post-marker crash.

---

## Decision

> **Mirror `ProcessedStripeEvent` exactly.** Two additive tables, each a `BaseEntity` with a unique
> business key, tenant-global (these are consumer-dedup rows, not tenant data). The backings move from
> singleton in-memory to **scoped** DB-backed (they need a `DbContext`); the interfaces and consumers do
> not change. A migration adds the two tables + their unique indexes (`manual_step: ef-migration`).

### D1 — `ProcessedMessage` entity (the durable `IIdempotencyGuard` backing)

`Cleansia.Core.Domain` (a `Queue`/`Messaging` folder, sibling to `Payments/ProcessedStripeEvent`):

| Field | Type | Notes |
|---|---|---|
| `Id` | string PK (ULID, max 26) | `BaseEntity`, as `ProcessedStripeEvent` |
| `MessageKey` | string, `[Required] [MaxLength(256)]` | the deterministic ADR-0002 D2.1 claim key (e.g. `push:{UserId}:{EventKey}:{Subject}`, `email:{Type}:{UserId}:{hash}`). **UNIQUE.** 256 covers the longest composed key with headroom |
| `ProcessedAt` | `DateTime` (UTC) | audit only — when the claim row was written |

`static ProcessedMessage Create(string messageKey)` → sets `MessageKey`, `ProcessedAt = DateTime.UtcNow`.
Mirrors `ProcessedStripeEvent.Create` (private setters, no public mutation).

**No `EventType` field.** `ProcessedStripeEvent` keeps `EventType` because the Stripe key (`evt_…`) is
opaque; our `MessageKey` is **self-describing** (its prefix already encodes the effect — `push:`,
`email:`), so a separate audit column would be redundant. Keep the row minimal. (If ops later want a
breakdown, derive it from the `MessageKey` prefix — no schema change.)

**Tenant-global (NOT `ITenantEntity`)** — same reasoning as `ProcessedStripeEvent` (recorded in its
doc-comment, lines 11-12): the queue consumer runs with **no JWT/tenant context** (it `SetTenantOverride`s
*after* the claim, see `SendPushNotificationHandler.cs:89` is *before* `:102`); the `MessageKey` already
embeds globally-unique ULIDs, so a `(TenantId, MessageKey)` index would only **weaken** dedup for
null-tenant rows. This is the **same reasoned S8 exception** ADR-0004/ADR-0008 recorded — call it out in
the entity doc-comment and the EF config so the S8 grep (`security-rules.md §S8`, "unique indexes on
tenant-scoped tables are `(TenantId, X)`") finds the justification and does not "fix" it.

### D2 — `DbIdempotencyGuard.AlreadyProcessedAsync` (claim-in-own-transaction)

```
Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct):
    repository.Add(ProcessedMessage.Create(messageKey))   // tracked insert on a FRESH scope's DbContext
    try:
        await repository.CommitAsync(ct)                  // its OWN unit of work
        return false                                       // WON the claim → caller proceeds
    catch (DbUpdateException e) when isUniqueViolation(e): // PG 23505 on MessageKey
        return true                                        // ALREADY claimed → caller acks, no re-send
```

- **Commits in its OWN unit of work, NOT the consumer's.** The push consumer's only commit is the
  dead-token prune (`SendPushNotificationHandler.cs:167`), which happens *after* the FCM send and only
  when there are invalid tokens — the claim must **not** wait on that. The guard owns its commit exactly
  like `DeadLetterStore.RecordAsync` (`DeadLetterStore.cs:24`). This realizes the interface's stated
  guarantee — **at-most-once after the marker**: the claim row is durable *before* the terminal effect, so
  a crash between claim and FCM-send loses that one push (accepted — `IIdempotencyGuard.cs:10-12`), and a
  redelivery short-circuits.
- **`return true` on 23505 only** — the same unique-violation→success conversion the webhook handler does.
  Any other `DbUpdateException` re-throws (the consumer's `catch` classifies it transient and the queue
  retries — `SendPushNotificationHandler.cs:175-187`). Use the existing PG-23505 detection helper the
  webhook path uses; do **not** hand-roll SQLSTATE parsing.
- **How it gets its own `DbContext`/scope:** the backing is registered **scoped** (D5), and the consumer
  resolves it **per-invocation** from the Functions request scope — so each `AlreadyProcessedAsync` runs
  on the **consumer invocation's** scoped `DbContext`. That is acceptable here *because the guard owns its
  own `CommitAsync`*: the claim is flushed immediately and independently of any later effect on the same
  context. (If a future consumer ever shared one context across claim **and** a deferred business commit
  such that the claim could be rolled back by a later failure, the guard MUST open a fresh scope — note
  this in the role file. Today no consumer does: the push consumer's only other commit is the post-send
  prune, the email consumer has no DB commit at all.)

### D3 — `CampaignProgress` entity + `DbCampaignProgressStore`

`CampaignProgress` (same `Queue`/`Messaging` folder), one row per campaign:

| Field | Type | Notes |
|---|---|---|
| `Id` | string PK (ULID) | `BaseEntity` |
| `CampaignId` | string, `[Required] [MaxLength(128)]` | **UNIQUE** — one progress row per campaign |
| `LastProcessedUserId` | string?, `[MaxLength(26)]` | the resume cursor; null before the first page completes (matches `CampaignProgress` record) |
| `IsComplete` | bool | terminal flag |

`DbCampaignProgressStore` maps the three interface methods to one row:
- **`GetAsync(campaignId)`** → read the row (cross-tenant, `IgnoreQueryFilters()`); if none, return
  `new CampaignProgress(null, false)` (the in-memory default at `InMemoryCampaignProgressStore.cs:23-25`).
- **`AdvanceAsync(campaignId, lastUserId)`** → **upsert**: find row by `CampaignId`; if present set
  `LastProcessedUserId = lastUserId`; else `Add` a new row with that cursor and `IsComplete = false`.
  Commit in its **own** unit of work. The `CampaignId` UNIQUE index makes a concurrent first-advance race
  surface as 23505 → re-read and update (or simply retry) — but unlike the guard, **the existing race
  residual is a benign re-cost, never a duplicate effect** (the downstream `push:` guard is the effect
  control — `ICampaignProgressStore.cs:5-7,16-18`). A simple find-or-insert-then-update upsert is
  sufficient; this is a **Bucket-C cost layer**, not an at-most-once control.
- **`MarkCompleteAsync(campaignId)`** → upsert the row with `IsComplete = true`. Own unit of work.

**Tenant-global (NOT `ITenantEntity`)** — same reasoning as D1: the fan-out consumer has no JWT, and
`CampaignId` is globally unique. Record the S8 exception in the entity + EF config.

### D4 — Migration delta (for the owner — `manual_step: ef-migration`)

Two **additive** tables, no changes to existing tables, no data backfill (the in-memory state was
ephemeral):

1. **`ProcessedMessages`** — `Id` (PK, varchar 26), `MessageKey` (varchar 256, NOT NULL),
   `ProcessedAt` (timestamptz NOT NULL). **`UNIQUE INDEX (MessageKey)`** — load-bearing.
2. **`CampaignProgresses`** — `Id` (PK, varchar 26), `CampaignId` (varchar 128, NOT NULL),
   `LastProcessedUserId` (varchar 26, NULL), `IsComplete` (bool NOT NULL default false).
   **`UNIQUE INDEX (CampaignId)`**.

Neither table is `ITenantEntity`, so no `TenantId` column and no global-filter wiring. **No NSwag change**
(internal — no DTO/endpoint surface).

### D5 — DI: replace the two in-memory singletons with scoped DB-backed registrations

The in-memory backings are **singleton** in `QueueExtensions.cs:36,41`. The DB-backed ones need a
`DbContext`, which is **scoped** — so they MUST be **scoped**, not singleton. Mirror exactly how the
`OutboxPendingDispatch` swap is done (`RepositoryExtensions.AddRepositories`, `RepositoryExtensions.cs:27`):
register the durable scoped backings in `AddRepositories` (which runs **after** `AddAzureStorageQueues`
and whose implementations live in `Cleansia.Infra.Database`), **and remove** the two `AddSingleton` lines
from `QueueExtensions.cs` so there is no singleton-then-scoped ambiguity:

```
// RepositoryExtensions.AddRepositories (next to the IPendingDispatch swap):
services.AddScoped<IIdempotencyGuard, DbIdempotencyGuard>();
services.AddScoped<ICampaignProgressStore, DbCampaignProgressStore>();

// QueueExtensions.cs: DELETE the two AddSingleton<…, InMemory…> lines (36, 41).
```

The consumer resolves both **per-invocation** from the Functions request scope (it already does — the
handlers take them as ctor deps: `SendPushNotificationHandler` line 35, `SendEmailHandler` line 24). The
two `InMemory*` classes can be retained as the test/in-memory double for unit tests but are **no longer
registered in production**.

### D6 — No interface change, no consumer logic change

`IIdempotencyGuard` and `ICampaignProgressStore` are **unchanged**. `SendPushNotificationHandler`,
`SendEmailHandler`, and the promo fan-out consumer are **unchanged** beyond DI resolution — they already
call the interfaces (`SendPushNotificationHandler.cs:89`, `SendEmailHandler.cs:56`). The only diff is the
DI registration (D5) plus the two entities/configs/backing classes. This is the same "backing swap, zero
call-site churn" shape ADR-0008 delivered for the outbox.

---

## Alternatives considered

- **Keep in-memory (do nothing).** Rejected by the owner: with T-0182 the guard is the load-bearing
  at-most-once control, so process-local state means duplicate pushes after every restart/scale event.
- **Redis / distributed cache for the claim.** Rejected: adds a new infra dependency and a second
  durability story; the DB + a unique index is the pattern already in the codebase (`ProcessedStripeEvent`),
  is transactional with PG semantics we already rely on (23505), and needs no new component.
- **Commit the claim on the consumer's UnitOfWork.** Rejected: the consumer has no MediatR `UnitOfWork`
  behavior (it's a queue trigger, like the poison consumers — `DeadLetterStore` had the same problem), and
  tying the claim to a later business commit would let a post-claim failure roll the claim back, breaking
  at-most-once-after-marker. The guard owns its commit, like `DeadLetterStore`.
- **`(TenantId, MessageKey)` / `(TenantId, CampaignId)` unique indexes (S8 literal).** Rejected: the
  consumer has no tenant context at claim time and the keys embed globally-unique ULIDs; a tenant-scoped
  index would weaken dedup for null-tenant rows. Reasoned S8 exception, same as ADR-0004/ADR-0008.
- **Add an `EventType`/audit column to `ProcessedMessage` (full `ProcessedStripeEvent` parity).**
  Rejected: our `MessageKey` is self-describing (prefix encodes the effect); the Stripe column existed only
  because `evt_…` is opaque. Keep the dedup row minimal.

---

## Consequences

**Cheaper / safer:**
- The push at-most-once guarantee (T-0182) now holds **across restart and scale-out** — the production
  duplicate-push gap is closed. The promo resume cursor survives the same events (no whole-base re-cost on
  a restart mid-fan-out).
- Zero interface/consumer churn — a backing swap + two tables, mirroring `ProcessedStripeEvent` and the
  ADR-0008 outbox swap. The pattern is now uniform across all three consumer-side durable stores
  (`OutboxMessage`, `DeadLetter`, `ProcessedMessage`/`CampaignProgress`).

**More expensive (new obligations):**
- Two new tables + unique indexes → **`manual_step: ef-migration` (owner-only)**. **No NSwag change.**
- Two `ProcessedMessages`/`CampaignProgresses` rows accrue per processed message/campaign — add a
  retention prune for `ProcessedMessages` (config-driven, e.g. rows older than the max queue
  redelivery/visibility window, generously 7-30 days) the same way `Dispatched` outbox rows are pruned
  (ADR-0008). `CampaignProgresses` is one row per campaign — negligible, no prune needed.
- The backings are now **scoped, not singleton** — anyone reading `QueueExtensions.cs` must find them in
  `AddRepositories`; D5 deletes the singleton lines so there is no stale registration to mislead.

---

## How a reviewer verifies compliance

**Mechanical:**
1. `ProcessedMessage` and `CampaignProgress` are `BaseEntity`, **not** `ITenantEntity`; their EF configs
   have a **UNIQUE** index on `MessageKey` / `CampaignId` respectively, each with an inline S8-exception
   comment (so the S8 grep passes).
2. `QueueExtensions.cs` no longer registers `InMemoryIdempotencyGuard` / `InMemoryCampaignProgressStore`;
   `AddRepositories` registers `DbIdempotencyGuard` / `DbCampaignProgressStore` as **`AddScoped`** (grep:
   no `AddSingleton<IIdempotencyGuard` / `AddSingleton<ICampaignProgressStore`).
3. `IIdempotencyGuard.cs` and `ICampaignProgressStore.cs` are **byte-for-byte unchanged**; the three
   consumer handlers' bodies are unchanged (diff shows only ctor DI resolution, no logic).
4. `DbIdempotencyGuard.AlreadyProcessedAsync` does `Add` → its **own** `CommitAsync` → `return false`;
   catches the unique violation (the shared PG-23505 helper) → `return true`; re-throws any other
   `DbUpdateException`. No reliance on the consumer's commit.

**Test contract (red first):**
5. **TC-IDEMP-DURABLE-0.** First `AlreadyProcessedAsync(key)` returns `false` and persists a row; a second
   call for the same key (simulating a redelivery on a *fresh* scope / "after restart") returns `true`
   and sends nothing — proving the claim survives the process boundary (the in-memory backing would
   forget it).
6. **TC-IDEMP-RACE-0.** Two concurrent claims of the same key on separate scopes → exactly one `false`,
   one `true` (the loser's 23505 → `true`), mirroring webhook TC.
7. **TC-CAMPAIGN-DURABLE-0.** `AdvanceAsync` then `GetAsync` on a fresh scope returns the persisted
   cursor; `MarkCompleteAsync` then `GetAsync` returns `IsComplete = true`; `GetAsync` for an unknown
   campaign returns `(null, false)`.

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`idempotency-guard.md`** (new, CRC) — *responsibility:* atomically claim a deterministic `messageKey`
  by inserting a unique `ProcessedMessage` row in **its own** committed unit of work, returning whether the
  key was already claimed. *Collaborators:* `ProcessedMessage`, the `ProcessedMessage` repo, the scoped
  `DbContext`. *Does NOT know:* what the message is, the terminal effect, who the consumer is, the tenant,
  or the consumer's commit (it owns its own). **Watch-list note:** if a future consumer ever shares one
  `DbContext` across the claim and a deferrable business commit that could roll the claim back, the guard
  must open a fresh scope — today none does.
- **`campaign-progress-store.md`** (new, CRC) — *responsibility:* persist + read the per-campaign resume
  cursor + completion flag, one row per `CampaignId`. *Collaborators:* `CampaignProgress`, its repo, the
  scoped `DbContext`. *Does NOT know:* recipient identity beyond the cursor `UserId`, the effect (the
  downstream `push:` guard owns dedup), or the tenant.
- **`processed-message.md`** (new, entity CRC) — *responsibility:* be the durable, unique-per-`MessageKey`
  record that an effect was claimed. *Collaborators:* the scoped `DbContext`, the guard. *Does NOT know:*
  what was sent or what the key means.

Catalog edit (same change): `agents/knowledge/patterns-backend.md` — record that the **consumer-side
durable idempotency-row** (`ProcessedStripeEvent` / `ProcessedMessage`) is the canonical at-most-once
control for a non-transactional consumer effect: a `BaseEntity` with a UNIQUE business key, claimed in the
consumer's **own** committed unit of work, converting PG 23505 into "already processed." Cross-references
the S8 reasoned exception (`security-rules.md §S8`).

---

## Note on the sibling fixes shipped alongside (NOT part of this ADR's decision)

T-0186's batch also carries fixes the Reviewer flagged; recorded here only so they are not lost, but they
are separate from the durability decision above:
- **T-0171 / AC5 host-split.** Remove the **mutation** endpoints (Create/Update/Delete/Open/Close) from
  `Cleansia.Web.Partner/Controllers/PayPeriodController.cs` (lines 39-97), leaving only the read endpoints
  (`GetPagedPayPeriods`, `GetPayPeriodById`). The full write surface already lives on
  `Cleansia.Web.Admin/Controllers/AdminPayPeriodController.cs` (incl. close). Add a route-gone test
  (POST/PUT/DELETE to the old Partner routes → 404/405). This honors the per-audience-host seam
  (`security-rules.md §S2`, authorization "holds regardless of which API host"): a write surface that
  belongs to Admin must not exist on the Partner host even if AdminOnly-gated.
- **T-0182 disabled-vs-transient.** `PushDispatchResult`/`IPushDispatcher` must signal "disabled/skipped"
  **distinctly** from "all-failed-transient" so `SendPushNotificationHandler` (`:147-155`) **acks** the
  FCM-disabled no-op (the documented dev/CI behavior — `FcmPushDispatcher.cs:51-56`) while still
  **throwing** on the genuine cold-start init race (`FcmPushDispatcher.cs:85`, the broad-catch all-failed).
  Add a `Skipped`/`Disabled` signal to the result shape (e.g. a `bool Skipped` or a `DispatchOutcome`
  enum); the handler acks when skipped, throws on all-failed-with-no-prunable-token. Add the disabled-ack
  test. (The result-shape change is a small contract decision; if the implementer wants it ADR-pinned,
  raise it — it is mechanical enough to ride this batch.)
- **NITs.** T-0181/T-0182 status logs need a red→green line; `OutboxMessageRepository.GetByQueueAndKeyAsync`
  (`OutboxMessageRepository.cs:18-24`, a pure existence read) should add `AsNoTracking()`.
