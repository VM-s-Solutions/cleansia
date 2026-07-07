# Side-effect dispatch & the outbox (living design note)

> Companion to the **immutable** ADR-0002 (`agents/backlog/adr/0002-outbox-dispatch-contract.md`).
> The ADR is the frozen contract; this file is the *evolving* design note — the trade-off space, the
> current shape, and the open Wave-1 questions. When Wave-1 (F2-FULL) lands, update this file in the
> same step (per `agents/process/deliberation.md`).

## The problem in one sentence

Every external effect (receipt PDF + email, invoice, push, sitewide promo, pay calc) is triggered by
enqueuing to one of five Azure Storage Queues, and today the **enqueue fires before the DB commit** and
**consumers are not safe to run twice** — so we both lose work (commit fails after a message is on the
wire → phantom effect; or a message fails 5× → silently poison-lost) and duplicate it (redelivery or
duplicate-enqueue → second receipt/push).

## The contract (frozen — ADR-0002)

A command handler **records intent**; it never sends to a queue directly. Intent is **realized after**
the owning UnitOfWork commit. Two *backings* of the same handler-facing seam:

```
Handler ──pending.Enqueue(queue, msg, key)──▶ IPendingDispatch ──drain──▶ PostCommitDispatchBehavior ──▶ IQueueClient ──▶ Azure Queue ──▶ Consumer (idempotent)
                                                  │
                          Wave-0: in-memory buffer (at-most-once dispatch)
                          Wave-1: outbox row in the SAME DbContext (at-least-once, atomic with state)
```

```
Pipeline order (after D4 reorder), outer → inner:
  PostCommitDispatchBehavior   → drains & dispatches AFTER the inner pipeline returns a committed success
    ValidationPipelineBehavior → rejects before the write boundary (closes F11)
      UnitOfWorkPipelineBehavior → commits a validated command (innermost write boundary)
        Handler                → pending.Enqueue(...)
```

Four load-bearing invariants:
1. **Post-commit dispatch** — a message is on the wire only after the row is durable (Wave-0) / written
   atomically with it (Wave-1). Realized via the **outermost pipeline behavior**, not an
   `IStartupFilter` (wrong layer) and not an in-handler call (handler doesn't own the commit).
2. **Deterministic `MessageKey` per logical effect** — a *duplicate enqueue* collapses onto the same
   key as a *redelivery*. (Random Guid would dedup redeliveries but not duplicate enqueues.)
3. **Idempotent consumers** — target-state check (preferred) or the durable `IIdempotencyGuard` /
   `ProcessedMessage` backstop (ADR-0010) for non-transactional effects (push/promo/email). *When*
   the marker is written is per-consumer (ADR-0023): claim-**before**-act (Mode A, at-most-once —
   mandatory for non-repeatable/money-shaped effects) or claim-**after**-successful-act (Mode B,
   at-least-once — permitted where a duplicate is benign; today: email only). See the claim-ordering
   section below.
4. **Poison/dead-letter floor + fiscal reconciliation** — every queue has a `<queue>-poison` consumer
   (durable `DeadLetter` row + alert); the two fiscal queues add a reconciliation sweep for the
   never-enqueued case.

## Honest guarantee table (do not collapse this)

| Step | Wave-0 | Wave-1 |
|---|---|---|
| Handler → buffer/outbox | in-memory, cleared on non-commit | outbox row, atomic with state |
| Buffer/outbox → wire | **at-most-once** (crash between commit & drain loses it) | **at-least-once** (drainer) |
| Consumer effect (receipt create, pay row) | exactly-once (target-state) | exactly-once |
| Consumer effect (push — non-transactional, Mode A) | **at-most-once after marker** (guard-first) | same |
| Consumer effect (email — non-transactional, Mode B since ADR-0023) | **at-least-once** (claim-after-successful-send; rare duplicate accepted, loss impossible short of dead-letter) | same |
| Never-enqueued silent loss (fiscal) | **detected + re-enqueued** by reconciliation (default 15 min) | gap removed by durable outbox |

Wave-0 is unambiguously better than today on the silent-loss axis (it replaces a *silent* phantom-and-
poison-loss with a *detected, re-enqueued* gap on the sensitive queues) — but it is **not** "the hole
is closed." That is Wave-1.

## MessageKey formulas (frozen — changing one needs a superseding ADR)

| Queue | Key | Dedups |
|---|---|---|
| `generate-receipt` | `receipt:{OrderId}` | one receipt per order |
| `generate-invoice` | `invoice:{PayPeriodId}:{EmployeeId}` | one invoice per employee per period |
| `notifications-dispatch` | `push:{UserId}:{EventKey}:{OrderId?}` | one push per user per event per subject |
| `calculate-order-pay` | `pay:{OrderId}:{EmployeeId}` | one pay row per order per cleaner |
| `sitewide-promo-fanout` | — (producer; dedup happens downstream on the push key) | — |

## Call-site map (verified by grep — 21 sends / 14 AppServices files + 1 in-Function producer)

- **Bucket A — command handlers (migrate to `IPendingDispatch` once, in Wave-0; 14 sends):**
  `CreateOrder:376`, `CompleteOrder:219,227,266`, `ConfirmRecurringOrder:112,118`, `CancelOrder:160`,
  `TakeOrder:195`, `StartOrder:137`, `NotifyOnTheWay:103`, `AddDisputeMessage:67`,
  `HandlePaymentNotification:241,246,278`.
- **Bucket B — sweeps & called-services (keep direct in Wave-0 under a documented carve-out; move to
  per-iteration outbox in Wave-1; 7 sends):** `AutoCancelStaleRecurringOrders:87`,
  `SendRecurringOrderReminders:77`, `SendMembershipLifecycleNotifications:87,125`,
  `NewJobsDigestService:170`, `SendSitewidePromo:88`, `LoyaltyService:75`. They loop and commit
  per-iteration; a request-scoped buffer drained once is the wrong shape.
- **Bucket C — in-Function producer (no commit to gate, stays direct):**
  `SendSitewidePromoFanoutFunction:123` (D2.3).

## Consumer status (as of this decision)

| Consumer | Idempotent today? | Wave-0 obligation |
|---|---|---|
| `GenerateReceiptFunction` | receipt *creation* yes (`:66-70`); **email re-send no** (`:95` before commit `:99`) | close the email window (claim-first or accept+document) |
| `CalculateOrderPayFunction` | yes — validator rejects already-calculated → ack (`:55-65`) | none (compliant) |
| `GenerateInvoiceFunction` | n/a — no-op stub (`:20-26`) | poison consumer only; guard lands *with* the effect |
| `SendPushNotificationFunction` | **no guard** (`:30-122`); throws on everything incl. deserialize (`:115-121`); commit is conditional (`:100-108`) | guard-first `IIdempotencyGuard`; split permanent/transient |
| `SendSitewidePromoFanoutFunction` | n/a — producer | none (downstream dedup); already continues per-recipient (`:137-146`) |

## Claim ordering is per-consumer (ADR-0023, 2026-07-08 — the current shape)

The durable dedup (ADR-0010: `ProcessedMessage` unique-row, own-unit-of-work commit) is uniform;
**when the row is written is not.** The SendGrid config-gap incident proved claim-before-send is the
wrong ordering for email: the durable claim committed, the send threw, and every queue retry
short-circuited on the claim and acked green — confirmation/reset emails were **permanently lost while
telemetry showed "already sent, skipping"**. The owner ruled (2026-07-08):

- **Mode A — claim-before-act** (at-most-once after the marker) stays **mandatory** where a duplicate
  effect is not safely repeatable: receipt/invoice generation, pay calculation, fiscal registration —
  anything money-shaped. Residual: a crash between claim and act loses that one effect.
- **Mode B — claim-after-successful-act** (at-least-once) is **permitted** where a duplicate is benign.
  Today: **the send-email consumer only** — non-claiming `HasProcessedAsync` pre-check → send →
  `MarkProcessedAsync` post-success (23505 benign; other claim-write failures logged + acked by the
  handler). A failed send leaves no row → the retry is real. Residual: rare duplicates in two windows
  (concurrent redeliveries both passing the pre-check; a crash between send-success and claim-write) —
  owner-accepted. The `email:` row now means *sent*, not *attempted*.
- **The boundary is one question** (the repeatable-effect test): would a second run need un-doing?
  Yes → Mode A. Nuisance at worst → Mode B permitted, ratified per consumer (ADR or ticket decision
  note citing ADR-0023). **Push is a candidate for Mode B — explicitly not decided;**
  `SendPushNotificationHandler` remains guard-first and untouched.

The mode is greppable at the call site (three named `IIdempotencyGuard` members, no boolean flag).
Full rule + rationale: `agents/backlog/adr/0023-per-consumer-claim-ordering-email-claims-after-successful-send.md`;
catalog entry: `agents/knowledge/patterns-backend.md` ("Queue-consumer idempotency").

## Why the rejected options were rejected

- **Enroll the queue/FCM in the EF transaction** — they are not transactional resources; a send can't
  roll back. Only *send-after-commit* or *store-then-send* are sound.
- **Idempotency only** — stops double-processing but not the *phantom* (message for a never-committed
  row). Post-commit removes the phantom at source.
- **Skip the in-memory buffer, do the DB outbox in Wave-0** — that *is* Wave-1; needs the table, an
  owner-only migration, and a drainer. The buffer is the bridge; reconciliation is the honesty.
- **Random Guid message id** — defeats the duplicate-enqueue case (each enqueue gets a new id).
- **Dispatch failure → 500** — fails an already-committed customer operation (violates the
  fiscal-compliance "customer completion is never blocked by a downstream effect" invariant).

## F11 (fixed here as part of the same structural change)

`FluentValidationExtensions.cs:13-14` registered `UnitOfWork` *outer* to `Validation`, and
`UnitOfWorkPipelineBehavior.cs:19-20` commits **unconditionally** after `next()` — so a command that
failed validation still committed. Fix: reorder to Validation-outer-to-UoW (validation returns the
failure result *without* calling `next()`, so UoW never runs) **plus** a defense-in-depth
`commit only on BusinessResult { IsSuccess: true }` check in UoW so a future re-swap can't resurrect it.

## Open questions for Wave-1 (F2-FULL — its own ADR)

1. **Outbox table shape** — columns, the dedup unique index on `(QueueName, MessageKey)`, retention.
2. **Drainer delivery & locking** — poll vs. `LISTEN/NOTIFY`, lease/visibility, ordering, batch size,
   how it nudges from `PostCommitDispatchBehavior`.
3. **In-Functions-host drainer (D1.3)** — the Functions worker runs the full pipeline, so a command
   invoked inside a Function writes outbox rows too. Decide whether the Functions host gets the
   post-commit behavior, the drainer, both, or neither — to avoid a nested-outbox surprise.
4. **Bucket-B migration** — move sweeps/called-services to the per-iteration outbox row (the shape
   that fits their commit-as-they-go loop) and retire the Wave-0 direct-send carve-out.
5. **Retire the D2.1a dual-read** once old bare-payload messages have drained.

## Pointers

- Immutable contract: `agents/backlog/adr/0002-outbox-dispatch-contract.md`
- Durable consumer dedup: `agents/backlog/adr/0010-durable-consumer-idempotency.md` (partially
  superseded by ADR-0023 for the email claim ordering + the interface-frozen invariant)
- Claim-ordering rule: `agents/backlog/adr/0023-per-consumer-claim-ordering-email-claims-after-successful-send.md`
- Guard role card: `agents/knowledge/roles/idempotency-guard.md`
- Authorization (prior ADR): `agents/backlog/adr/0001-authorization-model.md`
- Catalog: `agents/knowledge/patterns-backend.md:281` (B8 side-effects rule),
  `agents/knowledge/security-rules.md:100` (S7 idempotency)
- Canonical architecture: `docs/architecture/backend.md`, `docs/architecture/fiscal-compliance.md`,
  `docs/architecture/push-notifications.md`
