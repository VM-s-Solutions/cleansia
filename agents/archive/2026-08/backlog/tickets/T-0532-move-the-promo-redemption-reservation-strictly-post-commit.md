---
id: T-0532
title: Move the promo-code redemption reservation strictly post-commit onto IPostCommitEffects (retires the ADR-0038 §D3 interim)
status: done
size: M
owner: architect
created: 2026-08-03
updated: 2026-08-05
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

- [x] **AC0 — the ADR is `accepted` first.** ~~Given ADR-0038's `## Verdict` is author-only today…~~
      **DISCHARGED — re-verified by the architect at HEAD 2026-08-04:** ADR-0038 `:3` reads
      `**Status:** accepted`, its `## Verdict` records *"zero blocking challenges remain"* with
      amendments AM-1…AM-11, and CH-2 — the challenge flagged as able to delete this ticket's
      premise — was ruled and **did not**. This AC is closed; do not re-open it.
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
      real PostgreSQL, Then exactly **one** `PromoCodeRedemptions` row exists.
      ⚠️ **AMENDED 2026-08-04 (architect) — its precondition is now MET, so the escape hatch is
      withdrawn.** The AC used to read *"passes only once the owner's `.AreNullsDistinct(false)`
      migration lands… if the migration has not run, mark it explicitly and do not weaken it."*
      **It has landed.** Verified at HEAD: `PromoCodeRedemptionEntityConfiguration.cs:71` carries
      `.AreNullsDistinct(false)` and it is emitted in the committed `Initial`
      (`Migrations/20260723182623_Initial.Designer.cs:2322`, same in `CleansiaDbContextModelSnapshot.cs:2319`).
      **So AC5 must PASS, not be marked pending.** A test that is skipped, or that asserts two rows, is
      an AC5 failure. *(Four other tenant-scoped sole-arbiter indexes ship the same construct:
      `FiscalCounters`, `MembershipBenefitUsages`, `EmployeePayoutDetails`, `LiveActivityTokens`.)*
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
- [ ] **AC11 — the anti-orphan check exists** (§D4 binding 2). Every `INTERIM(ADR-NNNN … → T-xxxx)`
      marker in `src/` must reference a ticket id present and **open** in `agents/archive/2026-08/backlog/INDEX.md`.
      Generalizes past this ADR — it makes "interim with no named end state" impossible as a class.
      ⚠️ **AMENDED 2026-08-04 (architect) — the enforcer moves, because two governance artifacts named
      two different homes and NEITHER exists.** This AC named `agents/tools/check-consistency.mjs`;
      `agents/knowledge/consistency.md` §"Interim implementations must name their end state" declares
      **`InterimMarkerTripwireTests` in `Cleansia.Tests`**. Grepped at HEAD: `InterimMarker` appears in
      **seven files, all of them documentation** — there is no such test and no such rule. The catalog is
      currently asserting an enforcer that does not exist, which is exactly the ADR-0032 D3 defect
      ("a declared law needs a named, real gate").
      **Ruling — build the one the catalog names: `InterimMarkerTripwireTests` in `Cleansia.Tests`.**
      Two reasons, neither of them preference: (1) the marker lives in C# source and the law governs C#
      interims, so the guard belongs beside them; (2) `Cleansia.Tests` runs in `backend-ci.yml` = **T1-CI**,
      whereas `check-consistency.mjs` is in **zero** workflows = T2-ADVISORY — a law about an interim that
      can outlive its end state deserves the blocking tier. **Anti-vacuity is mandatory** (ADR-0032 D3,
      and ADR-0038's own §D4 check #7): the test must **fail on an empty corpus** — zero markers found in
      `src/` is a non-run, not a pass — and must fail if `INDEX.md` cannot be located. No change to
      `consistency.md` is needed; this AC now agrees with it.
- [ ] **AC12 — no external side effect rides the seam** (law 1). Given every
      `IPostCommitEffectExecutor` implementation, When it is walked, Then none references
      `IQueueClient`, `HttpClient` or Stripe — those belong on `IPendingDispatch`. CH-6 is OPEN on
      whether the greppable tripwire (shape of `SendPushNotificationSeamTripwireTests`) gates this
      ticket or follows it; the challenger pass decides.

## Out of scope

- ~~**`.AreNullsDistinct(false)` on `(TenantId, PromoCodeId, UserId, SlotOrdinal)`** — ADR-0038 §D5.2.
  ⚠️ `ef-migration`, **owner-only**…~~ **STALE — CLOSED 2026-08-04 (architect, verified at HEAD).** It
  was folded into the regenerated `Initial` (`7e1cf7f5`) and ships with the database drop:
  `PromoCodeRedemptionEntityConfiguration.cs:71` + `20260723182623_Initial.Designer.cs:2322`. There is
  no owner item here and no de-duplication step (the database is dropped, so there are no pre-existing
  `(NULL, code, user, ordinal)` rows to reconcile). **The consequence is on AC5, which is now required
  to pass rather than allowed to be marked pending.**
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
`docs/domain/roles/post-commit-effects.md` (new CRC card).

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
- 2026-08-04 — **ARCHITECT DISPOSITION: the ticket STANDS, `ready`, unblocked, and is now three AC
  sharper.** Verified against the tree rather than the ticket text:
  1. **The interim is still live** — `Infra.Database/Repositories/PromoCodeRedemptionRepository.cs:22`
     still carries `// INTERIM(ADR-0038 §D3 → T-0532)`. Nothing has retired it. The ticket is **not**
     already-resolved.
  2. **AC0 is discharged** — ADR-0038 `:3` is `accepted`, CH-2 ruled and the outbox rejection survived.
     Marked `[x]` above so no future reconcile re-derives it.
  3. **AC5's precondition is MET**, so its "mark it pending" escape hatch is **withdrawn** — the index
     ships `AreNullsDistinct(false)` in the committed `Initial`. AC5 must now pass.
  4. **AC11's enforcer was pointing at a file that does not implement it, while the catalog pointed at a
     test that does not exist.** `InterimMarkerTripwireTests` occurs in **seven files, all documentation**.
     Ruled onto the catalog's home (`Cleansia.Tests`, T1-CI) with a mandatory anti-vacuity clause.
     **This is the one genuinely new finding in this pass:** `consistency.md` currently declares an
     enforcer that has never been built — a live ADR-0032 D3 gap on a rule written eight days ago.
  **Retirement conditions: PENDING, not met.** Nothing about the interim has been retired; what changed
  is that every precondition to retiring it is now satisfied, so the work is unblocked in full.

- 2026-08-05 — **PM reconciliation pass 4: the architect's disposition is ACCEPTED; the row is correct as
  it stands.** `ready`, `M`, unblocked, `owner: architect`. Re-verified the one fact that would have
  changed it: `src/Cleansia.Infra.Database/Repositories/PromoCodeRedemptionRepository.cs:22` **still**
  carries `// INTERIM(ADR-0038 §D3 → T-0532)`, so the interim is live, the marker is non-orphan (§D4) and
  the ticket is neither shipped nor stale. **This is the highest-value unshipped `ready` ticket in the
  queue** — everything ahead of it in this pass turned out to be already shipped. Its AC11 finding stands
  and is worth restating for whoever picks it up: `consistency.md` declares an enforcer
  (`InterimMarkerTripwireTests`) that **has never been built** — the name occurs in seven files, all
  documentation. Note **T-0545 retired** in this pass, which removes the §D6.4 counter-repair from this
  ticket's neighbourhood but changes nothing about its own scope.

- 2026-08-05 — **ARCHITECT DISPOSITION (pass 2): the ticket STANDS, unchanged, `ready`, unblocked. No
  ADR, no supersede, no panel — and the question it was re-dispatched with is answered from the
  `accepted` record rather than re-decided.**
  1. **Ground truth re-verified at HEAD first.** The interim is live —
     `src/Cleansia.Infra.Database/Repositories/PromoCodeRedemptionRepository.cs:22` still carries
     `// INTERIM(ADR-0038 §D3 → T-0532)`, and the method below it is the change-tracked
     `Add(redemption)` with the app-level ordinal pre-read. `IPostCommitEffects` / `IPostCommitEffect`
     occur in **exactly one file** in `src/` — that same repository's comment. So nothing has shipped,
     the marker is non-orphan (§D4), and the ticket is **not** one of this session's already-satisfied
     ones.
  2. **The dispatch question — *"should the promo reservation follow the membership shape (atomic
     pre-commit `INSERT … ON CONFLICT DO NOTHING RETURNING` that auto-commits before the order exists,
     `OrderId` stamped afterwards, orphans swept) instead of `IPostCommitEffects`?"* — is real, is NOT in
     ADR-0038's A1…A11 table, and is nevertheless already adjudicated.** It is **ADR-0035's A13 read in
     the other direction**, and A13 is `accepted` record: AM-3 ruled *both* features in one amendment —
     express waiver → **Mode A, claim-before-act**; promo archetype → **reserve-after-persist and
     fail-soft** — on an asymmetry independent of mechanism (*"a promo discount requires **possession of
     a code an operator issued** … an express waiver requires **nothing but an active Plus
     subscription**: a soft cap is farmable by every subscriber"*). ADR-0038 §D1 then says in terms:
     *"This ADR changes **when** 'after persist' is, not **whether** the cap is soft."*
  3. **Three structural facts make the swap expensive in this direction specifically, and none is in
     either ADR's alternatives table** — recorded in `architecture/decisions/promo-redemption-ordering.md`
     §"What this does NOT reopen" so it is not asked a third time: (a) `MembershipBenefitUsage.OrderId`
     is `string?` **by design**; `PromoCodeRedemption.OrderId` is `[Required]` and **UNIQUE** — the
     natural key seam law 2 rests on, so nulling it is an owner `ef-migration` **and** deletes the
     idempotency guard; (b) the waiver reservation's answer is an **input** to the price (threaded into
     `orderFactory.CreateAsync`), while the promo's is an **output** `OrderFactory.ResolveLoy003Discount`
     may **discard** — so a pre-commit promo reservation needs a release on a **successful** order, which
     is §D5.1's defect re-created by construction; (c) an orphaned benefit row is a **capacity** unit,
     an orphaned redemption row is a **money** record (`AppliedDiscount`) on an entity documented
     *"append-only audit row"*.
  4. **The failure modes, in each direction, since the brief asked for both.** Post-commit (chosen):
     the residual is a **lost redemption** — a crash in the ms window after the commit; money already
     moved at the discounted price, the campaign is over-redeemed by one, and it is detected from
     persisted state by the amount-keyed §D6.3 query **with no new job**. Reserve-first + sweep: the
     residual is a **reservation that outlives a failed — or promo-discarded — order**; a customer told
     *"you already used this code"* for a booking that never happened, until a sweep that **does not
     exist for promo** runs; it also leaves §D6 leak 2 open (AC10) and needs its own discard-path
     compensation.
  5. **If anyone still wants Mode A for promo, the instrument is a superseding ADR against BOTH
     ADR-0035 AM-3 and ADR-0038** — carrying the migration, the sweep and the discard-path compensation
     as costs. It is **not** a re-scope of this ticket, and this disposition does not open one: no
     alternative's disposition was changed on an `accepted` ADR, so nothing here needed a panel.
  6. **AC-level state, unchanged by this pass:** AC0 discharged; AC5's precondition met
     (`PromoCodeRedemptionEntityConfiguration` carries `.AreNullsDistinct(false)` and it is emitted in
     the committed `Initial`) so AC5 must **pass**, not be marked pending; AC11's ruling stands and is
     re-verified against the workflow files rather than from memory — **`check-consistency.mjs` appears
     in ZERO `.github/` workflow files** (its only mention anywhere under `.github/` is a comment in
     `nx-project-registration.yml:20-21` citing it as *the counter-example*), while `Cleansia.Tests` is a
     required step in `backend-ci.yml:69-74` with no `continue-on-error`. So `InterimMarkerTripwireTests`
     in `Cleansia.Tests` is the only home of the two that can go red, and the anti-vacuity clause
     (fail on an empty corpus, fail if `INDEX.md` cannot be located) is mandatory.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
