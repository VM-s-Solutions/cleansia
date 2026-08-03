# Role — `IPostCommitEffects` / `PostCommitEffectBehavior` (CRC card)

> **LAW.** Introduced by **ADR-0038**
> (`agents/backlog/adr/0038-promo-redemption-reservation-runs-after-the-uow-commit.md`), `accepted`
> 2026-08-03 by the panel lead's `## Verdict` (amendments AM-1 … AM-11).
> Living doc: `agents/architecture/decisions/promo-redemption-ordering.md`.
>
> **The seam does not exist in code yet** — `IPostCommitEffects` and `PostCommitEffectBehavior` land in
> **T-0532**, together with their tripwire (ADR-0038 §D2.3 / FT-38.3). Until then this card is the
> binding contract for that ticket, not a menu: **the first effect is the promo reservation, and a
> second effect does not get added in the same PR.**

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

## The five laws (an effect is admissible only if ALL hold — ADR-0038 §D2.2/§D2.3)

**Enforced by** (ADR-0032 D2 — every law here constrains code other people will write, so every law
names its enforcer and its tier):

| Law | Enforcer | Tier |
|---|---|---|
| 1, 3, 5 | `PostCommitEffectSeamTripwireTests` (`Cleansia.Tests`, CI at `backend-ci.yml:71`), in the `SendPushNotificationSeamTripwireTests.cs:29-54` shape **including the present-assert** at `:52-53` | **`(gate pending: T-0532)` → T1-CI when T-0532 lands** |
| 2, 4 | `quality-gates.md` **Gate 4 (Architecture)** — already mandatory when an extension point is touched — + the deviating-form list in `consistency.md` §"Post-commit ordering + fail-soft admissibility" | **T3-HUMAN** |

*Why 1/3/5 are not T1-CI **today**, given a zero baseline: ADR-0032 D3 requires a tree-walking guard to
**fail** on an empty corpus, and there are zero `IPostCommitEffectExecutor` implementations — so the
tripwire is literally unwritable before its first subject exists. It lands **inside** T-0532's PR;
"follow-up afterwards" is not an available disposition (ADR-0032 D2). See ADR-0038 §D2.3 / AM-8.*

1. **Local to this database.** External side effects (queue, HTTP, Stripe, email, push) go to
   `IPendingDispatch`/the outbox. *Durable-external → outbox; local-idempotent-post-commit → effect.*
2. **Idempotent on a natural key of persisted state — and the guard must read *durable* state**
   (ADR-0038 AM-5). An effect whose second run does something is not admissible. A guard that queries
   the database cannot see a write the same unit of work has only **tracked**, so such a guard is
   disarmed for the rest of that unit of work and is not a guard. This is the exact mirror of law 3.
3. **Owns its own commit.** The pipeline's commit already happened and will not happen again —
   **a tracked `Add` inside an effect is a silent no-op** (the most likely misuse).
4. **Cannot fail the request.** Catch, log at Error with a stable event name, do **not** rethrow (the
   operation already committed — ADR-0002 D1, the fiscal-compliance invariant).
5. **Failure is detectable without the log** — a named reconciliation predicate over persisted state,
   written into the effect's doc-comment, and **keyed on a column `AnonymizeCustomerData` preserves**
   (an amount, not a source FK — ADR-0038 AM-9, or the predicate goes blind over the retention horizon).

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
  must know about this behavior **and must assert the FULL ordered sequence, not a prefix** — a
  prefix-enumerating test silently tolerates an inserted behavior, and then the ADR-0002 D4 ordering
  guarantee is a comment (ADR-0038 AM-11 / FT-38.6).
- Law 1 is the one that will erode. Its tripwire lands with the seam (FT-38.3) and **must carry the
  present-assert**: "no `IQueueClient` in any executor" over an *empty* executor set passes forever,
  including after someone renames the interface.
- **The intent record must not carry the inputs of a value it also carries** (AM-2). `PromoRedemptionEffect`
  carries the **frozen** `order.PromoDiscountAmount` and **not** the subtotal — hand the executor the
  subtotal and the forbidden recompute (§B8, ADR-0009 D2) becomes a one-liner.
- **Post-commit, an effect claims *inventory*, it does not re-decide *eligibility*** (AM-3). The promo
  executor runs the idempotency check, the global conditional increment and the per-user slot
  reservation — and does **not** re-validate availability, minimum order amount or currency. A
  post-commit refusal is unactionable: the customer has already been charged the discounted price, so
  refusing there can only manufacture *benefit applied, ledger row missing*.
