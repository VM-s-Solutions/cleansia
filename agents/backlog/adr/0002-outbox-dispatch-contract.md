# ADR-0002 — Side-effect dispatch contract: post-commit dispatch, idempotent consumers, and a poison/dead-letter floor

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-01
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting

> This ADR is **ADR-OUTBOX (contract)**. It freezes the *contract* that both the Wave-0 tactical fix
> (F2/F3/F4 — post-commit dispatch + idempotent consumers + poison/dead-letter) **and** the Wave-1
> full transactional outbox (F2-FULL) must honor. It also resolves the pipeline-commit-on-validation-
> failure bug (**F11**). It deliberately does **NOT** design the Wave-1 outbox *table* — it fixes the
> contract so the table can land later without a second rewrite of every command-handler call site.
> Once `accepted` it is immutable — change it by superseding, never by editing.

> **What Wave-0 honestly is.** Wave-0 moves the dispatch from *before* the commit to *after* it, and
> makes consumers safe to run twice. It does **not** make dispatch durable — the in-memory buffer is
> **at-most-once** for the dispatch step (a crash between commit and drain loses the send). Durable,
> at-least-once dispatch arrives only in **Wave-1** (the outbox row). Wave-0 therefore pairs the
> at-most-once dispatch with a **reconciliation backstop** on the two fiscal/financial queues so the
> residual silent-loss path is *detected and re-enqueued*, not silently dropped. The contract below is
> precise about which guarantees are Wave-0 vs Wave-1; do not quote a single sentence out of that frame.

---

## Context

Every write that triggers an external effect (receipt PDF + email, invoice, push notification,
sitewide promo fan-out, pay calculation) does so by enqueuing a message to one of five Azure Storage
Queues (`QueueNames.cs:5-9`: `generate-receipt`, `generate-invoice`, `notifications-dispatch`,
`sitewide-promo-fanout`, `calculate-order-pay`). The enqueue **and** the consumer side are both broken
in ways that lose work or duplicate it. All facts below are verified against the real code.

**1 — The enqueue fires before the database commit (dual-write hazard).**
`AzureStorageQueueClient.SendAsync` (`AzureStorageQueueClient.cs:14-27`) calls `SendMessageAsync`
**immediately** (`:26`). It is a singleton bound to `IQueueClient`; it is **not** enrolled in any EF
transaction. The `UnitOfWorkPipelineBehavior` (`UnitOfWorkPipelineBehavior.cs:19-20`) runs the
handler first and calls `CommitAsync` **after**. Therefore every `queueClient.SendAsync` inside a
handler puts a message **on the wire before** the commit, and that message is **not rolled back** if
the commit fails. Two failure shapes, both live today:

  - **Phantom side effect.** `CreateOrder.Handler` enqueues `generate-receipt` for cash orders
    (`CreateOrder.cs:376`) *inside* the handler; the order row is only persisted by the pipeline
    commit afterwards. If the commit throws, a receipt-generation message exists for an order that
    was never saved. The consumer then can't find the order (`GenerateReceiptFunction.cs:48-52`
    throws → retries 5× → poison).
  - **Webhook stamp/effect split.** `HandlePaymentNotification.HandleCompletedSession`
    (`HandlePaymentNotification.cs:241-257`) enqueues `generate-receipt` **and** an `OrderConfirmed`
    push **before** the pipeline commits the `ProcessedStripeEvent` stamp + the `Paid`/`Confirmed`
    state change (`HandlePaymentNotification.cs:144-159`). The idempotency design relies on a PG
    23505 unique-violation race **at commit** — but the side effects have already fired by then.

**1a — The parallel-retry trace (the subtle case the handler comment flags at `:136-143`).** Two
concurrent Stripe deliveries of the same event both pass `HasProcessedAsync` (`:144`, no row yet),
both `Add` the stamp (`:156`), both (post-Wave-0) buffer the receipt + push, both reach the UoW
commit. One wins the unique index; **the loser throws `DbUpdateException` at commit**. Under the
reordered pipeline (D4: Dispatch → Validation → UoW → Handler), the loser's exception propagates up
through `next()` in `PostCommitDispatchBehavior` → the dispatch guard (`response is BusinessResult
{ IsSuccess: true }`) is **never reached because `next()` threw**, not because it returned a failure
→ the loser **dispatches nothing**. So D1 + D4 do close the parallel-retry double-dispatch — and this
ADR now *shows* that, rather than asserting it. The already-processed short-circuit
(`HandlePaymentNotification.cs:144-150` → returns `BusinessResult.Success` with an **empty** pending
buffer) drains to nothing — no dispatch. See D1.2's buffer-lifetime clause.

  - The same before-commit pattern is in `CreateOrder`, `CompleteOrder`, `ConfirmRecurringOrder`,
    `CancelOrder`, `TakeOrder`/`StartOrder`/`NotifyOnTheWay`, `AddDisputeMessage`, and
    `HandlePaymentNotification` — all per-request command handlers (full verified inventory in D5). A
    second, structurally different set of callers (sweeps and called-services) is handled separately
    (D5 Bucket B).

  The `SendPushNotificationMessage` doc comment already *claims* dispatch happens "post-commit"
  (`SendPushNotificationMessage.cs`) — the contract this ADR freezes makes the comment true for the
  command-handler path.

**2 — Consumers are not safe to run twice.** Azure Storage queues are **at-least-once**; the queue
trigger redelivers on any unhandled exception until `maxDequeueCount` (`host.json:22` = 5). One
consumer is already idempotent **for its domain write** and is our reference:
`GenerateReceiptFunction` checks `order.PaymentStatus`/`PaymentType` eligibility
(`GenerateReceiptFunction.cs:59-64`) and `order.Receipt is not null` (`:66-70`) before *creating* a
receipt. **But that guard does not cover the email re-send** — the email is sent at `:95` and the
commit (`MarkEmailSent` + `CommitAsync`) is at `:97-99`; a crash between `:95` and `:99` re-sends the
email on redelivery. The terminal effect (email) is not yet idempotent (see D2.2 / C6 fix).
`SendPushNotificationFunction` has **no** dedup guard at all (`SendPushNotificationFunction.cs:30-122`):
a redelivery (or the duplicate enqueue from hazard 1) sends the **push again**. Its `CommitAsync` runs
**only** when there are dead tokens to prune (`:100-108`, `if (result.InvalidTokens.Count > 0)`), so
on the common path it never commits — any dedup row must be claimed in its *own* committed transaction
(D2.2). There is no per-message identity on the wire today — `SendPushNotificationMessage`,
`GenerateReceiptMessage`, and the others carry **no message id** (D2.1 adds one).

**3 — Poison messages vanish.** `host.json:17-22` sets a single global `maxDequeueCount: 5` with **no
per-queue handling and no dead-letter monitoring**. After 5 dequeues the Storage-queue runtime moves
the message to `<queue>-poison` and **nothing reads those queues**. A receipt or invoice that fails 5×
is **silently lost** — a lost *fiscal/financial* artifact. (Note: this poison floor only catches
messages that were *enqueued*; the Wave-0 never-enqueued silent-loss path is caught by reconciliation,
not poison — see D3.4 and the CH-1 fix.)

**4 — The pipeline commits even when validation fails (F11).** Behaviors run in registration order
(`FluentValidationExtensions.cs:13-14`): `UnitOfWorkPipelineBehavior` is registered **first**, so it
is the **outer** behavior and wraps `ValidationPipelineBehavior`. `UnitOfWork.Handle`
(`UnitOfWorkPipelineBehavior.cs:19-20`) calls `next()` (validation + handler) and then
**unconditionally** `CommitAsync` — even when validation returned a failure `BusinessResult` and the
handler never ran (`ValidationPipelineBehavior.cs:50-53` returns the failure result *without* calling
`next()`). This is the same surface the dispatch contract sits on, so it is fixed here (D4).

**5 — The Functions host runs the same pipeline.** `Cleansia.Functions/Program.cs` registers the
MediatR pipeline (incl. the new `PostCommitDispatchBehavior`). So `CalculateOrderPayFunction` invoking
`mediator.Send(CalculateOrderPay.Command)` (`CalculateOrderPayFunction.cs:43`) runs the **full
pipeline inside the Functions worker** — post-commit dispatch fires there too (D1.3 clause).

This is **one decision** — "where side effects fire relative to the commit, and what makes them safe
to fire" — because the parts are inseparable: post-commit dispatch is meaningless if the commit runs
on a validation failure (F11); at-least-once delivery makes post-commit dispatch *worse* unless
consumers are idempotent; idempotent consumers still lose work without a poison/dead-letter floor; and
the at-most-once Wave-0 dispatch needs reconciliation on the fiscal queues. The Wave-0 tactical fix
and the Wave-1 full outbox must honor the **same** command-handler call-site shape, or we rewrite
every enqueue twice — so the **contract**, not either implementation, is what gets frozen.

---

## Decision

> **Contract principle (governs D1–D6).** A command handler **records intent**; it never performs an
> external queue side effect directly. Intent is **realized after** the owning `UnitOfWork` commit
> succeeds — **best-effort in Wave-0** (in-memory buffer, at-most-once; reconciliation backstops the
> fiscal queues), **durably in Wave-1** (outbox row, at-least-once). Every realized effect is **keyed
> deterministically** so it is safe to realize more than once. Wave-0 and Wave-1 are two *backings* of
> the **same** handler-facing contract; swapping one for the other touches **zero command-handler call
> sites**.

### D1 — The dispatch contract: handlers enqueue intent; a post-commit dispatch step puts it on the wire

Command handlers MUST NOT call `IQueueClient.SendAsync` directly. They call a new collector,
**`IPendingDispatch`**, which **records** the message and returns synchronously without touching the
network:

```csharp
// Cleansia.Core.Queue.Abstractions/IPendingDispatch.cs — registered SCOPED (per request).
public interface IPendingDispatch
{
    // Records intent to send `message` to `queueName` after the unit of work commits.
    // `messageKey` is the deterministic idempotency key (D2). Wave-0: pure in-memory.
    // Wave-1: writes an outbox row into the SAME scoped DbContext the pipeline commits.
    void Enqueue<T>(string queueName, T message, string messageKey);
    IReadOnlyList<PendingMessage> Drain();   // hands the buffer to the dispatcher; clears it.
}

public sealed record PendingMessage(string QueueName, string Body, string MessageKey);
```

**D1.1 — `Enqueue` invariant (the seam that makes both backings equivalent — CH-4).**
`Enqueue` is **idempotent within a request on `(QueueName, MessageKey)`**: calling it twice with the
same key in one request realizes **exactly one** effect, in *both* backings. It always targets the
**same scoped `DbContext`/UoW** the pipeline commits. Therefore:
- a Wave-1 double-`Enqueue` writes **one** outbox row (collapsed on the key), not two;
- the consumer-side dedup (D2.2) is load-bearing for **in-request** dupes as well as cross-request
  redeliveries and duplicate enqueues;
- a handler that early-returns without a commit discards the buffer in Wave-0 and (because no commit
  ran) writes no durable outbox row in Wave-1 — equivalent.
This invariant is what lets D5 promise "change command-handler call sites once."

**D1.2 — Buffer lifetime (CH-3).** `Drain()` clears the buffer. On a committed success the dispatch
behavior drains and dispatches. On **any** non-success — validation failure, handler failure,
short-circuit returning success-with-empty-buffer (e.g. `HandlePaymentNotification.cs:144-150`), or
scope disposal — the buffer is **discarded** and **nothing** is dispatched. A buffered-but-not-
dispatched message MUST NOT leak into a reused scope (`IPendingDispatch` is scoped per request; scope
disposal drops it).

**Where it fires — precisely.** Dispatch is a **new pipeline behavior**,
`PostCommitDispatchBehavior<TRequest,TResponse>`, registered as the **outermost** behavior:

```
PostCommitDispatchBehavior        (outermost)   ← drains & dispatches AFTER inner pipeline returns
  └─ ValidationPipelineBehavior                 ← rejects before the handler/commit runs (now outer to UoW — D4)
       └─ UnitOfWorkPipelineBehavior            ← commits a validated command's work (now innermost write boundary)
            └─ Handler                          ← records intent via IPendingDispatch.Enqueue
```

```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next(ct);              // validation → commit (UoW) → handler, all inside.
                                               // If commit throws, next() throws → guard below never runs → no dispatch.
    if (response is BusinessResult { IsSuccess: true })   // dispatch ONLY on a committed success
        await dispatcher.DispatchAsync(pending.Drain(), ct);   // best-effort; logged, never throws into the response
    return response;
}
```

Why this point and not an `IStartupFilter` or an in-handler "after commit" call:
- It is **after** the `UnitOfWork` commit (the commit happens inside `next()`), so a message is on
  the wire **only if the row is durable** — removing the *before-commit* dual-write.
- A dispatch failure is **logged and swallowed**, never converted into a 500 — the customer-facing
  operation already committed (preserves the documented invariant that customer completion is never
  blocked by a downstream effect; `docs/architecture/fiscal-compliance.md`). A *lost* dispatch is the
  Wave-0 residual gap; it is recovered durably in Wave-1 and detected in Wave-0 by reconciliation
  (D3.4) for the fiscal queues.
- An `IStartupFilter` is the wrong layer (boot-time, not per request). An in-handler "after commit"
  call can't see whether the *pipeline* commit succeeded (the handler doesn't own the commit) and
  would re-scatter the timing across every call site.

**D1.3 — In-Functions-host behavior (C4).** Because `Program.cs` wires the full pipeline into the
Functions worker, `PostCommitDispatchBehavior` runs there too. **Wave-0:** intended — a command
invoked inside a Function (e.g. `CalculateOrderPay` from `CalculateOrderPayFunction.cs:43`) dispatches
its own intent post-commit. **Wave-1:** the in-Function pipeline would write outbox rows that the
drainer picks up; to avoid nested-outbox surprise, the **Wave-1 ADR (F2-FULL) MUST decide** whether
the Functions host gets the post-commit behavior, the drainer, both, or neither. This ADR names that
question and defers the answer — it is a Wave-1 *backing* concern, not a contract concern.

**Predicate alignment (C7).** `UnitOfWorkPipelineBehavior` keys on the request type name ending in
`Command` (`UnitOfWorkPipelineBehavior.cs:10,27`); the dispatch behavior keys on the response being a
successful `BusinessResult`. `ValidationPipelineBehavior` is already constrained `where TResponse :
BusinessResult` (`ValidationPipelineBehavior.cs:13`). These can disagree (a `…Command` returning
non-`BusinessResult`, or a `BusinessResult`-returning request not suffixed `Command`). Both write/
dispatch behaviors MUST converge on the **same** keying rule. This ADR freezes the dispatch rule as
`response is BusinessResult { IsSuccess: true }` (dispatch only on committed success) and requires
`UnitOfWorkPipelineBehavior` to add the same response-success check as defense-in-depth (see D4).
Paged queries return `PagedData<T>` (not `BusinessResult`) and are correctly skipped by both,
unchanged.

**This adapts** the backend command pattern (`agents/knowledge/patterns-backend.md:281` — "Side
effects (Stripe/email/queue)? -> narrow try/catch + idempotency (B8)" and "No `CommitAsync()` in
handlers"): B8 stays, but the *queue* side effect moves from an in-handler `SendAsync` to an
in-handler `pending.Enqueue` realized post-commit. Non-queue side effects (e.g.
`CreateCheckoutSessionAsync` in `CreateOrder`) are out of scope — the order must hold the returned
`StripeSessionId` before it commits, so they remain in-handler under the narrow-try/catch rule.

### D2 — The idempotent-consumer contract: every message carries a deterministic key; every consumer asserts before acting

Two layers, both mandatory. A consumer satisfies the contract iff it has **both**.

**D2.1 — Message identity (the deterministic per-message key).** Every queued message is wrapped in a
frozen envelope so the consumer has a stable key to dedup on:

```csharp
// Cleansia.Core.Queue.Abstractions/QueueEnvelope.cs
public sealed record QueueEnvelope<T>(
    string MessageKey,   // idempotency key — DETERMINISTIC, set by the producer (NOT a fresh Guid per send)
    string TenantId,     // explicit; the consumer has no JWT (see GenerateReceiptFunction tenant override).
                         // Redundant for notifications-dispatch (SendPushNotificationMessage already carries TenantId).
    T Payload);
```

The `MessageKey` is **deterministic per logical effect**, so a *duplicate enqueue* (hazard 1's retry)
produces the **same** key as a *redelivery* and the effect is recognized as already-done. Frozen key
formulas (one per queue; changing one is a superseding ADR):

  | Queue | `MessageKey` formula | Effect it dedups |
  |---|---|---|
  | `generate-receipt` | `receipt:{OrderId}` | one receipt per order |
  | `generate-invoice` | `invoice:{PayPeriodId}:{EmployeeId}` | one invoice per employee per period |
  | `notifications-dispatch` | `push:{UserId}:{EventKey}:{OrderId?}` | one push per user per event per subject |
  | `calculate-order-pay` | `pay:{OrderId}:{EmployeeId}` | one pay row per order per cleaner |
  | `sitewide-promo-fanout` | (producer — no inbound dedup; see D2.3) | — |

**D2.1a — Dual-read at the deploy boundary (CH-6 / pragmatic-C5).** Wrapping payloads in
`QueueEnvelope<T>` is a **wire-format change**, NOT backward-compatible on the wire: bare-payload
messages already in-flight when the new consumer deploys would deserialize to a null-`Payload`/null-
`MessageKey` envelope → throw → poison (and **dead-letter** on the fiscal queues). So, for one deploy
cycle, **every consumer MUST dual-read**: try-parse as `QueueEnvelope<T>`; on failure, fall back to
bare `T` and **synthesize the deterministic key from the payload fields** (possible precisely because
the key is deterministic — D2.1). The dual-read may be removed in a later release once the old
messages have drained. A drain-before-deploy is an acceptable alternative for low-volume queues but
the dual-read is the default because the fiscal queues cannot risk dead-lettering in-flight artifacts.

**D2.2 — The assertion (target-state check; generic backstop for non-transactional effects).** Before
performing its **terminal** effect (the user-visible PDF/email/push — NOT an intermediate domain
write), **every effect-realizing consumer MUST** assert the effect has not already happened.

  - `generate-receipt` — keep the eligibility + `order.Receipt is not null` checks
    (`GenerateReceiptFunction.cs:59-70`) for receipt *creation*. **Known Wave-0 gap (C6):** the email
    re-send is NOT covered — the email fires at `:95` before the commit at `:99`. Wave-0 MUST close
    this: mark the email-sent state in a commit that **precedes** the send (claim-first), accepting a
    rare lost email on crash, OR explicitly accept the rare double-email and document it. The contract
    requires the *terminal* effect (email) to be named in TC-IDEMP-0, not just receipt creation.
  - `calculate-order-pay` — keep "validator rejects already-calculated → ack, don't throw"
    (`CalculateOrderPayFunction.cs:55-65`; duplicate-guard lives in `CalculateOrderPay`'s validator).
    **Already compliant** via the inner command.
  - `generate-invoice` — **OUT OF SCOPE for Wave-0 (C6 / test-architect-C2):** the Function is a
    no-op stub (`GenerateInvoiceFunction.cs:20-26`); it has no effect to dedup. Its target-state guard
    (`EmployeeInvoice` for `(PayPeriodId, EmployeeId)` does not exist) MUST land **with** the effect,
    not before it. Until then `generate-invoice` carries the `-poison` consumer (D3) but no TC-IDEMP-0
    obligation.
  - `notifications-dispatch` — **the gap (F2/IDEMP).** Push has no domain target-state and FCM is a
    **non-transactional** external call (`SendPushNotificationFunction.cs:91`) — it cannot be made
    atomic with a DB row. The guarantee is therefore **at-most-once after the marker**, NOT
    "exactly once". The consumer uses **guard-first (claim-then-act)**:
      1. compute `MessageKey`; attempt to insert a `ProcessedMessage(MessageKey unique)` row in its
         **own committed transaction** (unconditionally — NOT gated behind the dead-token prune at
         `:100-108`); if the insert hits the unique index, the effect already ran → **ack and return**;
      2. only then call `pushDispatcher.SendAsync`.
    A crash after the guard-commit but before the send loses that one push — **accepted** for a
    notification. The reference is `ProcessedStripeEvent` (claim-then-act); the standalone Function
    does **not** inherit it via the pipeline (it has no MediatR request and its existing commit at
    `:107` is conditional) — it must do the claim explicitly.

The generic `ProcessedMessage(MessageKey unique)` table is the **fallback** for effects with no domain
target-state; domain target-state checks are **preferred** where they exist. **A consumer that has
neither a target-state check nor a `ProcessedMessage` guard fails the contract.** All consumers
expose the guard through one canonical, greppable abstraction — `IIdempotencyGuard.AlreadyProcessed(
messageKey)` for the generic backstop, or a named target-state method for the domain checks (so the
verification grep can find it — see check #3 / C4 fix).

**D2.3 — Fan-out producers are not effect-realizers (pragmatic-C3 / test-architect-C2).**
`SendSitewidePromoFanoutFunction` (`:123`) is a queue **consumer that is a producer**: it pages users
and enqueues N `notifications-dispatch` messages. It has **no commit to gate** and **no pipeline**, so
it **remains on direct `IQueueClient.SendAsync`**. Its at-least-once safety comes from the
**downstream** push consumer's `push:{UserId}:{EventKey}` dedup (D2.2) — which already exists in the
table. A mid-stream crash re-enqueues recipients from offset 0; this **doubles cost, not effect** (the
downstream dedup absorbs the duplicate pushes). Its per-recipient `try/catch`-and-continue
(`:137-146`) already prevents one bad enqueue from poisoning the whole campaign. A campaign-level
resume marker is a **nice-to-have optimization**, not a contract requirement. Fan-out producers are
**excluded** from TC-IDEMP-0 and from verification check #3.

This **adopts** S7 (`agents/knowledge/security-rules.md:100` — "Idempotency on side-effecting
commands") and extends it from *commands* to *queue consumers*, naming `ProcessedStripeEvent`
(`Core.Domain/Payments`) as the canonical shape the generic `ProcessedMessage` copies.

### D3 — The poison / dead-letter contract (F3): every queue has a dead-letter handler and an alert

`maxDequeueCount` stays at 5 (`host.json:22`) but a poisoned message must **not** vanish into an
unread `<queue>-poison`. Contract:

1. **Every business queue gets a paired poison consumer** bound to `<queue>-poison` whose sole job is
   to **persist the dead letter and alert**, never to re-process:
   ```csharp
   [Function("GenerateReceiptPoison")]
   public async Task Run(
       [QueueTrigger("generate-receipt-poison", Connection = "QueueStorageConnectionString")] string body,
       CancellationToken ct)
   {
       await deadLetterStore.RecordAsync(queue: "generate-receipt", body, ct);  // durable row, admin-visible
       logger.LogError("DEAD-LETTER on generate-receipt: {Body}", body);        // → Sentry/AppInsights alert
       // No throw: acking removes it from -poison. The durable DeadLetter row is the recovery source.
   }
   ```
   For `generate-receipt`/`generate-invoice` the durable `DeadLetter` DB row is the **fiscal/financial**
   recovery path and is mandatory; the other three log+alert+store at minimum.
2. **`host.json` per-queue note.** The global `maxDequeueCount: 5` is acceptable; no per-queue override
   is required by this ADR. The **floor** frozen here: *every* business queue has a `-poison` consumer
   + durable store + alert.
3. **Consumers must distinguish "retryable" from "permanent."** A **malformed or business-rejected**
   message is **logged at Warning and acked** (return — do not throw). An **infra/transient** failure
   **throws** (so the queue retries up to `maxDequeueCount`, then dead-letters). Throwing on a
   permanent error poison-queues a message that will never succeed; acking on a transient error
   silently drops recoverable work. **`SendPushNotificationFunction.cs:115-121` currently violates
   this** — it `throw`s on *any* exception including a permanent deserialize failure (`:42`) — and MUST
   be split per this rule.
   - **Fiscal-queue carve-out (CH-5b).** For `generate-receipt`/`generate-invoice`, a **"target not
     found"** (e.g. `GenerateReceiptFunction.cs:48-52`) stays **transient / bounded-retry** — it must
     NOT be reclassified to permanent-ack post-Wave-0. Post-commit dispatch makes the order durable
     before the message is normally sent, but the **CH-1 reconciliation re-enqueue** can legitimately
     race brief read-replica lag, and a bounded retry is the correct response. Reclassifying it to ack
     would mask the very silent-loss reconciliation exists to catch. Verification check #3 carries
     this exception explicitly.
4. **Reconciliation backstop for the at-most-once Wave-0 gap (CH-1 / CH-5a).** The poison floor only
   catches messages that were **enqueued and failed 5×**. The Wave-0 residual gap (committed row,
   in-memory dispatch never ran) produces **no message at all** → no poison → no alert. Therefore
   Wave-0 MUST add, for the two **fiscal/financial** queues, a **reconciliation sweep** (a timer
   Function / `BackgroundService`): orders in `Paid`/`Completed` with **no** `Receipt`, and
   `PayPeriod`s with employees who have **no** `EmployeeInvoice`, older than **N minutes** (default
   **15 min**, tunable) are **re-enqueued** through the same `IPendingDispatch`/idempotent-consumer
   path (so a re-enqueue that races a successful one is harmlessly deduped). This is the actual safety
   net for the never-enqueued case until Wave-1's durable outbox removes the gap. Without it, Wave-0
   ships a *new silent-loss path* on the most sensitive queues — so this AC is **blocking for Wave-0
   acceptance of the fiscal queues**.

### D4 — F11: validation must run *before* the commit (reorder the pipeline)

Swap the registration order in `AddValidators` (`FluentValidationExtensions.cs:13-14`) so
`ValidationPipelineBehavior` is **outer** to `UnitOfWorkPipelineBehavior`:

```csharp
// FluentValidationExtensions.cs — NEW order (outer → inner):
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PostCommitDispatchBehavior<,>));  // outermost (D1)
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));  // was inner, now BEFORE UoW
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));  // was outer, now innermost write-boundary
```

Resulting order (outer→inner): **Dispatch → Validation → UnitOfWork → Handler.** On a validation
failure, `ValidationPipelineBehavior.cs:50-53` returns a failure `BusinessResult` **without** calling
`next()`, so control never enters the UoW behavior and `CommitAsync` never runs on a rejected command.
On success: validate → commit → dispatch, each strictly after the previous. **Defense-in-depth (C7):**
`UnitOfWorkPipelineBehavior` additionally gains a `commit only if response is BusinessResult {
IsSuccess: true }` check, so the two behaviors share the same predicate and a future re-swap of
registration order cannot resurrect F11.

### D5 — Migration path: the verified call-site inventory and a three-bucket classification

The promise "change command-handler call sites once" holds **only for per-request command handlers**.
The real surface is **21 `queueClient.SendAsync` call sites across 14 AppServices files** (verified by
grep) plus one in-Function producer — not the 6 the original draft claimed. They fall into three
buckets:

**Bucket A — Per-request command handlers (covered by `IPendingDispatch`; the "change once" set; 14 sends):**
- `CreateOrder.cs:376`
- `CompleteOrder.cs:219,227,266` (three sends)
- `ConfirmRecurringOrder.cs:112,118`
- `CancelOrder.cs:160`
- `TakeOrder.cs:195`, `StartOrder.cs:137`, `NotifyOnTheWay.cs:103`
- `AddDisputeMessage.cs:67`
- `HandlePaymentNotification.cs:241,246,278`

**Bucket B — Sweeps and called-services (do NOT fit the request-scoped buffer — pragmatic-C2; 7 sends):**
- `AutoCancelStaleRecurringOrders.cs:87`, `SendRecurringOrderReminders.cs:77`,
  `SendMembershipLifecycleNotifications.cs:87,125`, `NewJobsDigestService.cs:170`,
  `SendSitewidePromo.cs:88` — loop over N rows and enqueue **per iteration**, committing as they go.
  A request-scoped buffer drained **once** at the request boundary is the wrong shape (it would buffer
  all N pushes and fire only after the whole sweep commits). **Wave-0:** these keep direct
  `IQueueClient.SendAsync` under a **documented carve-out**; reviewer check #1 whitelists them.
  **Wave-1:** they move to the outbox row written inside each per-iteration commit — the correct shape
  (each commit drains its own row). Treated as a **Wave-1 follow-up**, not forced into the Wave-0 seam.
- `LoyaltyService.cs:75` — a plain service called *inside* `CompleteOrder`'s scope, with its own
  ledger-based idempotency (`LoyaltyService.cs:52-60`). It **may** use the outer `IPendingDispatch`
  (its loyalty push then gates on `CompleteOrder`'s commit — the explicit choice this ADR makes,
  resolving the previously-undefined "which commit gates it"); if it does, it is Bucket A by
  association. If it stays direct, it is Bucket B with a carve-out.

**Bucket C — In-Function producers (no commit to gate, no pipeline — pragmatic-C3):**
- `SendSitewidePromoFanoutFunction.cs:123` — stays direct `IQueueClient` (D2.3); excluded from check #1
  (it is a Function, not a handler) and from check #3.

**Wave-0 sequencing:**
1. **Testability deliverable (test-architect-C1):** the Functions consumer bodies are currently in an
   `OutputType=Exe` project (`Cleansia.Functions.csproj:8`) that `Cleansia.Tests.csproj:25-26` does
   not reference (it references `Cleansia.Core.AppServices` + `Cleansia.TestUtilities`). Extract the
   consumer bodies into a **non-Exe class library** (`Cleansia.Functions.Core`) that the Exe host
   thinly wraps, and reference it from `Cleansia.Tests`. This is a **named Wave-0 precondition** for
   the verification gate, not an assumption. (Referencing the Exe directly is legal but discouraged.)
2. Add `IPendingDispatch` (scoped, in-memory) + `PostCommitDispatchBehavior` + reorder the pipeline
   (D4). `Enqueue` records; the behavior calls the existing `IQueueClient.SendAsync` per drained
   message — unchanged client.
3. **Replace every Bucket-A `queueClient.SendAsync(...)` with `pending.Enqueue(...)`**, wrapping each
   message in `QueueEnvelope<T>` with its D2.1 key. **This is the call-site edit that happens once.**
4. Add consumer-side guards (D2.2, via `IIdempotencyGuard`) + dual-read (D2.1a) + `-poison` consumers
   (D3) + the fiscal reconciliation sweep (D3.4) + the `ProcessedMessage`/`DeadLetter` stores.

**Wave-1 (F2-FULL):** swap **only** `IPendingDispatch`'s backing so `Enqueue` writes an outbox row in
the same DbContext (atomic with business state — dual-write gone), plus a drainer. **No Bucket-A
handler and no consumer changes.** Bucket B migrates here. The outbox **table design is deferred** to
F2-FULL's own ADR; this ADR only guarantees it slots under the frozen `IPendingDispatch` seam, and
that F2-FULL decides the in-Functions-host drainer question (D1.3).

---

## Alternatives considered

- **Enroll `IQueueClient` in the EF transaction / `TransactionScope`.** Rejected. Azure Storage Queues
  are not transactional resources; a send cannot be rolled back. Same for FCM. The only sound options
  are *send after commit* (Wave-0) or *store-then-send* (Wave-1 outbox) — both under one seam.
- **Idempotency-only (keep enqueuing inside the handler).** Rejected as the *primary* fix. Idempotent
  consumers stop *double*-processing but not the **phantom** side effect (a message for a never-committed
  row). Post-commit dispatch removes the phantom at the source; idempotency is the second layer.
- **Go straight to a DB outbox in Wave-0 (skip the in-memory buffer).** This is doing Wave-1 now.
  Rejected for *timing*: it needs the outbox table, a migration (`manual_step: ef-migration`,
  owner-only), and a drainer with its own delivery/locking semantics. D5 makes it a drop-in later. The
  in-memory buffer is the bridge — and Wave-0 is **honest** that it is at-most-once for dispatch
  (hence the D3.4 reconciliation backstop on the fiscal queues).
- **Per-send random `Guid` message id instead of a deterministic key.** Rejected. A random id makes a
  *redelivery* dedupable, but a **duplicate enqueue** (hazard 1) gets a new id and fires twice. A
  deterministic `MessageKey` collapses both onto one key — and is what makes the D2.1a wire fallback
  possible.
- **Raise dispatch failures as 500.** Rejected — would fail a customer-facing operation that already
  committed, violating the fiscal-compliance invariant. Dispatch failures are logged + recovered
  (Wave-1 drainer; Wave-0 reconciliation on fiscal queues).
- **Fix F11 by a response-check in UoW instead of reordering.** The honest model is the *order*
  (validation gates the write boundary), and reordering makes D1's outermost-dispatch fall out
  naturally — so the order is the primary fix, with the response-check kept as defense-in-depth (D4).

---

## Consequences

**Cheaper:**
- The **before-commit** dual-write is removed: a Wave-0 queue message is on the wire only **after** the
  row is durable, and a Wave-1 message is written **atomically** with it. The "receipt for a
  non-existent order" / "double push on webhook retry" class of bug cannot recur (parallel-retry trace
  in Context §1a).
- Wave-1 lands with **zero Bucket-A call-site churn** — the expensive part is paid once, in Wave-0.
- Consumers are uniformly safe to run twice (with the honest semantics named per queue in D2.2).
- Poisoned *enqueued* fiscal/financial work is durable and admin-replayable (D3); never-enqueued
  fiscal work is detected and re-enqueued by reconciliation (D3.4).
- F11 is closed: no command commits partial state on a validation failure (D4, + defense-in-depth).

**More expensive (new obligations):**
- A command handler MUST NOT call `IQueueClient.SendAsync` — it calls `IPendingDispatch.Enqueue(...)`
  with a `QueueEnvelope<T>` and the queue's frozen `MessageKey`. Direct `IQueueClient` use from a
  Bucket-A handler is a blocking review finding. (Bucket B/C are whitelisted carve-outs.)
- Every **new queue** MUST: declare a frozen `MessageKey` formula, have a consumer with a target-state
  check **or** an `IIdempotencyGuard` guard (D2.2), have a `<queue>-poison` consumer + durable
  dead-letter + alert (D3), and dual-read at deploy (D2.1a).
- Every consumer MUST classify failures (D3.3) and honor the fiscal-queue carve-out (target-not-found
  stays transient).
- **Wave-0 known residual gaps, explicitly accepted:**
  - *Dispatch is at-most-once* — a crash between commit and the in-memory drain loses the send. This is
    the reason Wave-1 exists. On the two fiscal queues it is **detected and re-enqueued** by D3.4
    reconciliation; on the other three (push/promo) a lost dispatch is a lost notification, accepted.
  - *Push idempotency is at-most-once after the marker* (D2.2) — a crash between the guard-commit and
    the FCM send loses that one push. Accepted for a notification; never for a fiscal artifact.
  - *Receipt email re-send* (C6) — must be closed in Wave-0 per D2.2; until then it is a known
    double-email window.
  All residual gaps MUST be documented wherever Wave-0 ships so they are not mistaken for the end state.

**Rollout ordering (ticket sequencing):**
- **F11 (D4) + D1 ship together** — reorder + post-commit behavior are one structural change.
- The **Bucket-A call-site migration (D5 step 3) ships with D1/D4** — a half-migrated handler is the
  worst of both worlds.
- The **consumer guards + dual-read + poison consumers + fiscal reconciliation (D2/D3)** must land in
  the same Wave-0 — `SendPushNotificationFunction` is unsafe to run twice until D2.2, and the fiscal
  reconciliation is blocking for the fiscal queues.
- The **testability extraction (D5 step 1)** is a precondition for the verification gate.
- No NSwag change (the queue contract is internal). `ProcessedMessage` / `DeadLetter` tables are
  `manual_step: ef-migration` (owner-only) — flag in tickets.

---

## How a reviewer verifies compliance

**Mechanical (automated — the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **No direct queue send from a Bucket-A handler.** Grep for `queueClient.SendAsync` / `IQueueClient`
   inside any `Features/**` command **Handler** class → must be **zero** after Wave-0, **except** the
   Bucket-B/C whitelist enumerated in D5 (sweeps, called-services, the fan-out Function) and the
   dispatch behavior / Wave-1 drainer. A non-whitelisted match is a blocking finding.
2. **Every business queue name has a poison consumer.** For each `const` in `QueueNames.cs`, assert a
   Function bound to `"<value>-poison"` exists.
3. **Every effect-realizing consumer has a *named* idempotency guard (C4 fix).** For each
   `[QueueTrigger]` Function — **excluding** `-poison` consumers, the `generate-invoice` stub (until it
   has an effect), and fan-out producers (D2.3) — assert the body calls the canonical
   `IIdempotencyGuard.AlreadyProcessed(messageKey)` **or** a named target-state guard method, **before**
   its terminal effect. A freeform `... is not null` no longer satisfies this check (it green-lights
   non-idempotent code — verified against `SendSitewidePromoFanoutFunction.cs:79,118`). The grep
   enforces *presence of the named token*; actual idempotency is proven by TC-IDEMP-0. The fiscal-queue
   target-not-found-stays-transient carve-out (D3.3) is recorded so reviewers do not "simplify" the
   throw at `GenerateReceiptFunction.cs:50-51` into an ack.
4. **Pipeline order.** A unit test resolves `IEnumerable<IPipelineBehavior<,>>` and asserts the
   concrete order (PostCommitDispatch → Validation → UnitOfWork) to prevent a re-swap of F11.

**Test contract (these are the gate, not aspiration — D5 step 1 makes them buildable):**
5. **TC-IDEMP-0 ("safe to run twice").** For each effect-realizing consumer (excluding the invoice stub
   and fan-out producers), invoke `Run` **twice with the same `QueueEnvelope<T>`** and assert the
   **terminal** effect happened **exactly once**:
   - `generate-receipt` → `IReceiptService.GenerateReceiptAsync` **and** `IEmailService.SendOrderReceiptEmailAsync`
     each invoked once (the email is the terminal effect — C6).
   - `notifications-dispatch` → `IPushDispatcher.SendAsync` invoked once; second run short-circuits on
     the `IIdempotencyGuard` claim. The assertion is **at-most-once after marker** semantics
     (guard-first), explicitly labeled — not a mythical exactly-once.
   - `calculate-order-pay` → its pay-row effect once.
6. **TC-KEY-0 (producer-side key determinism — the property the whole ADR rests on, CH/test-arch-C6).**
   For each Bucket-A producing call site, **two invocations with the same domain inputs emit the same
   `MessageKey`**. This is the only test that catches an accidental `Guid.NewGuid()`/timestamp in a key,
   which would defeat the Stripe-retry duplicate-enqueue this ADR was created to fix.
7. **TC-DISPATCH-0 (pipeline-integration, not a handler test — C5).** Wiring `UnitOfWorkPipelineBehavior`
   + `PostCommitDispatchBehavior` together (the same harness as #4; confirm/extend `Cleansia.TestUtilities`
   to provide a MediatR pipeline builder — a named deliverable): when the UoW commit **throws**, the
   buffer is **not** drained (no `IQueueClient.SendAsync`); on success it drains exactly once after
   commit; on validation failure it drains nothing.
8. **TC-POISON-0 (D3 behavior).** Given a body, a `-poison` consumer writes a durable `DeadLetter` row
   and does **not** throw (does not re-poison).
9. **TC-CLASSIFY-0 (D3.3).** Per consumer: a malformed body is **acked / no throw**; a simulated infra
   fault **throws**. This is the test that catches `SendPushNotificationFunction.cs:115-121`'s
   throw-on-everything (which currently poison-queues a permanent deserialize failure).
10. **F11 regression test.** A command whose validator **fails** results in **no** `CommitAsync`
    (mock `IUnitOfWork`) and **no** dispatch.

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`pending-dispatch.md`** (new) — `IPendingDispatch`: *responsibility:* buffer (Wave-0) / record as an
  outbox row in the pipeline's DbContext (Wave-1) a command's intended post-commit queue sends,
  idempotently on `(QueueName, MessageKey)`, and hand them to the dispatcher on drain. *Collaborators:*
  the dispatch behavior, `QueueEnvelope`, the scoped `DbContext` (Wave-1). *Does NOT know:* whether/when
  the commit happened (the pipeline owns that), how a message reaches Azure (the `IQueueClient`/drainer
  owns that), or any domain rule. **Note:** this "does NOT know whether/when the commit happened" is
  exactly why Bucket-B sweeps (which need intra-loop commit-then-dispatch) are NOT covered by it (D5).
- **`post-commit-dispatch-behavior.md`** (new) — *responsibility:* on a committed successful command,
  drain `IPendingDispatch` and dispatch each message; never throw into the response; discard the buffer
  on any non-success. *Collaborators:* `IPendingDispatch`, `IQueueClient` (Wave-0) / drainer nudge
  (Wave-1). *Does NOT know:* message contents, key meaning, or consumer behavior.
- **`queue-consumer.md`** (new, generic CRC) — *responsibility:* realize exactly one external **terminal**
  effect for a `QueueEnvelope<T>`, idempotently (target-state check or `IIdempotencyGuard`), dual-reading
  the bare-payload fallback at deploy, classifying failures permanent (ack) / transient (throw) with the
  fiscal target-not-found carve-out. *Collaborators:* domain repos / `IPushDispatcher` / `IReceiptService`,
  `IIdempotencyGuard` + `ProcessedMessage` (when no domain target-state), `ITenantProvider`,
  `IDeadLetterStore` (poison consumers). *Does NOT know:* who enqueued it, whether it is a first delivery
  or a redelivery (must behave identically), or any JWT/caller identity. **Fan-out producers (D2.3) are a
  distinct role — a producer, not an effect-realizer.**
- **`idempotency-guard.md`** (new) — `IIdempotencyGuard`: *responsibility:* claim-then-act dedup for
  non-transactional effects via a `ProcessedMessage(MessageKey unique)` row committed in its own
  transaction. *Collaborators:* `ProcessedMessage` repo. *Does NOT know:* the effect itself, or that FCM
  is involved.
- **`UnitOfWorkPipelineBehavior` (existing)** — updated: now the **innermost** write boundary, runs
  **after** validation, and commits only on `response is BusinessResult { IsSuccess: true }` (predicate
  aligned with the dispatch behavior — C7).

---

## Challenge / Defense / Verdict trail (condensed)

The deliberation that produced this ADR (per `agents/process/deliberation.md`). Author drafted;
three challenger panels (distributed-systems, pragmatic, test-architecture) attacked; the Lead
re-verified every load-bearing citation against the real code and adjudicated. **Verdict: all
challenges RESOLVED; zero blocking; consensus reached.**

| # | Challenge (severity) | Disposition | Where in this ADR |
|---|---|---|---|
| CH-1 | "structurally removed" overstates — post-commit relocates the dual-write to an at-most-once dispatch gap; fiscal queues can be *worse* on silent-loss (MAJOR) | CONCEDE + REVISE | Framing banner; D1 best-effort framing; **D3.4 fiscal reconciliation** (blocking AC) |
| CH-2 | Push idempotency cited `SendPushNotificationFunction.cs:107` — a *conditional* commit that usually never runs; design as written still double-sends or loses pushes (CRITICAL) | CONCEDE + REVISE | D2.2 rewritten **guard-first / at-most-once after marker**; `:107` citation removed; claim is unconditional |
| CH-3 | Parallel-retry double-fire fix asserted, not traced; buffer lifetime undefined (MAJOR) | CONCEDE + REVISE | Context §1a explicit trace; D1.2 buffer-lifetime clause |
| CH-4 | `Enqueue` semantics drift Wave-0 (in-memory, infallible) → Wave-1 (DbContext write) breaks "zero change" promise (MAJOR) | CONCEDE + REVISE | D1.1 in-request idempotency invariant pinned |
| CH-5 | (a) poison can't catch the never-enqueued case; (b) fiscal "target not found" must stay transient, not reclassify to permanent-ack (MODERATE) | CONCEDE + REVISE | D3 scope-limit; D3.3 fiscal carve-out; check #3 exception |
| CH-6 | Envelope is a wire-format break; in-flight messages dead-letter at deploy (MAJOR) | CONCEDE + REVISE | D2.1a dual-read D-clause |
| Prag-C1 | Call-site inventory wrong (~3×): 21 sends / 14 files, not 6 (BLOCKING) | CONCEDE + REVISE | D5 full verified inventory (Lead re-counted: 21 sends, 14 Bucket-A) |
| Prag-C2 | Sweeps/called-services don't fit a request-scoped buffer (BLOCKING) | CONCEDE + REVISE | D5 Bucket B carve-out + Wave-1 follow-up |
| Prag-C3 | Fan-out is a producer-in-consumer with no commit to gate (BLOCKING) | CONCEDE + REVISE | D2.3 (stays direct; downstream dedup) |
| Prag-C4 | In-Function pipeline → nested outbox in Wave-1 (MODERATE) | CONCEDE + REVISE | D1.3 (Wave-0 intended; Wave-1 question deferred to F2-FULL) |
| Prag-C6 / Test-C2 | Invoice is a stub; receipt *email* is not idempotent (MODERATE) | CONCEDE + REVISE | invoice scoped out; email named terminal effect in D2.2 + TC-IDEMP-0 |
| Prag-C7 | UoW predicate (`name ends Command`) ≠ dispatch predicate (`BusinessResult success`) (MODERATE) | CONCEDE + REVISE | D1/D4 predicate alignment + defense-in-depth |
| Test-C1 | Test project can't see Functions consumers (`Exe`, unreferenced) (BLOCKING) | CONCEDE + REVISE | D5 step 1: extract `Cleansia.Functions.Core` library (named precondition) |
| Test-C4 | Check #3 was a substring grep that green-lights non-idempotent code (BLOCKING) | CONCEDE + REVISE | check #3 requires canonical `IIdempotencyGuard` / named target-state token |
| Test-C5 | "dispatch test" mislabeled as a handler test (MINOR) | CONCEDE + REVISE | TC-DISPATCH-0 reclassified pipeline-integration |
| Test-C6 | No producer-key-determinism test (MODERATE) | CONCEDE + REVISE | TC-KEY-0 added |
| Test-C7 | Poison + failure-classification unverified (MODERATE) | CONCEDE + REVISE | TC-POISON-0 + TC-CLASSIFY-0 added |

**Affirmed unchallenged (all panels):** post-commit dispatch via a pipeline behavior over
`IStartupFilter`/in-handler; deterministic key over random Guid; rejecting transaction-enrollment of
the non-transactional queue/FCM; the F11 reorder; the uniform `-poison` shell; the dispatch success
guard against `BusinessResult<T> : BusinessResult` (verified `BusinessResultT.cs:3`).

**Lead re-verification (this adjudication), all against current code:**
`AzureStorageQueueClient.cs:26` immediate send; `UnitOfWorkPipelineBehavior.cs:14,19-20,27`
unconditional commit + `Command`-suffix predicate; `FluentValidationExtensions.cs:13-14` UoW-before-
Validation (F11); `ValidationPipelineBehavior.cs:13,50-53` returns failure without `next()` (reorder
is mechanically sound) and is constrained `: BusinessResult`; `SendPushNotificationFunction.cs:91`
unconditional external send, `:100-108` conditional commit, `:115-121` throw-on-everything;
`GenerateReceiptFunction.cs:48-52` throw-on-not-found, `:66-70` creation guard, `:95-99` email-before-
commit gap; `GenerateInvoiceFunction.cs:20-26` stub; `CalculateOrderPayFunction.cs:55-65` ack-on-
reject; `SendSitewidePromoFanoutFunction.cs:123` direct send + `:137-146` per-recipient continue;
`host.json:22` `maxDequeueCount: 5`; `QueueNames.cs:5-9` five queues; `Cleansia.Tests.csproj:25-26`
references AppServices + TestUtilities but not Functions; `Cleansia.Functions.csproj:8` `OutputType=Exe`;
`BusinessResultT.cs:3` `BusinessResult<TValue> : BusinessResult`. The 21-send / 14-file inventory was
re-counted from the grep and is correct (the earlier "15" summary number was a residual error from the
draft and is fixed here).

**Escalations to the owner:** none. The two new tables (`ProcessedMessage`, `DeadLetter`) are
owner-only `manual_step: ef-migration` (flagged, not a decision). The reconciliation cadence (default
15 min) is a tunable, not a business decision.
