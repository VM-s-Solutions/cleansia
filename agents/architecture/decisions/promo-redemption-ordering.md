# Promo redemption ordering — where the reservation runs relative to the commit

> **[ADR-0038](../../backlog/adr/0038-promo-redemption-reservation-runs-after-the-uow-commit.md)**
> (**`accepted` 2026-08-03**, amendments AM-1 … AM-11 after the challenger pass
> `adr/challenges/0038-seam.md`) — the immutable record, with the alternatives and the deliberation
> trail. This file is the **living companion**: the current shape, the trade-off space, and what is
> still open.
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

## Where this actually stands right now (read this before the diagram)

| | Shipped today (`da88b695`) | End state (**T-0532**) |
|---|---|---|
| The reservation | a **change-tracked** EF insert riding the order's own commit | the **unchanged** self-committing statement, run **strictly after** the commit on `IPostCommitEffects` |
| The per-user arbiter | an app-level pre-read | the single atomic statement — **plus** `.AreNullsDistinct(false)` (owner migration) before it is genuinely an arbiter |
| Same-unit-of-work double reservation | **unguarded** (all guards read the DB, the row is only tracked) — unreachable behind one call site, pinned by FT-38.1 | guarded again, structurally (P3) |
| The seam | does not exist | `IPostCommitEffects` + `PostCommitEffectBehavior` + its law-1/3/5 tripwire, all in one PR |

The marker in code is the source of truth for "is this still the interim?":
`INTERIM(ADR-0038 §D3 → T-0532)` on `PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync`. It is
deleted by the PR that lands the end state, and nothing else deletes it.

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
| Latency **(AM-1 — corrected)** | drainer tick ≤10s (`*/10 * * * * *`) **+ idle-queue listener back-off ≤30s** (`host.json` `maxPollingInterval: 00:00:30`) + handler = **~40s worst case**, ~15–25s typical. The drainer only *enqueues* (`OutboxDrainerService.cs:62`) | **adds** milliseconds to the request, and the write is durable **before the response leaves the pipeline** |
| Context | consumer has **no JWT** → `SetTenantOverride` from the envelope | ambient tenant + actor, for free |

**The number that decided it, stated so nobody re-derives it:** what an attacker needs to double-redeem
a one-shot code is **two overlapping requests** on the in-process seam, versus **two requests within
~40s** on the outbox — the jump from *needs concurrency tooling* to *needs a stopwatch*. That is ~2
orders of magnitude, **not** the ~4 the draft claimed, and the in-process side is **not** "milliseconds":
the window starts at the per-user pre-read (`CreateOrder.cs:280`), not at the commit, so it spans the
rest of the request including the Stripe session mint. **Post-commit does not narrow the cap-farming
window at all** — it closes the FK/orphan window and guarantees the ledger row exists before the
customer is told "booked". `.AreNullsDistinct(false)` is the only thing here that makes the concurrent
per-user case actually arbitrated.

**The discriminator, memorized in one line:** *durable-external → outbox; local, idempotent,
must-not-join-the-order's-transaction → post-commit effect.*

`IPendingDispatch` **cannot** be overloaded for the second column: under the durable backing
`OutboxPendingDispatch.Drain()` returns `[]` by construction, so an in-process effect recorded there is
silently discarded.

### The five laws for anything placed on the effect seam

1. Local to this database (external → outbox). — *`(gate pending: T-0532)` → T1-CI*
2. Idempotent on a natural key of persisted state (here: `OrderId` + its UNIQUE index) — **and the
   guard must read *durable* state, not the change tracker** (AM-5). — *T3-HUMAN, Gate 4*
3. Owns its own commit — **a tracked `Add` in a post-commit effect is a silent no-op**. — *`(gate
   pending: T-0532)` → T1-CI*
4. Cannot fail the request: log at Error with a stable event name, never rethrow. — *T3-HUMAN, Gate 4*
5. Failure is detectable **without** the log — a named reconciliation predicate must exist, **keyed on
   an anonymization-stable column** (AM-9). — *`(gate pending: T-0532)` → T1-CI*

Full tier table + why 1/3/5 cannot be gated *before* the seam exists (ADR-0032 D3 anti-vacuity over an
empty corpus): `agents/knowledge/roles/post-commit-effects.md`.

**The effect record carries the frozen amount and not its inputs** (AM-2), and **post-commit an effect
claims inventory, never re-decides eligibility** (AM-3) — a post-commit refusal is unactionable, because
the customer has already been charged the discounted price.

---

## Trade-off space (the map, so it is not re-walked)

| Option | Why it is where it is |
|---|---|
| **Post-commit, statement unchanged** ✅ | FK satisfied structurally; the single-statement atomic cap survives byte-for-byte; fail-soft becomes structurally real (a post-commit failure *cannot* roll back the order); the global-counter leak's dominant form disappears |
| **Tracked insert** ⏳ *interim* | Proven by integration test; no migration; no seam. Trades a clean `null` refusal for a `DbUpdateException` in tenanted deployments — **and (AM-4) disarms the same-unit-of-work idempotency guard in *every* deployment**, see below |
| **Outbox + consumer** ❌ | Strongest durability, but the **~40s** drain window makes the pre-read blind — converting the per-user exploit from *overlapping requests* (needs tooling) to *two requests within 40s* (needs a browser and a stopwatch). Retained as an **additive** future leg |
| **Revert the interim on CH-P2** ❌ | The regression is real; its consequence has **no reachable caller** (one production call site each for `IOrderPromoApplier.ApplyAsync` / `IPromoCodeService.ApplyAsync`). Reverting restores a **100% outage** to close an unreachable path |
| **Persist a refusal marker on the order** ❌ | Would separate §D6.3's two populations — but the log line already carries *more* information than a boolean, the support case is answered before the ledger, and the marker would need its own self-committing write in the very window that can lose the redemption. Owner migration for a negative |
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

### The fifth thing, found by the challenger pass: the interim's guards read a database that cannot see it

`Add(redemption)` (`PromoCodeRedemptionRepository.cs:56`) is `Context.Add` — the **change tracker**.
Every guard on that path is a **database** read: `GetByOrderIdAsync`, `CountForUserAndCodeAsync`, and
the ordinal pre-read. An EF LINQ query never returns an `Added` entity. So for the interim's life, a
second reservation inside one unit of work:

- **misses the idempotency short-circuit** (`PromoCodeService.cs:90-94`) and, on the same `OrderId`,
  violates `IX_PromoCodeRedemptions_OrderId` — unique on a **NOT NULL** column, so nulls-distinct cannot
  save it ⇒ `DbUpdateException` ⇒ **the paid order rolls back, in single-tenant mode too**;
- on a *different* `OrderId` with the same user+code, takes ordinal `0` twice — and there the index
  **is** nulls-distinct, so **both rows land silently** and the per-user cap is over-redeemed with no
  exception anywhere. That is the worse half, and it is the one nobody would notice.

**It is unreachable today**: `IOrderPromoApplier.ApplyAsync` has exactly one production call site
(`CreateOrder.cs:315`) and `IPromoCodeService.ApplyAsync` exactly one (`OrderPromoApplier.cs:58`); the
recurring-booking path never reaches the applier. So the interim was **not reverted** — but its safety
now rests on a *call-graph accident* holding a *safety property*, which is pinned by **FT-38.1** and
retired by §D4-**P3**.

**The reusable rule** (now in `patterns-backend.md` as Corollary 2, and it is the mirror of seam law 3):
*converting a self-committing write into a change-tracked write disarms **every** DB-read guard over it
for the rest of the unit of work.* Re-read the guards whenever you make that conversion.

### Detection: gate on the AMOUNT, not the FK

```sql
SELECT o."Id", o."PromoDiscountAmount", o."PromoCodeId", o."CreatedOn"
FROM   "Orders" o
WHERE  o."PromoDiscountAmount" IS NOT NULL AND o."PromoDiscountAmount" > 0
  AND  NOT EXISTS (SELECT 1 FROM "PromoCodeRedemptions" r WHERE r."OrderId" = o."Id");
```

The draft gated on `PromoCodeId IS NOT NULL` and called it exact. **`Order.AnonymizeCustomerData()`
nulls `PromoCodeId` (`Order.cs:641`) and keeps `PromoDiscountAmount`**, and it runs live
(`DataRetentionBackgroundService.cs:161`, `GdprDeletionService.cs:190`) — so that predicate goes
**blind** over the retention horizon: a false negative, with the report still looking clean. The
anonymizer nulls *identifiers* and preserves *amounts*, by design. **Gate on the amount *instead of*,
never *in addition to*, the id.**

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

### The mirror question, asked again 2026-08-05 and answered from the record: **should promo take the membership shape?**

*"The membership benefit reservation is one atomic `INSERT … SELECT … ON CONFLICT DO NOTHING RETURNING`
that auto-commits **before** the order exists; `OrderId` is stamped afterwards on the unit of work and
orphans are reclaimed by a sweep. Should the promo redemption follow **that** shape instead of
`IPostCommitEffects`?"* It is a fair question — it is **not** in ADR-0038's A1…A11 table — and it needs
no new decision, because **it is ADR-0035's A13 read in the other direction, and A13 is `accepted`
record.** ADR-0035 AM-3 ruled on *both* features in one amendment: the express waiver takes **Mode A,
claim-before-act**; the promo archetype **is** *reserve-after-persist and fail-soft*, and the ground is
an asymmetry that has nothing to do with the mechanism:

> a promo discount requires **possession of a code an operator issued** … an express waiver requires
> **nothing but an active Plus subscription**: a soft cap is farmable by *every* subscriber, with
> concurrent requests alone, at will.

ADR-0038 then changed *when* "after persist" is, and said so explicitly (§D1: *"This ADR changes **when**
'after persist' is, not **whether** the cap is soft"*). So the two shapes are not interchangeable options
that happened to be decided differently — each was argued from what the entitlement costs to obtain, and
the mirror alternative is written into the other ADR as rejected.

**Three structural facts make the swap expensive in this direction specifically** — recorded because the
question will be asked a third time, and because none of them is in either ADR's alternatives table:

| | Express waiver (`MembershipBenefitUsage`) | Promo (`PromoCodeRedemption`) |
|---|---|---|
| `OrderId` | `string?` — **nullable by design**, stamped later by `StampOrderId`, orphans swept | `[Required]`, non-null, **UNIQUE** — the natural key seam law 2 rests on. Making it nullable is an owner `ef-migration` **and** deletes that idempotency key |
| The reservation's answer is… | an **input** to the price: `TryReserveAsync` runs *before* `orderFactory.CreateAsync`, and the reserved waiver is threaded in as a parameter | an **output**: `OrderFactory.ResolveLoy003Discount` may **discard** the promo when membership+tier is larger, so a pre-commit promo reservation must be *released* on a path that is not a failure at all — a **successful** order. That is §D5.1's defect, re-created by construction |
| What an orphan row is | a reserved **capacity** unit; nothing was charged | a **money** record — `AppliedDiscount` — with no order behind it, on an entity documented *"append-only audit row"* |

And the residuals point in opposite directions, which is the choice in one line:

- **Post-commit (chosen):** the residual is a **lost redemption** — a crash in the ms window after the
  commit. Money already moved at the discounted price; the campaign is over-redeemed by one. Detected
  from persisted state by the amount-keyed query below, with **no new job**.
- **Reserve-first + sweep:** the residual is a **reservation that outlives a failed (or promo-discarded)
  order** — a customer told *"you have already used this code"* for a booking that never happened, until
  a sweep that **does not exist for promo** runs. It also leaves §D6 leak 2 open (the global increment
  still precedes the commit) and needs its own compensation on the discard path.

**Disposition: T-0532 proceeds exactly as specified.** No ADR, no supersede, no panel — nothing here
changes a decision; it records where the decision already is. If anyone *does* want to move promo to
Mode A, the instrument is a **superseding ADR against both 0035 AM-3 and 0038**, carrying the migration,
the sweep and the discard-path compensation as costs — not a re-scope of T-0532.

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
| 1 | One post-commit mechanism or two? (CH-2 — the outbox rejection turns on the drain window) | **CLOSED 2026-08-03 — two.** The outbox stays rejected on corrected arithmetic (~40s vs one request duration; ~2 orders of magnitude, not ~4). The discriminator is one line: *durable-external → outbox; local-idempotent-post-commit → effect*. The outbox leg stays **additive**. |
| 2 | Does the law-1 tripwire test gate the seam's landing, or follow it? (CH-6) | **CLOSED 2026-08-03 — neither: it lands *inside* the seam's PR (T-0532).** ADR-0032 D3 makes an empty-corpus tripwire unwritable before the first executor exists; ADR-0032 D2 forbids "follow up later" for a mechanizable zero-baseline rule. Two of three options are closed. |
| 3 | Should a *refused* reservation be persisted on the order, so §D6.3's query stops matching two populations? Needs a column → an owner migration | **CLOSED 2026-08-03 — refused.** The `PromoCodeError` log line carries more than a boolean; the support case is answered on the order before the ledger; and the marker would need its own self-committing write on the refusal path, inside the same window that can lose the redemption. |
| 4 | When does the additive durable outbox leg get built? | Trigger: non-zero unexplained drift in the (now amount-keyed) §D6.3 report |
| 5 | Is a call-graph property an acceptable basis for a safety property, for an interim's life? | **Answered for this interim only: no — pin it (FT-38.1).** The general question (when may a temporary implementation rely on "nobody calls this twice"?) is not settled here; the answer that *is* settled is that relying on it silently is not allowed. |

---

## Status / history

| Date | Change |
|---|---|
| 2026-08-02 | ADR-0038 authored under a live outage. Interim (tracked insert) + trigger predicate authorized to ship immediately; end state (post-commit effect seam) ruled; §D5.2 index option flagged `ef-migration`. Status `proposed` — one challenger pass outstanding on CH-2/CH-6/CH-7. Discharges ADR-0035 E-4. |
| 2026-08-03 | Interim shipped as `da88b695`. Challenger pass filed (`adr/challenges/0038-seam.md`, 9 findings, 3 blocking). **Panel lead adjudicated; ADR `accepted` with AM-1 … AM-11.** Headlines: outbox latency corrected **~10s → ~40s** *in the catalog as well as the ADR* (it was published as the general outbox figure and mispriced every seam choice); the interim's *"loses nothing"* justification **struck** and its table given a fourth row — it disarms every DB-read guard over the tracked write for the rest of the unit of work, latent behind a single call site (**FT-38.1** pins it, **P3** retires it); the interim itself **not reverted** (the consequence has no reachable caller and a revert restores a 100% outage); the seam's five laws given ADR-0032 tiers and its tripwire scheduled **inside T-0532**; the interim-marker gate moved out of `check-consistency.mjs` (in **zero** CI workflows) into a `Cleansia.Tests` tripwire, with the pattern fixed to match the marker that actually shipped (`§Dn` explicit + optional) and "open" tightened to "open **and not blocked**"; the detection query re-keyed onto `PromoDiscountAmount` because the anonymizer nulls `PromoCodeId`; `RawSubtotal` dropped from the effect record and post-commit eligibility re-validation ruled out. Open questions 1, 2 and 3 **closed**. |
