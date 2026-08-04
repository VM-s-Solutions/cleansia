---
id: T-0532
title: Move the promo-code redemption reservation strictly post-commit onto IPostCommitEffects (retires the ADR-0038 §D3 interim)
status: ready
size: M
owner: architect
created: 2026-08-03
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0038, 0035, 0002]
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 15
source: ADR-0038 §D1/§D2/§D4 — the END STATE the live-outage interim (§D3) is bound to. Filed
  alongside that interim so its `INTERIM(ADR-0038 §D3 → T-0532)` marker references a real, open
  ticket, per §D4 binding 1.
---

## Context

The promo redemption used to be written by a **self-committing raw INSERT** issued from inside
`CreateOrder.Handler`, against an `Orders` row that the UnitOfWork pipeline had not committed yet.
Result: `23503 FK_PromoCodeRedemptions_Orders_OrderId` on **every** promo booking — a total outage.

The interim that shipped (ADR-0038 §D3) makes the reservation a **change-tracked EF insert** that
rides the order's own `SaveChangesAsync`, so EF orders the `Orders` INSERT before the dependent
`PromoCodeRedemptions` INSERT. That restores service with no migration and no new seam, and it is
**explicitly not the end state**:

| Property | Before the outage (intended) | Interim today | End state (this ticket) |
|---|---|---|---|
| Per-user cap arbiter | one atomic SQL statement | an **app-level pre-read** of `MAX(SlotOrdinal)` | the atomic statement again, unchanged |
| Refusal surfaces as | `null` (a result) | `null` **or**, under a non-null tenant on a genuine race, a `DbUpdateException` that rolls back a paid order | `null`, always |
| Global-counter leak | every promo order | only orders whose commit fails (§D3 residual) | none — the increment follows a committed order |

The end state, per **ADR-0038 §D1/§D2**: the reservation runs **strictly after** the pipeline's
commit, with `TryReserveRedemptionSlotAsync`'s SQL restored **byte-for-byte**, executed in-process on
a new scoped seam — `IPostCommitEffects` + `PostCommitEffectBehavior` — registered **immediately
inside** `PostCommitDispatchBehavior` (so: effects first, queue dispatch second). Interface sketch,
the five admissibility laws, and the rejected alternatives (outbox / `Task.Run` / overloading
`IPendingDispatch` / raw closures) are all in ADR-0038 §D2 — **read the ADR, not this summary**.

⚠️ **ADR-0038 is `proposed`, not `accepted`.** §D3 was authorized to ship immediately because the
alternative was leaving a total outage live; **§D1/§D2 are NOT.** Three items are flagged OPEN for a
challenger pass (CH-2 one post-commit mechanism or two · CH-6 tripwire before or after the seam
lands · CH-7 whether a refusal deserves a persisted marker). **This ticket does not start until that
pass signs the `## Verdict`** — if it rules for the outbox, the seam in §D2 is not built at all and
this ticket is rewritten, while the interim and the §D5.1 predicate fix stand either way.

## Acceptance criteria

- [ ] **AC0 — the ADR is `accepted` first.** Given ADR-0038's `## Verdict` is author-only today, When
      this ticket starts, Then a second instance has signed it and CH-2 is resolved. **Do not build
      the seam against a `proposed` ADR.**
- [ ] **AC1 — the statement is restored byte-for-byte.** Given `TryReserveRedemptionSlotAsync`, When
      the end state lands, Then its SQL is identical to the pre-interim raw INSERT (`INSERT … SELECT
      COALESCE(MAX("SlotOrdinal")+1,0) … HAVING … ON CONFLICT DO NOTHING RETURNING`), including the
      explicit `NpgsqlDbType.Text` tenant parameter that fixes `42P08`. **Evidence:** reviewer diffs
      it against `2026-08-02`'s `master` (the pre-interim revision).
- [ ] **AC2 — the marker is deleted.** Given `INTERIM(ADR-0038 §D3 → T-0532)` on the repository
      method and the two doc-comments that name the interim
      (`IPromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync`,
      `PromoCodeRedemption.SlotOrdinal`), When this ticket lands, Then all three are gone and the
      doc-comments state the atomic-reservation contract again.
- [ ] **AC3 — the reservation runs after the commit.** Given a successful `CreateOrder`, When the
      request completes, Then the `Orders` row is committed **before** the reservation statement is
      issued. **Evidence:** the existing `CreateOrderPromoRedemptionPersistenceTests` still passes
      **unedited**, plus an ordering assertion.
- [ ] **AC4 — P1: refusal is a result, never an exception** (§D4). Given a one-shot code already
      redeemed by this user, When they redeem it on a second order, Then `ApplyAsync` returns
      `PerUserLimitReached` **and the second order is created and left intact**. Migration-free —
      this is the binding retirement test.
- [ ] **AC5 — P2: the database is the arbiter under concurrency** (§D4). Given two concurrent
      redemptions of a one-shot code by the same user **with a NULL tenant**, When both run against
      real PostgreSQL, Then exactly **one** `PromoCodeRedemptions` row exists. **This fails today,
      fails under the interim, and passes only once the owner's `.AreNullsDistinct(false)` migration
      lands (§D5.2).** Write the test; if the migration has not run, mark it explicitly and do not
      weaken it into passing.
- [ ] **AC6 — the effect is a serializable intent record, not a closure** (§D2.1). Given
      `PromoRedemptionEffect`, When it is written, Then it is a `record` carrying `OrderId`,
      `PromoCodeId`, `UserId`, `AppliedDiscount`, `RawSubtotal` with an `EffectKey` of
      `promo-redemption:{OrderId}`, and its doc-comment names §D6.3's detection query (law 5).
- [ ] **AC7 — the effect records the FROZEN amount** (§D5.1 end state, ADR-0009 D2). Given the
      reservation now runs after the commit, When the ledger row is written, Then it carries
      `order.PromoDiscountAmount` **verbatim** rather than re-deriving it via `ComputeDiscount` — an
      admin edit to the code mid-request must not make the recorded `AppliedDiscount` disagree with
      what the customer was charged.
- [ ] **AC8 — the pipeline-order test knows about the new behavior** (§D2.1). Given
      `PostCommitEffectBehavior<,>` is registered between `PostCommitDispatchBehavior<,>` and
      `ValidationPipelineBehavior<,>`, When the pipeline-order test runs, Then it asserts that
      position. `FluentValidationExtensions.cs:28-32` already declares a re-swap a blocking finding;
      that guarantee is only real if the test covers the new behavior.
- [ ] **AC9 — the executor cannot fail the request** (law 4). Given the effect's executor throws,
      When it runs, Then it logs at **Error** with a stable event name and does **not** rethrow —
      admissible only because all three §D8 conditions now hold (post-commit ✓, normally succeeds ✓
      per AC3's test, detectable without the log ✓ per AC6).
- [ ] **AC10 — the global increment moves with it.** Given the end state, When a redemption is
      reserved, Then `TryIncrementGlobalRedemptionsAsync` fires only for an order that **committed**,
      closing §D6 leak 2 (the interim's named residual: a promo order whose commit fails still burns
      a global slot today).
- [ ] **AC11 — the anti-orphan check exists** (§D4 binding 2). Given `agents/tools/check-consistency.mjs`,
      When it runs, Then every `INTERIM(ADR-NNNN … → T-xxxx)` marker in `src/` must reference a ticket
      id present and **open** in `agents/backlog/INDEX.md`. Generalizes past this ADR — it makes
      "interim with no named end state" impossible as a class.
- [ ] **AC12 — no external side effect rides the seam** (law 1). Given every
      `IPostCommitEffectExecutor` implementation, When it is walked, Then none references
      `IQueueClient`, `HttpClient` or Stripe — those belong on `IPendingDispatch`. CH-6 is OPEN on
      whether the greppable tripwire (shape of `SendPushNotificationSeamTripwireTests`) gates this
      ticket or follows it; the challenger pass decides.

## Out of scope

- **`.AreNullsDistinct(false)` on `(TenantId, PromoCodeId, UserId, SlotOrdinal)`** — ADR-0038 §D5.2.
  ⚠️ `ef-migration`, **owner-only**, and explicitly **off the outage path**. It is what makes AC5
  achievable; it does not gate AC4. Pre-migration: de-duplicate any existing
  `(NULL, code, user, ordinal)` rows or index creation fails (the `OrderId` unique index guarantees
  at most one row per order, so duplicates are distinguishable and one can be re-ordinal'd).
- **The §D6.4 counter repair** — a `sql-scripts/` data-repair script reconciling
  `PromoCodes.CurrentRedemptionsCount` to the ledger. Run **after** the interim deploys and during
  low traffic. Not a migration, not a background job. Every promo attempt since the bug shipped burnt
  a global slot; campaigns may be **already dead** in DEV.
- **The orphan Stripe session** — ADR-0038 §D7. Bounded, unreachable by the customer, deliberately
  not fixed, and **two tempting fixes are pre-rejected**: do not move the Stripe call post-commit
  (`Response.StripeSessionId` is on three client contracts), do not wrap the handler in an explicit
  transaction (breaks ADR-0002 D1/D5).
- **A durable outbox leg beside the in-process one.** §D2.1's serializable intent record keeps it a
  pure addition if §D6.3 ever shows drift. Not built now.

## Implementation notes

**Do not start with the code.** Start with the challenger pass (AC0). CH-2 is the one that can
invalidate the whole shape.

Files the end state touches (from the ADR, for sizing only):
`Cleansia.Core.AppServices/Behaviors/IPostCommitEffects.cs` (new) ·
`Behaviors/PostCommitEffectBehavior.cs` (new) · `Config/…/FluentValidationExtensions.cs`
(registration order) · `Features/Orders/OrderPromoApplier.cs` (records an effect instead of calling
the service) · `Services/PromoCodeService.cs` (frozen amount, AC7) ·
`Infra.Database/Repositories/PromoCodeRedemptionRepository.cs` (restore the statement, AC1) ·
`agents/knowledge/roles/post-commit-effects.md` (new CRC card).

**The trap to expect first** (ADR-0038 Consequences): law 3 — adding a **tracked** entity inside a
post-commit effect and expecting it to save. The pipeline's commit has already happened and will not
happen again, so a tracked `Add` there is a **silent no-op**. An effect owns its own commit.

**Archetype:** `agents/knowledge/patterns-backend.md` → *"Post-persist" means POST-COMMIT, or the FK
will say so (ADR-0038)* and *Fail-soft is admissible only over an operation that normally SUCCEEDS*.
Both sections are marked **PROPOSED** and flip to law with the ADR.

## Status log
- 2026-08-03 — draft (created by backend while shipping the ADR-0038 §D3 interim, so the interim's
  marker references a real open ticket per §D4 binding 1). **Held out of `ready` by AC0** — ADR-0038
  is `proposed` with three OPEN challenges, one of which (CH-2) can delete this ticket's premise.
  The interim it retires is live and named in code at
  `src/Cleansia.Infra.Database/Repositories/PromoCodeRedemptionRepository.cs:22`.
- 2026-08-04 — **draft → ready** (PM sprint-15 reconciliation). 🔓 **AC0 IS CLEARED.** AC0 read *"Do not
  build the seam against a `proposed` ADR"* — **ADR-0038 was accepted in `f7828fb8`** with *"zero blocking
  challenges remain"*, and CH-2 (one post-commit mechanism or two), the challenge flagged as able to delete
  this ticket's premise, was **ruled and did not**: the outbox rejection SURVIVES, with both its numbers
  corrected (the real worst case is ~40s — drainer plus a 30s idle queue poll — not 10s, and the in-process
  baseline is one request duration, not milliseconds). The rejection survives more cleanly than it was
  argued: two OVERLAPPING requests versus two requests within ~40s.
- 2026-08-04 — **the accepted ADR adds a condition this ticket must carry.** CH-P2 was SUSTAINED IN PART:
  the interim now rests on a **call-graph accident holding a safety property** (`ApplyAsync` has exactly one
  caller, called once). That must be pinned by a one-call-site tripwire **or** restored properly — an
  accident nobody has written down is not a guarantee. Per CH-6 the tripwire lands **inside this ticket's
  PR**, and the binding reason is a rule, not cost: ADR-0032 D3 requires a tree-walking guard to fail on an
  empty corpus, so with zero executors the tripwire is **unwritable** before the seam exists, while D2
  forbids "later". Two of three options are closed by rule.
- 2026-08-04 — **verified at HEAD that this row is still non-orphan (ADR-0038 §D4):**
  `Infra.Database/Repositories/PromoCodeRedemptionRepository.cs:22` carries
  `// INTERIM(ADR-0038 §D3 → T-0532)`.
- 2026-08-04 — **`.AreNullsDistinct(false)` on the promo per-user index is NO LONGER a separate owner
  item** — it was folded into the regenerated `Initial` at `7e1cf7f5` and lands with the database drop. The
  §D6.4 counter repair is still owed and is filed separately as **T-0545**.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
