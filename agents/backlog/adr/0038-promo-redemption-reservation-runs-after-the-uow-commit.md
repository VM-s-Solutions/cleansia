# ADR-0038 — The promo-code redemption reservation runs **strictly after** the `UnitOfWork` commit, on a new in-process **post-commit effect** seam; a change-tracked insert ships **first** as a named interim and is retired by that seam

- **Status:** proposed   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-02
- **Supersedes:** —
- **Superseded by:** —
- **Backs / extends:** **ADR-0035 §D3.1/AM-3** (the promo archetype is *reserve-after-persist and
  fail-soft* **by design** — this ADR makes "post-persist" mean *post-commit*, which is what AM-3
  already believed it meant), **ADR-0035 §D3.2/AM-4** (which named this exact fallback: the
  post-commit seam, *"which re-opens a bounded orphan window but never a FK violation"*), **ADR-0035
  E-4** (the escalation this ADR discharges), **ADR-0035 AM-6** (the backstop-vs-sole-arbiter unique
  index rule — applied here to the promo index itself), **ADR-0002 D1** (the post-commit contract
  principle and its logged-and-swallowed posture), **ADR-0009 D2 / `patterns-backend.md` §B8** (the
  price is frozen at purchase and is never re-derived downstream).
- **Applies to:** backend | database (index option only — see §Migration impact) | cross-cutting (pipeline)
- **Ticket:** the live-outage fix (PM to number) · **Consumers:** the end-state seam ticket, the
  index-option ticket (⚠️ `ef-migration`), the counter data-repair script

> **⚠️ ADR number.** `0037` was taken by the concurrently-running **order-offerability** panel while
> this ADR was being researched (its files did not exist at the start of this session and did by the
> end). This ADR is **0038**. Re-check `agents/backlog/adr/` before claiming the next number — this is
> the second collision near-miss in as many rounds.

> **⚠️ This ADR is `proposed` and the ruling is nonetheless BINDING ON THE FIX NOW.** A total outage is
> live. The panel protocol (`agents/process/deliberation.md`) requires an author and a *different*
> lead; that second pass has not happened. What that costs and what it does not:
> - **§D3 (the interim) is authorized to ship immediately.** It restores service, needs no migration
>   and no seam, and is proven by an existing integration test against real PostgreSQL. Nothing in a
>   challenger pass can make "every promo booking 500s" the better state.
> - **§D1/§D2 (the end state and its seam) require one challenger pass before this ADR flips to
>   `accepted`.** Three things to attack, named so the pass is cheap: (1) the rejection of the
>   **outbox** route in §D1.3 turns on a *quantitative* claim about the drain window — check the
>   arithmetic; (2) the **five laws** in §D2.2 are what stop the new seam becoming a dumping ground —
>   attack their sufficiency, not their wording; (3) §D5 folds a *second* defect into the outage fix —
>   attack the scope argument.
> - `agents/knowledge/*` edits landed with this ADR are marked **PROPOSED** and are not law until the
>   `## Verdict` is signed by a second instance.

---

## Context

### The defect, proven — not inferred

`src/Cleansia.IntegrationTests/Features/Orders/CreateOrderPromoRedemptionPersistenceTests.cs` drives
the real `CreateOrder` handler through `IMediator` against real PostgreSQL (Testcontainers, the real
`Initial` migration, Respawn between tests — so `FK_PromoCodeRedemptions_Orders_OrderId` is enforced
exactly as in production, which SQLite would not do). With a valid 20% code:

```
Npgsql.PostgresException: 23503: insert or update on table "PromoCodeRedemptions"
violates foreign key constraint "FK_PromoCodeRedemptions_Orders_OrderId"
  at PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync (:99)
  at PromoCodeService.ApplyAsync (:164)
  at OrderPromoApplier.ApplyAsync (:54)
  at CreateOrder.Handler.Handle (:315)
```

Post-failure state, instrumented: `Orders` **0**, `PromoCodeRedemptions` **0**, promo
`CurrentRedemptions` **1**.

The mechanism is fully accounted for in code and needs no inference:

| Step | Evidence |
|---|---|
| The order is only **change-tracked**, never written | `OrderFactory.cs:167` — `orderRepository.Add(order)` is the last statement |
| Nothing commits before the promo call | `OrderPaymentDispatcher.cs:30-74` mints a Stripe session / enqueues a receipt; no commit |
| The commit happens **after the handler returns** | `UnitOfWorkPipelineBehavior.cs:27-30` → `CleansiaDbContext.CommitAsync` (`:67-99`) |
| The reservation **auto-commits in its own implicit transaction** | `PromoCodeRedemptionRepository.cs:99-101` — `SqlQueryRaw` against a non-existent principal row |

So `CreateOrder.cs:315` inserts a child row referencing an `Orders` row that does not exist yet.
ADR-0035 AM-4 predicted this defect in the *membership* path and escalated the *live* promo instance
as **E-4**; this ADR discharges E-4.

### The one property of today's failure that governs the whole ruling

**The failure is not swallowed.** `OrderPromoApplier.ApplyAsync` has **no `try`/`catch`** — its
fail-soft (`OrderPromoApplier.cs:61-66`) only covers a *returned* `applyResult.Success == false`. A
thrown `PostgresException` propagates out through every behavior. The customer gets a **500 and no
booking**.

Therefore **"discount applied, redemption unrecorded, limits unenforced" does not happen today.** It
is precisely what a `try`/`catch` around the failing call would create, and it would be *worse than
the outage*: a 500 is a page, and silence is a quarterly discovery. §D8 turns this into an enforceable
rule instead of a warning.

### Two consequences that ride on the same fix

1. **Global counter leak.** `TryIncrementGlobalRedemptionsAsync` (`PromoCodeRepository.cs:24-48`)
   auto-commits `+1` **before** the reservation; the compensating `DecrementGlobalRedemptionsAsync`
   runs only on `redemption == null` (`PromoCodeService.cs:166-173`), **not on a throw**. So **every**
   promo attempt today permanently burns a global slot: a 100-redemption campaign is dead after 100
   *failed* bookings. This is not a future risk — it is accruing right now in every environment where
   the promo path is exercised. See §D6, including the data repair.
2. **Orphan Stripe session.** On Web + Card, `OrderPaymentDispatcher.DispatchAsync` mints a Checkout
   Session (`:43-45`) before the throw, for an order that never exists. See §D7.

### A third defect, found by reading, that changes the trade-off analysis

The per-user backstop index is declared **nulls-distinct**:

```csharp
// PromoCodeRedemptionEntityConfiguration.cs:66-67
builder.HasIndex(r => new { r.TenantId, r.PromoCodeId, r.UserId, r.SlotOrdinal }).IsUnique();
```

PostgreSQL treats NULLs as **distinct** in a UNIQUE index by default, and single-tenant mode *is*
`TenantId == null` (ADR-0035 AM-6, adjudicated, citing `CleansiaDbContext.cs:239-246` + `CLAUDE.md`).
So wherever `TenantId` is NULL, `ON CONFLICT DO NOTHING` **has no conflict to find**.

And the statement's own `HAVING COALESCE(MAX("SlotOrdinal")+1,0) < @maxPerUser` guard does not cover
for it: under READ COMMITTED two concurrent executions each take a statement-start snapshot, neither
sees the other's uncommitted insert, **both** compute ordinal `0`, **both** pass `HAVING`, and with a
NULL tenant **both land**. The repository comment already concedes the statement needs the index —
it calls it *"defense-in-depth BACKSTOP"* (`:44-46`) — and per ADR-0035 AM-6 an index that is the
**sole arbiter of a concurrent claim** must be `NULLS NOT DISTINCT` or it is not an arbiter at all.

**Consequence for this ruling:** the "single-statement atomic per-user cap" that the tracked-insert
interim is accused of giving up **does not currently exist under concurrency in the deployment that is
live**. That is not a reason to keep the interim forever; it is the reason the interim costs less than
it appears to, and the reason §D5.2 makes the index option part of the end state.

---

## Decision

### D0 — The ruling, in one paragraph

**End state:** the redemption reservation runs **strictly after** the `UnitOfWork` commit, with the
reservation statement **unchanged**, executed in-process on a new, narrow **post-commit effect** seam
(`IPostCommitEffects` + `PostCommitEffectBehavior`), and backed by an index-option change that makes
the unique backstop actually fire. **Interim, shipping first:** the reservation becomes a
**change-tracked EF insert** that rides the existing UoW commit — no migration, no seam, service
restored. **Retirement:** the interim carries a mechanical marker tied to a filed end-state ticket and
is deleted in the PR that lands the seam; §D4 states the trigger and the acceptance test. **Also in
scope of the same fix, mandatory:** the trigger predicate moves from the *preview* to the *persisted
order* (§D5.1) — otherwise the interim ships a new customer-harming defect on day one. **Not in scope:**
the orphan Stripe session (§D7), which the fix removes in its promo-caused form and which must not be
"fixed" along the way.

### D1 — The reservation is a **post-commit effect**

**The rule:** *no write that references `Order.Id` under a foreign key may execute before the
`UnitOfWork` commit that creates that order.* The reservation self-commits, so it must run after.

Three properties fall out, and they are the reason this is the end state and not just a bug fix:

1. **The FK is satisfied structurally, not by luck.** The order is durably committed before the child
   row is attempted. There is no ordering left to get wrong.
2. **The single-statement atomic cap survives byte-for-byte.** `TryReserveRedemptionSlotAsync` does
   not change. `null` still means "no slot" — a **result**, never an exception, never a rollback of a
   paid order. This is the property the whole design exists to protect
   (`PromoCodeRedemptionRepository.cs:48-53`).
3. **Fail-soft becomes structurally real.** `OrderPromoApplier`'s stated policy — *"failure logs but
   never rolls back… Apply runs post-persist"* (`:50-53`) — is currently **false**: "post-persist"
   meant `orderRepository.Add`, not the database, so a failure *did* roll back (by 500ing before the
   commit ran). After the move, a post-commit failure **cannot** roll back the order, because there is
   no transaction left to roll back. The comment stops being aspirational.

This does **not** contradict ADR-0035. AM-3 adjudicated the promo archetype as *reserve-after-persist
and fail-soft*, and defended the soft per-user cap on the asymmetry that a promo requires **possession
of an operator-issued code** while an express waiver requires only a subscription. This ADR changes
*when* "after persist" is, not *whether* the cap is soft. AM-4 named this seam as its own fallback and
said of it: *"never a FK violation."*

#### D1.1 — Why not the outbox (`IPendingDispatch`), even though it is the platform's canonical post-commit mechanism

This was the strongest competing option and it loses on a measurable, not aesthetic, ground.

`OutboxPendingDispatch.Enqueue` writes an `OutboxMessage` row **into the same scoped `DbContext` the
pipeline commits** (`OutboxPendingDispatch.cs:27-40`), so an intent row would exist **iff** the order
committed — the strongest durability available, with retry/backoff/dead-letter for free. The operation
is even naturally idempotent for at-least-once delivery: `PromoCodeService.ApplyAsync` short-circuits
on `GetByOrderIdAsync` (`:90-94`) behind a UNIQUE index on `OrderId` alone
(`PromoCodeRedemptionEntityConfiguration.cs:72-73`).

**It is rejected because of the drain window.** `OutboxDrainerFunction` runs on `*/10 * * * * *` — a
10-second timer, plus queue delivery, plus consumer execution. During that window the per-user
pre-read (`CountForUserAndCodeAsync`, `PromoCodeService.cs:47-53` and `:123-128`) sees **zero prior
redemptions**. That converts the per-user cap exploit from **concurrent** (fire N simultaneous
requests — needs tooling) to **serial** (book, wait a second, book again — needs a browser). A
one-shot code becomes an N-shot code for anyone with 30 seconds. ADR-0035 AM-3 conceded a *soft* cap;
it did not concede a **serially farmable** one, and widening a money-path window by ~4 orders of
magnitude is a decision, not an implementation detail.

Two secondary costs, recorded so the option is not re-proposed on the assumption they are free:
- **Tenant.** A queue consumer has no JWT, so `tenantProvider.GetCurrentTenantId()` is null and the
  reservation would write a null-tenant row for a tenanted order. Every existing consumer handles this
  with an explicit `SetTenantOverride` from the envelope (`GenerateReceiptHandler.cs:61-76`,
  `SendPushNotificationHandler.cs:98-103`, `SendEmailHandler.cs:83-86`) — correct, but it is a fourth
  hand-rolled instance of a pattern, on a money path, for no gain over D1.2.
- **Actor.** `CreatedBy` degrades to `"System"` (`PromoCodeRedemptionRepository.cs:55-56`).

**What is preserved for later:** §D2.1 requires the effect to be a **serializable intent record**
(not a closure). If drift is ever observed (§D6.3's query), adding a durable outbox leg beside the
in-process one is a *pure addition* — the same record becomes the message payload and the consumer is
already idempotent on `OrderId`. Belt-and-braces stays one small ticket away; it is not built today
because the residual it closes is a crash in a millisecond window on a discount ledger, not on money.

#### D1.2 — Why in-process, in the same request scope

`PostCommitDispatchBehavior` is already the **outermost** behavior
(`FluentValidationExtensions.cs:34-37`: `AuditFailureCapture → PostCommitDispatch → Validation →
UnitOfWork → AuditLog → Handler`), so code placed after its `next()` runs **strictly after the commit**
with the request scope still alive. That gives, at no cost: the ambient tenant, the ambient user
session (so `CreatedBy` is the real actor), the same `DbContext`, and **millisecond** latency — the
same window the design has always assumed.

**Rejected: a fire-and-forget `Task.Run` / `IHostedService` hand-off.** It escapes the request scope
(disposed `DbContext`), loses the tenant and the actor, is unobservable in tests, and converts a
bounded in-request effect into an unbounded background one. Someone will propose it because it is
three lines; it is the wrong three lines.

### D2 — The seam: `IPostCommitEffects`, and the five laws that keep it narrow

`PostCommitDispatchBehavior` drains queue messages only — and under the durable backing
`OutboxPendingDispatch.Drain()` returns `[]` by construction (`:42`), so `IPendingDispatch` **cannot**
be overloaded to carry an in-process effect. A second, explicitly-scoped buffer is required.

#### D2.1 — Shape (interface sketch; the implementing ticket owns the code)

```csharp
// Cleansia.Core.AppServices/Behaviors/IPostCommitEffects.cs — registered SCOPED (per request).
public interface IPostCommitEffects
{
    // Records a LOCAL, IDEMPOTENT effect to run strictly after the UnitOfWork commit succeeds.
    // Idempotent within a request on the effect's natural key: recording the same key twice runs once.
    void Record<TEffect>(TEffect effect) where TEffect : IPostCommitEffect;
    IReadOnlyList<IPostCommitEffect> Drain();
}

// A serializable intent record — NOT a closure. (D1.1: this is what makes a future durable leg additive.)
public interface IPostCommitEffect { string EffectKey { get; } }

public interface IPostCommitEffectExecutor<in TEffect> where TEffect : IPostCommitEffect
{
    Task ExecuteAsync(TEffect effect, CancellationToken cancellationToken);
}

// The promo instance:
public sealed record PromoRedemptionEffect(
    string OrderId, string PromoCodeId, string UserId, decimal AppliedDiscount, decimal RawSubtotal)
    : IPostCommitEffect { public string EffectKey => $"promo-redemption:{OrderId}"; }
```

**Registration:** `PostCommitEffectBehavior<,>` is registered **immediately inside**
`PostCommitDispatchBehavior<,>` (i.e. between it and `ValidationPipelineBehavior`), which makes
execution order **effects first, queue dispatch second**. Deliberate: a local DB effect is retractable
in a way an external wire send is not, so it runs while the cheaper failure is still the only one that
has happened. The predicate is the same `response is BusinessResult { IsSuccess: true }` the other two
behaviors use (ADR-0002 "predicate alignment", C7). **The pipeline-order unit test must be extended in
the same PR** — `FluentValidationExtensions.cs:28-32` already warns that a re-swap is a blocking
finding, and that guarantee only holds if the test knows about the new behavior.

#### D2.2 — The five laws (what may go on this seam)

A seam that will take anything becomes a dumping ground and the CQRS/UoW boundary erodes one
"just this once" at a time. An effect is admissible **only if all five hold**:

1. **It is local to this database.** External side effects (queue, HTTP, Stripe, email, push) go to
   `IPendingDispatch`/the outbox, which is durable and already adjudicated (ADR-0002 D1). **This seam
   is not a second dispatch mechanism.** The discriminator, stated once so it is quotable:
   *durable-external → outbox; local, idempotent, must-not-join-the-order's-transaction → post-commit effect.*
2. **It is idempotent on a natural key of persisted state** — here `OrderId`, backed by the UNIQUE
   index on `PromoCodeRedemptions.OrderId` and the `GetByOrderIdAsync` short-circuit. An effect whose
   second execution does something is not admissible.
3. **It owns its own commit.** The pipeline's commit has already happened and will not happen again.
   An effect persists via a self-committing statement (the reservation already is one) or an
   explicitly-committed repository call, and says so in a doc-comment using the existing
   sanctioned-exception vocabulary (`PromoCodeRedemptionRepository.cs:48-53`). **Adding a tracked
   entity in a post-commit effect and expecting it to save is a silent no-op** — the most likely way
   to misuse this seam.
4. **It cannot make the request fail.** The executor catches, logs at **Error** with a stable event
   name, and does **not** rethrow — the operation already committed (ADR-0002 D1: *"logged and
   swallowed — never converted into a 500"*, the fiscal-compliance invariant). This is admissible only
   because §D8's three conditions hold; it is not a general licence.
5. **Its failure is detectable without the log.** A named reconciliation predicate over persisted
   state must exist and be written into the effect's doc-comment. For the promo effect it is §D6.3.

**Failing law 1 is the common case and the answer is always the outbox.** Failing law 2 or 3 means the
work belongs *inside* the handler's unit of work, not after it.

### D3 — The interim: a change-tracked insert, shipping first

**Authorized to ship immediately**, ahead of the seam, because the alternative is leaving a total
outage live for a day. `TryReserveRedemptionSlotAsync`'s raw self-committing INSERT is replaced (behind
the same repository method, via DI only) by a **change-tracked** `PromoCodeRedemption` added to the
scoped `DbContext`, with the ordinal from a pre-read. EF's dependency-ordered command batch places the
`Orders` INSERT before the dependent `PromoCodeRedemptions` INSERT inside the single
`SaveChangesAsync`, so the FK is satisfied — the same mechanism ADR-0035 AM-4 relies on, and the
backend agent's integration test already proves it end to end (row lands, `SlotOrdinal == 0`).

It also gets two things right that the raw statement had to hand-roll: `CommitAsync` stamps
`CreatedBy`/`CreatedOn` **and** `TenantId` for tracked `Added` entities (`CleansiaDbContext.cs:74-93`),
so the `42P08` untyped-tenant-parameter class of bug (`PromoCodeRedemptionRepository.cs:85-93`) cannot
recur on this path.

**What it gives up — stated precisely, because the usual framing overstates it:**

| Property | Today (intended) | Interim, `TenantId` **NULL** (single-tenant — the live deployment) | Interim, `TenantId` non-null |
|---|---|---|---|
| Serial re-use of a used code | refused cleanly (`HAVING` → `null`) | refused cleanly (app pre-read, `PromoCodeService.cs:123-128`) | same |
| **Concurrent** double-redeem | **already broken** — both pass `HAVING`, `ON CONFLICT` finds no conflict (§Context) | **same, unchanged** | index fires → `DbUpdateException` at commit → **the whole order rolls back** (a 500) |
| Refusal surfaces as | a `null` result | a `null` result | an **exception**, not a result |

So: **in the deployment that is live, the interim loses nothing on the per-user cap** — the property it
is accused of surrendering is not currently held. In a tenanted deployment it trades a clean refusal
for a rolled-back order on a genuine race (realistically: a double-click). That is worse than the end
state and **better than a 100% outage**, and it is bounded — the user retries and the first order is
already there.

**Interim residual that cannot be fixed within the interim's shape:** the global increment still
auto-commits before the tracked insert, and the commit now happens *outside* the handler, so the
handler cannot compensate a commit-time failure. The leak therefore narrows from **every promo order**
(today) to **only orders whose commit fails** — monotonic improvement, but named. §D6 closes it in the
end state.

### D4 — The retirement trigger (mechanical, not a promise)

An interim with no named end state becomes permanent. Three bindings, all checkable:

1. **A marker in code.** The interim carries, on the changed method,
   `// INTERIM(ADR-0038 §D3 → <ticket-id>): change-tracked insert; delete when IPostCommitEffects lands.`
   The ticket id is filled in by the PM **before the interim merges** — an unfilled marker is a review
   block.
2. **A mechanical anti-orphan check** (`agents/tools/check-consistency.mjs`, per
   `process/enforcement.md`): *every `INTERIM(ADR-NNNN → T-xxxx)` marker in `src/` must reference a
   ticket id present and open in `agents/backlog/INDEX.md`*. This generalizes past this ADR — it makes
   "interim with no named end state" impossible as a class, which is the actual recurring failure mode.
3. **The acceptance test for retirement**, so "is it retired?" is not a judgment call. The end-state PR
   deletes the marker and must restore **both** properties:
   - **P1 — refusal is a result, never an exception.** A test asserting that a second redemption of a
     one-shot code by the same user returns `PerUserLimitReached` and **leaves the first order intact**.
   - **P2 — the database is the arbiter under concurrency.** A real-PostgreSQL test firing two
     concurrent redemptions of a one-shot code **with a NULL tenant** and asserting exactly **one** row.
     P2 fails today and fails under the interim; it passes only with §D5.2's index option. It is the
     honest definition of "the property was restored", not "the property was returned to its previous
     state" — because its previous state was broken.

**The interim does not get to outlive its ticket.** If the end-state ticket is deprioritized, that is
an owner decision made explicitly, not by drift — the marker plus check (2) keeps it visible.

### D5 — Two things that ship **with** the fix, not after it

#### D5.1 — The trigger predicate: drive from the **persisted order**, never the preview (mandatory, same change)

`CreateOrder.cs:315` calls `ApplyAsync` whenever `preview.DiscountAmount > 0`. But
`OrderFactory.ResolveLoy003Discount` may have **discarded** the promo in favour of a larger
membership+tier combination (`OrderFactory.cs:208-215`), in which case the order is persisted with
`PromoDiscountAmount = null` and `PromoCodeId = null` (`:94-95`). The redemption is recorded anyway —
**burning a customer's one-shot code for a discount they never received.**

**This is in scope for the same fix and is not a separate ticket.** The argument is not convenience:
the defect is *currently unreachable* because everything throws first, and it becomes **live the moment
the interim lands**. Shipping the interim without it means shipping a new customer-harming defect on
day one, caused by our own fix. Splitting it would be splitting a change from its own precondition.

The rule, which is also the seam: **`Order` is the sole source of truth for what was applied; the
preview is an estimate that the factory is free to discard.**

- **Gate on** `order.PromoCodeId is not null && order.PromoDiscountAmount > 0` — exactly the resolution
  the factory persisted. Not `preview.DiscountAmount`.
- **Pass `rawSubtotal`**, not `order.TotalPrice + preview.DiscountAmount`. That re-gross is *already*
  wrong on express orders: `finalTotalPrice = ApplyExpressSurcharge(RawSubtotal − appliedAmount, …)`
  (`OrderFactory.cs:100-102`), so adding the discount back does not reconstruct the subtotal. The
  handler already has `rawSubtotal` (`CreateOrder.cs:275`).
- **End state: record the frozen amount, stop recomputing.** `ApplyAsync` re-derives the discount via
  `ComputeDiscount` (`PromoCodeService.cs:143`); moving post-commit widens the window in which an admin
  edit to the code makes the *recorded* `AppliedDiscount` disagree with the *charged*
  `Order.PromoDiscountAmount`. §B8 / ADR-0009 D2 already say the price is frozen at purchase and is
  never re-applied downstream — the ledger row must carry `order.PromoDiscountAmount` verbatim. (Interim
  keeps the recompute with the corrected subtotal, so the two agree except under a mid-request admin
  edit; the end state removes the window.)

#### D5.2 — The backstop index becomes `NULLS NOT DISTINCT` (⚠️ needs an EF migration — owner-only)

Per ADR-0035 AM-6's adjudicated rule, a unique index that is the **sole arbiter of a concurrent claim**
must be `NULLS NOT DISTINCT`; only a *backstop behind an authoritative app-level assert* may stay
nulls-distinct. `(TenantId, PromoCodeId, UserId, SlotOrdinal)` is the sole arbiter (the `HAVING` guard
demonstrably is not — §Context) and is therefore in the first category.

```csharp
builder.HasIndex(r => new { r.TenantId, r.PromoCodeId, r.UserId, r.SlotOrdinal })
       .IsUnique()
       .AreNullsDistinct(false);   // ADR-0035 AM-6 — sole arbiter of a concurrent claim
```

The provider already emits this twice in the committed `Initial` migration
(`FiscalCounterEntityConfiguration.cs:23-29`, `LiveActivityTokenConfiguration.cs:26-28`), so this is
the platform's existing answer, not a deviation. **The reviewer checks the emitted DDL, not the C#.**

⚠️ **This is the only part of this ADR that requires an EF migration.** Migrations are owner-only in
this project — route it to the owner as a `MANUAL_STEP`. It is **not** on the outage path: the interim
and the end-state seam both work without it; what it adds is P2 in §D4. **Pre-migration check:** if any
environment already holds duplicate `(NULL, code, user, ordinal)` rows from the race, the index
creation will fail — de-duplicate first (the `OrderId` unique index guarantees at most one row per
order, so the duplicates are distinguishable and one can be re-ordinal'd rather than deleted).

### D6 — The global counter: three leaks, three answers, plus a repair that must not be skipped

| # | Leak | Fires when | Answer |
|---|---|---|---|
| 1 | Increment commits, reservation throws `23503` | **every promo order, today** | Removed by §D1/§D3 — the throw is gone |
| 2 | Increment commits, the order's commit then fails | interim only, rare | Accepted interim residual (§D3) — unfixable in the interim's shape; removed by the end state, where the increment fires only after a committed order |
| 3 | Increment commits, the reservation throws for **any other** reason (transient DB error, timeout) | rare, today and after | **Fixed in the same change:** the compensating decrement must run on **any** non-success of the per-user reservation, not only the `null` return |

**On leak 3 and the try/catch prohibition — the distinction, stated so this ADR cannot be misread as
licensing a swallow.** The prohibited catch is the one that **absorbs the failure and lets the caller
believe the redemption succeeded**. The required catch here **compensates and then surfaces**: release
the global slot, then return the failure result (post-commit, per §D2.2 law 4, "surface" means log at
Error with the stable event name + the counter, not rethrow into a committed request). A catch that
restores an invariant is the opposite of a catch that hides one.

**D6.3 — Detection (required by §D2.2 law 5).** The named reconciliation predicate:

```sql
SELECT o."Id", o."PromoCodeId", o."PromoDiscountAmount", o."CreatedOn"
FROM   "Orders" o
WHERE  o."PromoCodeId" IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM "PromoCodeRedemptions" r WHERE r."OrderId" = o."Id");
```

This is exact **because of §D5.1**: `Order.PromoCodeId` is non-null **iff** a promo actually priced the
order. It matches two populations — a genuinely refused reservation (cap race loser: a *result*, and a
known-soft outcome per ADR-0035 AM-3) and a lost effect (crash in the post-commit window). They are
distinguished by the Error-level log line, which is why the log is not optional. **This is a report, not
an auto-repair**: an auto-correcting sweep would race the increment→reserve window and is deliberately
not built.

**D6.4 — The one-off data repair (do not skip; the fix alone does not un-burn the slots).** Every promo
booking attempted since the bug shipped has incremented `CurrentRedemptionsCount` with no row to show
for it. Campaigns can be *already dead* in DEV and anywhere else the path was exercised. A `sql-scripts/`
repair (a script, **not** a migration) reconciles the denormalized counter to the ledger:

```sql
UPDATE "PromoCodes" p
SET    "CurrentRedemptionsCount" = (
           SELECT COUNT(*) FROM "PromoCodeRedemptions" r WHERE r."PromoCodeId" = p."Id")
WHERE  p."CurrentRedemptionsCount" <> (
           SELECT COUNT(*) FROM "PromoCodeRedemptions" r WHERE r."PromoCodeId" = p."Id");
```

Run it **after** the fix is deployed and during low traffic: it races the increment→reserve window, so
running it before the fix (when that window is 100% of attempts) would repair a state the very next
booking re-breaks. ±1 under concurrent load is acceptable for a one-shot repair; it is exactly why this
is not wired up as a recurring job.

### D7 — The orphan Stripe session: named, bounded, and deliberately **not** fixed here

The promo-caused instances **disappear with the fix** — there is no throw after the session is minted.
What remains is pre-existing and independent: `OrderPaymentDispatcher.DispatchAsync` mints the session
(`:43-45`) *before* the pipeline's commit, so **any** commit failure orphans one. Scope: **Web + Card
only** — the Mobile channel returns a null session id by design (`:36-39`).

**Severity, argued rather than assumed:** the session URL reaches the customer only inside a successful
`CreateOrder.Response`. A session orphaned by a commit failure is therefore **unreachable**, no charge
can occur, and Stripe expires it. No money moves; no ledger row exists.

**Ruling: no fix in this change, and two "obvious" fixes are pre-rejected** so nobody slips one into
the outage PR:
- **Do not move the Stripe call post-commit.** `Response.StripeSessionId` (`CreateOrder.cs:226-229`) is
  part of the client contract on three surfaces; the session id must exist before the response is
  built. This is the one external effect that genuinely cannot be deferred, which is exactly why it is
  not on the §D2 seam.
- **Do not wrap the handler in an explicit transaction** to commit before dispatching. That relocates
  the commit out of `UnitOfWorkPipelineBehavior` and breaks ADR-0002 D1/D5's guarantee that post-commit
  dispatch happens strictly after *the pipeline's* commit — the same invariant this ADR is built on.

If it is ever worth closing, the shape is a sweep over sessions with no matching order, keyed on the
order id already carried in the session — its own ADR, on its own evidence.

### D8 — Fail-soft admissibility (the catalog rule this outage earns)

The instruction "do not wrap this in a try/catch" is correct but unenforceable as folklore. The rule
that makes it checkable — **all three conditions, or the catch is a defect**:

> A `catch` that logs and continues is admissible only when:
> 1. **It is post-commit.** The committed state cannot be rolled back by rethrowing, so a throw buys
>    nothing but a 500 on an operation that already succeeded.
> 2. **The wrapped operation succeeds in the normal case.** Evidence bar: an integration test against
>    **real PostgreSQL** proving the happy path lands its row. Fail-soft over a *deterministic* failure
>    is not resilience — it is a silent outage.
> 3. **The failure is detectable without the log**: a named reconciliation predicate over persisted
>    state (§D6.3 is the template).
>
> A catch that satisfies (1) and (3) but not (2) converts a loud 500 into **silent data loss**, which is
> strictly worse. **That is why this bug must not be fixed with a try/catch**, and it is why the
> integration test that proved the bug is also the acceptance evidence for the fix.

Condition (2) is the one this outage adds to the catalog, and it is the one a reviewer will otherwise
not think to check.

---

## Alternatives considered

| # | Option | Verdict |
|---|---|---|
| A1 | **`try`/`catch` around `ApplyAsync`** | **REJECTED — the worst option.** Fails §D8(2): it swallows a *100%* failure rate, so every promo order silently loses its redemption and both caps go unenforced forever, while looking green. Turns a paged outage into an undetected one. |
| A2 | **`DEFERRABLE INITIALLY DEFERRED` FK** | **REJECTED on correctness.** The reservation auto-commits in its **own** implicit transaction, so deferral is checked at the end of *that* transaction — immediately. It would only help if the statement joined the order's transaction, and if it did, a per-user unique violation would surface at the order's commit and **roll back a paid order** — the exact failure `PromoCodeRedemptionRepository.cs:48-53` exists to avoid. Also needs a migration. |
| A3 | **Commit the order inside the handler** | **REJECTED.** Violates the UoW invariant (`CLAUDE.md`: never call `CommitAsync` in handlers) and breaks ADR-0002 D1/D5's guarantee that post-commit dispatch follows *the pipeline's* commit. Re-scatters commit timing across call sites. |
| A4 | **Tracked insert as the permanent end state** | **REJECTED as an end state, ADOPTED as the interim (§D3).** Permanently trades a clean `null` refusal for a `DbUpdateException` that rolls back a paid order in tenanted deployments, and moves the per-user arbiter from the database into an app pre-read. Acceptable for a day, not forever — §D4 binds its removal. |
| A5 | **Post-commit via the outbox + a Function consumer** | **REJECTED (§D1.1).** Strongest durability, but the 10s drain window converts the per-user cap exploit from concurrent to **serial**. Retained as an additive future leg, which §D2.1's serializable intent record deliberately preserves. |
| A6 | **Fire-and-forget background task** | **REJECTED (§D1.2).** Escapes the request scope: disposed `DbContext`, lost tenant, lost actor, untestable. |
| A7 | **Overload `IPendingDispatch` to carry in-process effects** | **REJECTED.** `OutboxPendingDispatch.Drain()` returns `[]` by construction (`:42`) — the durable backing would silently discard every effect. Also conflates two contracts with different durability guarantees (§D2.2 law 1). |
| A8 | **Raw `Func<>` closures in the post-commit buffer** | **REJECTED.** Untestable, invites arbitrary deferred work with captured state, and forecloses the additive outbox leg. §D2.1 requires typed, serializable intent records. |
| A9 | **Fix the trigger predicate (§D5.1) in a separate ticket** | **REJECTED (§D5.1).** The defect is unreachable today and goes live the moment the interim lands. Splitting it means shipping a new customer-harming defect caused by our own fix. |

---

## Migration impact (explicit — migrations are owner-only in this project)

| Part | EF migration? |
|---|---|
| §D3 interim (tracked insert) | **No.** Existing table, existing indexes, existing entity. |
| §D5.1 trigger predicate + subtotal | **No.** Call-site change only. |
| §D1/§D2 end-state seam | **No.** Pipeline behavior + scoped service; no schema. |
| §D5.2 `AreNullsDistinct(false)` on the per-user index | **YES — ⚠️ `ef-migration`, route to the owner.** Index-option change; regenerates the index DDL. Not on the outage path. De-duplicate any existing NULL-tenant duplicates first, or index creation fails. |
| §D6.4 counter repair | **No — a `sql-scripts/` data-repair script, not a migration.** |
| A2 deferrable FK (rejected) | would have needed one |

---

## Consequences

- **The outage ends in one small PR** with no migration and no seam change (§D3 + §D5.1).
- **A new pipeline behavior exists.** Every command pays one buffer drain (empty in the overwhelming
  majority). The pipeline-order test must be extended, or the ADR-0002 D4 ordering guarantee silently
  weakens.
- **A new seam exists that can be abused.** §D2.2's five laws and the role card are the mitigation; the
  first misuse to expect is law 3 (adding a tracked entity in a post-commit effect and expecting it to
  save — a silent no-op).
- **The per-user cap is, and remains, soft** for the discount itself (ADR-0035 AM-3 adjudicated this).
  This ADR narrows the window from "the whole request" to "milliseconds", and §D5.2 makes the concurrent
  case genuinely arbitrated for the first time.
- **`Order` becomes the single source of truth for applied discounts** (§D5.1) — which is also what
  makes the §D6.3 detection query exact. Any future discount source must persist its applied amount on
  the order or it is undetectable.
- **A known residual remains:** a crash between the commit and the post-commit effect loses one
  redemption record. Bounded to milliseconds, detected by §D6.3, closed by the additive outbox leg if it
  is ever observed.
- **Interim risk carried for the interim's life:** in tenanted deployments a genuine same-user race
  rolls back the loser's order with a 500 (§D3).

---

## How a reviewer verifies compliance

1. **No `try`/`catch` was added around a pre-commit `ApplyAsync`.** Grep `OrderPromoApplier.cs` for
   `catch` — the only admissible catch is §D6's compensating one, inside `PromoCodeService.ApplyAsync`,
   which **releases the global slot** before returning/logging. A catch that only logs, pre-commit,
   fails §D8(1) and (2).
2. **The integration test passes unchanged.** `CreateOrderPromoRedemptionPersistenceTests` is the
   acceptance evidence (§D8(2)). It must **not** be edited to accommodate the fix; if it needs editing,
   the fix is wrong.
3. **The trigger predicate reads the order, not the preview.** `CreateOrder.cs` gates on
   `order.PromoCodeId` / `order.PromoDiscountAmount`; `preview.DiscountAmount` must not appear in the
   apply gate. Test: membership+tier beats promo → order persisted with `PromoDiscountAmount == null`
   → **zero** `PromoCodeRedemptions` rows.
4. **The subtotal passed to apply is `rawSubtotal`**, not `order.TotalPrice + preview.DiscountAmount`.
   Test with an express-window `CleaningDate` — the old expression is provably wrong there.
5. **Interim only:** the `INTERIM(ADR-0038 §D3 → <ticket-id>)` marker exists, with a **filled, open**
   ticket id. An unfilled marker blocks the PR.
6. **End state only:** the marker is **deleted**, `TryReserveRedemptionSlotAsync`'s SQL is
   **byte-identical** to today's (§D1 property 2), and the call site moved to `IPostCommitEffects`.
7. **End state only:** `PostCommitEffectBehavior` is registered **between** `PostCommitDispatchBehavior`
   and `ValidationPipelineBehavior` in `FluentValidationExtensions.cs`, and the pipeline-order test
   asserts it.
8. **Effects are records, not closures** (§D2.1), each with an `EffectKey` and a doc-comment naming its
   detection query (§D2.2 law 5).
9. **§D5.2:** the reviewer checks the **emitted DDL** for `NULLS NOT DISTINCT`, not the C# builder call.
10. **Retirement:** P1 and P2 (§D4) both exist as tests; P2 runs against real PostgreSQL with a **NULL**
    tenant and asserts exactly one row.
11. **§D6.4 repair** is a `sql-scripts/` file referenced in the deploy notes, not an EF migration and
    not a background job.
12. **No new external side effect was moved onto the post-commit effect seam** (§D2.2 law 1). Any
    `IQueueClient` / HTTP / Stripe call inside an effect executor is a violation — it belongs on
    `IPendingDispatch`.

---

## Roles affected

- **NEW** `agents/knowledge/roles/post-commit-effects.md` — `IPostCommitEffects` /
  `PostCommitEffectBehavior` CRC card (the seam, its five laws, its "does NOT know" list).
- **`IPromoCodeService` / `PromoCodeService`** — gains: releases the global slot on **any** reservation
  non-success (§D6); records the frozen applied discount rather than recomputing (§D5.1, end state).
  Still does **not** know when it runs relative to the commit — that is the caller's/seam's decision.
- **`IOrderPromoApplier` / `OrderPromoApplier`** — its stated fail-soft contract becomes true (§D1
  property 3). Gains: it reads the **persisted order**, not the preview. Does **not** know about the
  pipeline; it records an effect and returns.
- **`IPromoCodeRedemptionRepository`** — unchanged in the end state (the statement is preserved
  byte-for-byte); temporarily change-tracked in the interim, behind the same method signature.
- **`IPendingDispatch`** — **unchanged**, and explicitly *not* the carrier for local effects (§D2.2
  law 1 / A7).

---

## Challenge

> Compressed self-panel (§ADR banner). These are the attacks a challenger instance should press; each
> is answered below. The three marked **OPEN** are the ones a second instance must still rule on.

| # | Challenge |
|---|---|
| CH-1 | **The end state creates the very outcome the brief forbids** — "discount applied, redemption unrecorded". A post-commit failure gives exactly that. |
| CH-2 | **The outbox is the platform's adjudicated post-commit mechanism (ADR-0002 D1) and this ADR invents a second one.** Two mechanisms for "after the commit" is the coupling that costs later. **OPEN.** |
| CH-3 | **The interim's degradation is understated.** "The DB is the arbiter" becomes an app pre-read; a `DbUpdateException` rolls back a paid order. |
| CH-4 | **§D5.1 is a second decision in one ADR** — "one decision per ADR. If you're writing two, split." |
| CH-5 | **§D5.2 needs an owner migration, so the end state cannot actually land**, which makes the interim permanent by default. |
| CH-6 | **The five laws are aspirational.** Nothing mechanically stops the second effect from being an HTTP call. **OPEN.** |
| CH-7 | **The §D6.3 detection query cannot distinguish a refusal from a lost effect**, so it is noise, so nobody will read it. **OPEN.** |
| CH-8 | **Latency:** the post-commit reservation is now inside the request, so `CreateOrder` gets slower on the customer's critical path. |

## Defense

- **CH-1 — REBUT.** The brief's prohibition is on a `try`/`catch` that swallows a **deterministic**
  failure; §D8(2) is exactly that distinction promoted to a rule. Today the operation fails **100% of
  the time**; after the move it succeeds in the normal case and the unrecorded outcome shrinks to a
  crash inside a millisecond window — the residual ADR-0002 D1 already adjudicated and accepted for
  every post-commit effect in this platform. And the fail-soft *policy* is not new: ADR-0035 AM-3
  adjudicated the promo archetype as fail-soft **by design**. This ADR does not introduce fail-soft; it
  makes the existing fail-soft claim structurally true instead of false.
- **CH-2 — REBUT, with the boundary written into the catalog.** §D1.1 rejects the outbox on a
  *measured* ground (the 10s `*/10 * * * * *` drain window converting a concurrent exploit into a
  serial one), not on taste. And §D2.2 law 1 states the discriminator in one line —
  *durable-external → outbox; local-idempotent-post-commit → effect* — so the two mechanisms are not
  overlapping choices. A7 additionally shows `IPendingDispatch` **cannot** carry this
  (`OutboxPendingDispatch.Drain()` returns `[]`). §D2.1 keeps the outbox leg additive. **Flagged OPEN**
  because "is one more mechanism worth a 10s window on a *promo* cap?" is a legitimate judgment a
  second instance may weigh differently — and if it rules for the outbox, the interim and §D5.1 are
  unaffected.
- **CH-3 — CONCEDE in part + REVISE.** The framing was wrong in *both* directions and §D3 now carries
  the three-column table: in the **live** (NULL-tenant) deployment the interim loses **nothing** on the
  per-user cap, because the property being "given up" is already broken there (§Context, the
  nulls-distinct finding). In tenanted deployments the challenge stands exactly as stated, is named as
  a residual, and is bounded by the retry. §D4-P2 makes restoring it the retirement test.
- **CH-4 — REBUT.** The decision is *where the redemption record is written relative to the commit*.
  §D5.1 answers *what input that write reads* — the same write. It is a precondition, not a second
  decision: the defect is unreachable until this ADR's own fix lands, and shipping without it means our
  fix introduces a customer-harming bug (A9). Splitting here would split a change from its precondition,
  which is the failure mode the "one decision" rule exists to prevent, not an instance of it.
- **CH-5 — REBUT.** §D5.2 is explicitly **off the critical path**: the interim and the end-state seam
  both land without it. It restores a property (P2) that **does not exist today**, so its absence cannot
  block the retirement of an interim that also does not have it. §D4-P1 is migration-free and is the
  binding retirement test; P2 follows when the owner runs the migration.
- **CH-6 — CONCEDE in part.** Laws 1–5 are prose today; only law 1 is cheaply mechanizable (an
  `IQueueClient`/`HttpClient`/`Stripe` reference inside an `IPostCommitEffectExecutor` implementation is
  greppable, in the shape of the existing `SendPushNotificationSeamTripwireTests`). Added to §D4's
  enforcement item as a follow-up rather than claimed as done. **Flagged OPEN** for a second instance to
  decide whether the tripwire gates the seam's landing or follows it.
- **CH-7 — CONCEDE + REVISE.** §D6.3 now states plainly that it matches two populations and that they
  are separated by the Error-level log line, which is therefore **not optional**; and that it is a
  **report, not an auto-repair**, because an auto-correcting sweep races the increment→reserve window.
  **Flagged OPEN**: a cleaner separation would persist the refusal on the order, which needs a column
  and therefore an owner migration — deliberately not proposed on an outage fix.
- **CH-8 — REBUT.** Two statements (a conditional `UPDATE` and one `INSERT … RETURNING`) against a warm
  connection in the same scope. That is the cost the design has always assumed — today they run
  *earlier* in the same request, not less often. The outbox alternative would remove them from the
  request and cost the §D1.1 window; that trade was made explicitly.

## Verdict

*Author-only. A second instance must sign this section before the status flips to `accepted`.*

**Provisionally ruled** (binding on the fix now, per the ADR banner):

1. **Ship §D3 + §D5.1 immediately, in one PR.** Interim tracked insert + order-driven trigger predicate
   + the §D6 compensating release. No migration. The existing integration test is the acceptance
   evidence and must not be edited.
2. **Run §D6.4's counter repair after that deploy.** Without it, campaigns stay dead.
3. **End state is §D1/§D2**: reservation strictly post-commit, statement unchanged, on `IPostCommitEffects`.
4. **Retirement is §D4**: marker + anti-orphan consistency check + P1/P2. The interim does not outlive
   its ticket.
5. **§D5.2 is `ef-migration` — owner-only**, off the critical path, and is what makes P2 achievable.
6. **§D7 (orphan Stripe session): not fixed here**, and the two tempting fixes are pre-rejected.
7. **§D8 enters the catalog** as the fail-soft admissibility rule (marked PROPOSED until this Verdict
   is signed).

**Still open for the second instance:** CH-2 (one post-commit mechanism or two), CH-6 (tripwire before
or after the seam lands), CH-7 (whether the refusal deserves a persisted marker and therefore a
migration).
