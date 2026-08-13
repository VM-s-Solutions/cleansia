# Side-effect dispatch & the outbox (living design note)

> Companion to the **immutable** ADR-0002 (`docs/decisions/adr-0002.md`).
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
   never-enqueued case. The row is **evidence with two clocks**, not an archive — see
   §"Dead-letter retention" below (ADR-0002 partial supersede, 2026-08-10).

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
Full rule + rationale: `docs/decisions/adr-0023.md`;
catalog entry: `agents/knowledge/patterns-backend.md` ("Queue-consumer idempotency").

## Dead-letter retention — the row is evidence, not an archive (ADR-0002 partial supersede, 2026-08-10)

ADR-0002 D3 called the `DeadLetter` row *"the recovery source"* and the tree read that as *forever*
(`DeadLetter.cs:34-35`: *"stored as `text` (unbounded) so nothing is truncated"*). That was right when
the row was the recovery copy of a failed dispatch. It stopped being right once the `send-email` body —
recipient address, real name, user id, and until `e84aed25` a live reset token — became one of the seven
bodies that reach it. **Bounded in credential value by the 15-minute code expiry; unbounded in PII
value.**

**The fact the whole ruling rests on: the recovery role is nominal.** Nothing reads a `DeadLetter` row.
`IDeadLetterRepository` occurs four times in `src/` — interface, DI comment, the store's ctor, the repo
class — and **none is a read**; `Cleansia.Web.Admin` has no `DeadLetter` anything. Meanwhile the fiscal
recovery D3 promised is done by `FiscalReconciliationService`, which **re-derives** the message from the
order/pay-period rows (`:93,100-104`) off a candidate query with no lower bound on age
(`OrderRepository.cs:380-410`), every 5 minutes, forever — it never touches the table. And every
drainer-written dead letter is a **second copy**: `OutboxDrainerService.cs:81,86` marks the outbox row
`Failed` *and* records the dead letter, and `PruneOutbox` deletes only `Dispatched` rows
(`PruneOutbox.cs:72-74`).

**Current shape — two clocks, uniform across all seven queues:**

| Clock | What it does | Default | Anchor |
|---|---|---|---|
| `BodyRetentionDays` | overwrite `RawBody` with `AnonymizationMarker.Value` (`"[DELETED]"`) | **7** | `DeadLetteredAt` |
| `RowRetentionDays` | delete the row | **90** | `DeadLetteredAt` |

Delete runs **first**, then redact, so the two steps are disjoint in a run and no cross-field config
invariant is needed. Lives in a new `PruneDeadLetters` command driven by the **existing**
`PruneOutboxTimerHandler` (one wakeup, two sends) — *not* in `DataRetentionBackgroundService`, which is
weekly and short-circuits on a feature flag, and *not* folded into `PruneOutbox`, whose
"read-terminal-then-delete only … dispatch unchanged" charter must stay literally true.

**The row's identity moves out of the body into columns** (`MessageKey`, `BodyFingerprint`;
`manual_step: ef-migration`). Required, not adjacent: redaction destroys the only documented handle
(`RawBody LIKE '%{MessageKey}%'`, `PoisonHandlerBase.cs:95`). Both writers already compute both values
and discard them (`PoisonHandlerBase.cs:69` vs `:80`; `OutboxDrainerService.cs:85` vs `:86`).

**Why not per-queue retention** (the alternative that looks obviously right): a `switch` on
`SourceQueue` is a denylist maintained by memory — the exact shape `PoisonAlert` refused one layer up
(*"fail-closed by construction, not by denylist"*, `PoisonAlert.cs:26-30`) — and its premise is
backwards. The fiscal bodies are `(OrderId, LanguageCode)` and `(EmployeeId, PayPeriodId,
LanguageCode)`: **no credential, no PII, ids the key already encodes**, and no recovery that reads the
row. They are also the only known amplification path — a permanently-failing receipt is re-enqueued
every 5 minutes forever, minting a new dead-letter row each cycle. The exemption would point at the
rows that need the clock most.

**Still needed for recovery when the clock runs out?** Redacted anyway, because that state does not
exist at HEAD: fiscal work is still being re-enqueued by the sweep; drainer bodies survive on the
`Failed` outbox row; a `send-email` replay after 15 minutes mails a dead token; a stale push or Live
Activity replay is a harm. **A body past the window is not a recovery asset, it is a stale one.** The
residual is named as an obligation rather than assumed away: a **new queue declares its dead-letter
body class** (`re-derivable` / `duplicated-in-outbox` / `not-recoverable-from-body`), and one that can
claim none of the three decides its retention in its own ADR — until then the defaults apply.

**Conditional, and say so out loud:** this shape holds *because* no replay path exists. Building one
that reads `RawBody` is a superseding-ADR event, not a feature ticket. The admin read endpoint the row
actually deserves projects `SourceQueue`/`MessageKey`/`TenantId`/`Error`/`DeadLetteredAt`/`Bytes`/
`Fingerprint` — **and not the body**.

**Composition with GDPR erasure (T-0583):** different clocks, both needed. Retention is absolute and
for everyone; erasure is event-driven and for one subject, and must run immediately — a user erased
today whose dead letter is 2 days old otherwise keeps their name in plaintext for 5 more. Retention
**bounds** erasure's residual; promoting `MessageKey` makes erasure an indexed prefix match instead of
a `LIKE` scan. The one subset where **retention is the only erasure**: a row whose body was unparseable
carries `MessageKey = "<unparseable>"` and has no subject handle by construction.

**Named residual, not fixed here:** `OutboxMessage.Body` on `Failed` rows is the same bytes one table
over, never pruned — and unlike the dead letter it has a *real* recovery role, so its clock is its own
decision and its own ticket.

Full record: ADR-0002 §"Partial supersede — 2026-08-10 (architect, T-0584)".

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

> Wave-1 landed (ADR-0008). Items 1–3 are answered there; kept for the trade-off trail. **Item 1's
> "retention" leg is answered only for `Dispatched` rows** — the `Failed`-row body is still unbounded
> (see §"Dead-letter retention" → named residual).

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

- Immutable contract: `docs/decisions/adr-0002.md`
- Durable consumer dedup: `docs/decisions/adr-0010.md` (partially
  superseded by ADR-0023 for the email claim ordering + the interface-frozen invariant)
- Claim-ordering rule: `docs/decisions/adr-0023.md`
- Guard role card: `docs/domain/roles/idempotency-guard.md`
- Authorization (prior ADR): `docs/decisions/adr-0001.md`
- Catalog: `agents/knowledge/patterns-backend.md:281` (B8 side-effects rule),
  `docs/architecture/security-rules.md:100` (S7 idempotency)
- Canonical architecture: `docs/architecture/backend.md`, `docs/architecture/fiscal-compliance.md`,
  `docs/architecture/push-notifications.md`
