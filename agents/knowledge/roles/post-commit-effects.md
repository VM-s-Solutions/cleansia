# Role — `IPostCommitEffects` / `PostCommitEffectBehavior` (CRC card)

> **PROPOSED — not yet the standard.** Introduced by **ADR-0038**
> (`agents/backlog/adr/0038-promo-redemption-reservation-runs-after-the-uow-commit.md`), status
> `proposed` as of 2026-08-02, pending one challenger pass. Do not cite as law; do not build a second
> effect on this seam until the ADR's `## Verdict` is signed by a second instance.
> Living doc: `agents/architecture/decisions/promo-redemption-ordering.md`.

## Responsibility (one sentence)
Carry a **local, idempotent, same-database** write that must run **strictly after** the `UnitOfWork`
commit — because it self-commits and references a row the pipeline has not written yet — and run it in
the still-live request scope, so it keeps the ambient tenant, the ambient actor and millisecond latency.

## Collaborators
- **`PostCommitEffectBehavior<,>`** — the pipeline behavior, registered **immediately inside**
  `PostCommitDispatchBehavior<,>` (so: effects first, queue dispatch second), gated on the same
  `response is BusinessResult { IsSuccess: true }` predicate the other two behaviors use.
- **`IPostCommitEffect`** — a **serializable intent record** (never a closure), carrying an `EffectKey`
  built from the natural key it is idempotent on.
- **`IPostCommitEffectExecutor<TEffect>`** — the per-effect executor; owns its own commit and its own
  logging.
- The first (and today only) instance: the **promo redemption reservation** —
  `IPromoCodeService.ApplyAsync` via `IOrderPromoApplier`, idempotent on `OrderId` behind the UNIQUE
  index on `PromoCodeRedemptions.OrderId`.

## The five laws (an effect is admissible only if ALL hold — ADR-0038 §D2.2)
1. **Local to this database.** External side effects (queue, HTTP, Stripe, email, push) go to
   `IPendingDispatch`/the outbox. *Durable-external → outbox; local-idempotent-post-commit → effect.*
2. **Idempotent on a natural key of persisted state.** An effect whose second run does something is not
   admissible.
3. **Owns its own commit.** The pipeline's commit already happened and will not happen again —
   **a tracked `Add` inside an effect is a silent no-op** (the most likely misuse).
4. **Cannot fail the request.** Catch, log at Error with a stable event name, do **not** rethrow (the
   operation already committed — ADR-0002 D1, the fiscal-compliance invariant).
5. **Failure is detectable without the log** — a named reconciliation predicate over persisted state,
   written into the effect's doc-comment.

## Does NOT know
- **Whether the commit succeeded** — the behavior's predicate decides that; the effect is only ever
  handed work that committed.
- **What the handler did**, beyond the fields on its own intent record. An effect that needs the
  handler's local state should be taking more fields, not a closure.
- **The queue, the outbox, or any wire protocol** — that is `IPendingDispatch`. An `IQueueClient` or
  `HttpClient` reference inside an executor is a law-1 violation.
- **Retry/durability.** This seam is at-most-once by construction. An effect that *needs* at-least-once
  belongs on the outbox (or gets an additive outbox leg beside it — which is why the intent record is
  serializable).
- **Ordering relative to other effects.** Effects drain in record order; an effect that depends on
  another effect having run is two effects that should be one.

## Watch-list
- `IPendingDispatch` **cannot** be reused for this: under the durable backing,
  `OutboxPendingDispatch.Drain()` returns `[]` by construction, so an in-process effect recorded there
  is silently discarded. Any PR that "simplifies" the two seams into one must answer that first.
- The pipeline-order unit test (`FluentValidationExtensions.cs:28-32` names it as a blocking finding)
  must know about this behavior, or the ADR-0002 D4 ordering guarantee silently weakens.
- Law 1 is the one that will erode. The mechanizable form is a tripwire test in the shape of
  `SendPushNotificationSeamTripwireTests` — see ADR-0038 CH-6 (open).
