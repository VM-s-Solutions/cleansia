# Promo redemption ordering — where the reservation runs relative to the commit

> **[ADR-0038](../../backlog/adr/0038-promo-redemption-reservation-runs-after-the-uow-commit.md)**
> (`proposed`, 2026-08-02) — the immutable record, with the alternatives and the deliberation trail.
> This file is the **living companion**: the current shape, the trade-off space, and what is still open.
>
> Related: **ADR-0035** §D3/§D3.1/§D3.2 (AM-3, AM-4, AM-6) — the membership panel that adjudicated the
> promo archetype's *ordering* and *softness*, and escalated the live promo FK defect as **E-4**, which
> ADR-0038 discharges. **ADR-0002** D1/D5 — the post-commit dispatch contract. **ADR-0009** D2 /
> `patterns-backend.md` §B8 — the price is frozen at purchase.
> Business view: `agents/analysts/` (loyalty/promo). Published view: `docs/architecture/backend.md`.

---

## The one-line shape

**A promo redemption is a self-committing ledger write that references `Order.Id` under a foreign key.
It must therefore run *strictly after* the order's `UnitOfWork` commit — never before it, never inside
it — and it reads what the *persisted order* says was applied, never what the preview estimated.**

---

## Why this needed a decision at all

`OrderPromoApplier`'s doc-comment has said *"Apply runs post-persist so the redemption row gets the
order id"* since it was extracted. That sentence was true of `orderRepository.Add(order)` and **false
of the database**: the commit happens in `UnitOfWorkPipelineBehavior:27-30`, after the handler returns.
So a raw, auto-committing `INSERT` fired against a principal row that did not exist, and every promo
booking threw `23503` and produced no order.

The word "persist" carried two meanings in one codebase — *tracked* and *durable* — and a money-path
ordering rule was built on the ambiguity. That is the reusable lesson, and it is why the decision is an
ADR rather than a bug fix: the same ambiguity is one call site away in any feature that writes a child
row from a handler.

---

## Current shape (as ruled)

```
CreateOrder.Handler
  ├─ preview promo (pure read; the discount folds into the price)
  ├─ OrderFactory.CreateAsync  ── ResolveLoy003Discount may DISCARD the promo (membership+tier wins)
  │                            └─ Order.PromoCodeId / PromoDiscountAmount = what ACTUALLY applied
  ├─ OrderPaymentDispatcher  (Stripe session on Web+Card; receipt intent on Cash)
  ├─ record the redemption EFFECT  ─────────────────┐   (gated on order.PromoCodeId, not the preview)
  └─ return                                          │
                                                     │
UnitOfWorkPipelineBehavior ── COMMIT ────────────────┤
PostCommitEffectBehavior   ── run effects ───────────┘   ← the reservation runs HERE
PostCommitDispatchBehavior ── drain queue messages
```

Pipeline registration (outermost first), `FluentValidationExtensions.cs`:

```
AuditFailureCapture → PostCommitDispatch → [PostCommitEffect] → Validation → UnitOfWork → AuditLog → Handler
```

Effects run **before** queue dispatch (a local DB write is retractable in a way a wire send is not).

### The two seams, and the line between them

| | `IPendingDispatch` (ADR-0002) | `IPostCommitEffects` (ADR-0038) |
|---|---|---|
| For | **external** side effects (queue, email, push, fiscal) | **local**, same-database writes that must not join the order's transaction |
| Durability | outbox row, atomic with the commit; at-least-once | in-process, at-most-once; residual = a crash in a ms window |
| Latency | drainer tick (`*/10 * * * * *`) + delivery | milliseconds, same request scope |
| Context | consumer has **no JWT** → `SetTenantOverride` from the envelope | ambient tenant + actor, for free |

**The discriminator, memorized in one line:** *durable-external → outbox; local, idempotent,
must-not-join-the-order's-transaction → post-commit effect.*

`IPendingDispatch` **cannot** be overloaded for the second column: under the durable backing
`OutboxPendingDispatch.Drain()` returns `[]` by construction, so an in-process effect recorded there is
silently discarded.

### The five laws for anything placed on the effect seam

1. Local to this database (external → outbox).
2. Idempotent on a natural key of persisted state (here: `OrderId` + its UNIQUE index).
3. Owns its own commit — **a tracked `Add` in a post-commit effect is a silent no-op**.
4. Cannot fail the request: log at Error with a stable event name, never rethrow.
5. Failure is detectable **without** the log — a named reconciliation predicate must exist.

---

## Trade-off space (the map, so it is not re-walked)

| Option | Why it is where it is |
|---|---|
| **Post-commit, statement unchanged** ✅ | FK satisfied structurally; the single-statement atomic cap survives byte-for-byte; fail-soft becomes structurally real (a post-commit failure *cannot* roll back the order); the global-counter leak's dominant form disappears |
| **Tracked insert** ⏳ *interim* | Proven by integration test; no migration; no seam. Trades a clean `null` refusal for a `DbUpdateException` **in tenanted deployments only** |
| **Outbox + consumer** ❌ | Strongest durability, but the 10s drain window makes the pre-read blind — converting the per-user exploit from *concurrent* (needs tooling) to *serial* (needs a browser). Retained as an **additive** future leg |
| **`try`/`catch`** ❌❌ | Swallows a 100% failure rate → silent, permanent loss of both caps. Strictly worse than the outage |
| **Deferrable FK** ❌ | The reservation auto-commits in its own transaction, so deferral is checked immediately. If it *did* join the order's transaction, a unique violation would roll back a paid order — the exact failure the design exists to avoid |
| **Commit inside the handler** ❌ | Breaks the UoW invariant and ADR-0002 D1/D5 |
| **Background `Task.Run`** ❌ | Disposed `DbContext`, lost tenant, lost actor, untestable |

---

## The three defects this ruling covers

1. **The FK violation** (the outage) — fixed by ordering. Interim: tracked insert. End state: post-commit.
2. **The wrong trigger** — `CreateOrder` fired on `preview.DiscountAmount > 0`, but
   `ResolveLoy003Discount` may discard the promo when membership+tier is larger. That burns a one-shot
   code for a discount the customer never got. **Unreachable today (everything throws first); live the
   moment the interim lands** — which is why it ships in the same PR, not a follow-up.
   **Rule: `Order` is the sole source of truth for what was applied; the preview is an estimate the
   factory is free to discard.**
3. **The global-counter leak** — the increment auto-commits before the reservation and the compensating
   decrement only ran on a `null` return, not on a throw. Today that means **every** promo attempt burns
   a global slot. Fixed by (a) removing the throw, (b) compensating on *any* non-success, (c) a one-off
   `sql-scripts/` repair — **the fix alone does not un-burn already-lost slots**.

### The fourth thing, found while ruling: the backstop index does not fire

```csharp
// PromoCodeRedemptionEntityConfiguration.cs:66-67
builder.HasIndex(r => new { r.TenantId, r.PromoCodeId, r.UserId, r.SlotOrdinal }).IsUnique();
```

PostgreSQL treats NULLs as **distinct** by default, and single-tenant mode *is* `TenantId == null`. The
`HAVING MAX+1 < max` guard does not cover for it either: under READ COMMITTED two concurrent executions
both see the pre-existing rows only, both compute ordinal `0`, both pass `HAVING`, and with a NULL
tenant **both land**. So **the concurrent per-user cap is already unenforced in the live deployment** —
which is why the interim's "degradation" costs less than it looks like, and why
`.AreNullsDistinct(false)` (ADR-0035 AM-6's sole-arbiter rule) is part of the end state.

⚠️ That index option is **the only part of this area that needs an EF migration** — owner-only, and off
the critical path.

---

## Deliberately not fixed here

**The orphan Stripe session.** `OrderPaymentDispatcher` mints the Checkout Session before the pipeline's
commit (Web + Card only; Mobile returns null by design), so *any* commit failure orphans one. The
promo-caused instances vanish with this fix. The residual is benign — the session URL only ever reaches
the customer inside a successful response, so an orphan is unreachable and expires with no charge.

Two "obvious" fixes are **pre-rejected**, because they will be proposed: moving the Stripe call
post-commit breaks the `Response.StripeSessionId` client contract, and wrapping the handler in an
explicit transaction breaks the ADR-0002 D1/D5 commit-ownership invariant that this whole ruling stands
on. If it is ever worth closing, the shape is a sweep over sessions with no matching order — its own ADR.

---

## What this does NOT reopen

**ADR-0035's A13 stays rejected for the express waiver.** AM-3 rejected reserve-after-persist for the
membership express waiver on *two* grounds: (1) it makes the cap **soft**, and (2) "post-persist" in
this codebase did not mean post-commit anyway. ADR-0038 fixes (2) — but (1) stands on its own and is
the load-bearing half: a promo requires **possession of an operator-issued code**, an express waiver
requires only an active subscription, so a soft cap there is farmable by every subscriber. The
membership path keeps **Mode A, claim-before-act**. The new seam does not change that; it only means a
future membership ticket may use `IPostCommitEffects` for the *order-attach*, which AM-4 already routes
through the UoW.

## Open questions

| # | Question | Status |
|---|---|---|
| 1 | One post-commit mechanism or two? (CH-2 — the outbox rejection turns on the 10s drain window) | Open for the second panel instance |
| 2 | Does the law-1 tripwire test gate the seam's landing, or follow it? (CH-6) | Open |
| 3 | Should a *refused* reservation be persisted on the order, so §D6.3's query stops matching two populations? Needs a column → an owner migration | Open; deliberately not proposed on an outage fix |
| 4 | When does the additive durable outbox leg get built? | Trigger: non-zero unexplained drift in the §D6.3 report |

---

## Status / history

| Date | Change |
|---|---|
| 2026-08-02 | ADR-0038 authored under a live outage. Interim (tracked insert) + trigger predicate authorized to ship immediately; end state (post-commit effect seam) ruled; §D5.2 index option flagged `ef-migration`. Status `proposed` — one challenger pass outstanding on CH-2/CH-6/CH-7. Discharges ADR-0035 E-4. |
