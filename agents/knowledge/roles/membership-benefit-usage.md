# Role — `MembershipBenefitUsage` + `IMembershipBenefitUsageRepository` (CRC card)

> **✅ ACCEPTED AND SHIPPED.** Retires the *"PROPOSED — not yet the standard … do not build against
> this card until ADR-0035 is `accepted`"* banner this card carried. **ADR-0035's own status line reads
> `accepted`** (`agents/backlog/adr/0035-metered-membership-benefit-usage.md:3`, adjudicated 2026-08-02
> with 16 binding amendments), the feature is live, and `CLAUDE.md` §*"Metered membership benefits"*
> documents it as shipped behaviour. **Retires when:** ADR-0035's status line stops reading `accepted`.
>
> ⚠️ **This card was written against the DRAFT and several entries below were amended at adjudication.**
> Corrected 2026-08-09 against the tree; each claim is cited at `file:line`, read that day. The three
> that had gone the *opposite* way of the code are marked **[WAS WRONG]** so the change is legible
> rather than silent.
>
> Ancestor archetype: `PromoCodeRedemption` + `IPromoCodeRedemptionRepository` — **as a statement
> shape, and no longer as a working example.** That path was converted **back** to a change-tracked
> insert under ADR-0038 §D3 (`PromoCodeRedemptionRepository.cs:22`, `:31-42`, interim until T-0532), so
> its raw-SQL reservation is *gone from the tree*. Read this card's own repository for the live form.

## Responsibility (one sentence)

**The row:** be the durable, tenant-scoped, append-then-soft-release record that **one user** received
**one metered benefit** **once** inside **one period** — and, by occupying a slot in a filtered unique
index, *be* the cap rather than merely describe it (`MembershipBenefitUsage.cs:8-25`).

**The repository:** reserve the next free slot **atomically in a single SQL statement** (returning the
row or `null`), stamp the resulting order onto it, release it, and count the live slots for a period
(`IMembershipBenefitUsageRepository.cs`, five methods, nothing else).

## Collaborators

- `User` (`UserId`, FK `Restrict`) and `UserMembership` (`UserMembershipId`, FK `Restrict`) — who
  earned it (`MembershipBenefitUsageEntityConfiguration.cs:40-55`).
- `Order` (`OrderId`, **nullable**, FK `Restrict`) — the booking the benefit was granted on. Null until
  stamped; null past the orphan cutoff means the booking never existed
  (`MembershipBenefitUsage.cs:54-61`).
- The **filtered partial unique index** `(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)`,
  `IsUnique().AreNullsDistinct(false).HasFilter("\"IsActive\" = TRUE")`, named
  `IX_MembershipBenefitUsages_Slot` (`…EntityConfiguration.cs:57-66`) — the actual enforcement. The row
  without the index is a log line. Two supporting indexes exist and are load-bearing:
  `…_Quota` on the quota key (`:70-71`) and `…_Orphans`, filtered `OrderId IS NULL AND IsActive`
  (`:74-76`).
- `ExpressWaiverResolver` — the only reader that decides a price; `GetMyMembership` — the read surface.
- **`ExpressWaiverConsumer` — the only caller of the consuming reservation**
  (`ExpressWaiverConsumer.cs:33`), itself called from **`CreateOrder.Handler`** (`CreateOrder.cs:409`).
  **[WAS WRONG]** *This card said `OrderFactory`.* **AM-9 moved consumption out of the factory** so the
  factory gains zero collaborators, and a thin consumer seam was interposed; `OrderFactory` never
  reserves. `roles/express-waiver-resolver.md` records the same amendment.
- **Release callers: `CancelOrder.cs:145` and `AdminCancelOrder.cs:146`** (both via
  `ExpressWaiverConsumer.ReleaseForOrderAsync`), plus the orphan reclaim
  **`ReleaseOrphanedBenefitReservations.cs:46`**.
  **[WAS WRONG]** *This card named `CleanupStalePendingOrders` as a release caller.* It is not one and
  **structurally cannot be**: it queries `Orders`, and an orphan is by definition a row whose order
  never existed — `IMembershipBenefitUsageRepository.cs:70-71` states exactly this (AM-7).

## Does NOT know

- **What the benefit means, or what it is worth.** It stores a `BenefitKind` discriminator and a slot.
  The 20% surcharge, the express window, and the price live in `BookingPolicy` and the pricing path.
- **How many are allowed.** `maxPerPeriod` is passed *in* to the reservation
  (`MembershipBenefitUsageRepository.cs:70`, supplied as `waiver.Quota` at `ExpressWaiverConsumer.cs:38`);
  it is a `MembershipPlan` property (`ExpressUpgradesPerMonth`), never a column on the row and never a
  constant in the repo.
- **How the period is computed.** It stores a `PeriodKey` string it is handed
  (`MembershipBenefitUsage.cs:36-44`). Calendar-vs-billing, and the time zone the calendar is evaluated
  in, belong to the resolver (ADR-0035 D2).
- **Whether the user has an active membership.** That predicate exists once, in
  `UserMembershipRepository.ActiveForUserQuery` (`:20-31`) / `UserMembership.IsActive`.
- **Why a release happened.** The caller decides (`!hasBeenAccepted`, `CancelledBy != Customer`,
  orphan); the row only knows `IsActive` flipped.
- **The order's price.** It is written *before* the price is final and never reads `Order.TotalPrice`.
- **Which enrolment earned the slot, on any counting path.** `UserMembershipId` is stored
  (`MembershipBenefitUsage.cs:64-67`) as a **support payload column only**; it appears in the
  reservation's `INSERT` list and in **no** `WHERE`, `GROUP BY`, `HAVING` or join anywhere in the
  repository. Verify by grep — a single counting-path occurrence is the AM-19 violation.

## Invariants a reviewer checks

1. `ITenantEntity` — **not** the ADR-0010 tenant-global exception (`MembershipBenefitUsage.cs:26`). The
   reservation runs inside a request that has a JWT and a tenant; a benefit counter that leaks across
   tenants is a billing defect.
2. The unique index is **filtered on `IsActive` and `NULLS NOT DISTINCT`**
   (`…EntityConfiguration.cs:62-66`). Without the filter a released row keeps its ordinal and the
   release frees nothing; without `.AreNullsDistinct(false)` the index never fires in single-tenant mode
   (`TenantId = null`, the platform's default deployment) and quota 2 silently becomes quota 3+ under
   concurrency. It is the **sole arbiter** of the race, which is what makes it mandatory rather than
   stylistic (`consistency.md` §*"Tenant-scoped unique indexes"*, first bullet).
3. The ordinal is the **smallest free** one, derived **inside** the reservation statement over **live**
   rows — `generate_series(0, @max-1)` + `NOT EXISTS` + `ORDER BY g LIMIT 1`
   (`MembershipBenefitUsageRepository.cs:38-63`, rationale `:17-37`) — never from a pre-read count in
   application code, and never from `MAX(...)+1`.
   *(Re-anchored: this invariant used to cite `PromoCodeRedemption.cs:39-41` as the documentation of
   why. That doc-comment now reads the other way — under ADR-0038 §D3 the promo ordinal **is** an
   app-level pre-read, with a named collision residual (`:33-43`). The live rationale is the repository
   doc-comment above; `MembershipBenefitUsage.cs:46-52` states it on the column.)*
4. A full quota returns **`null`** — a result, never an exception that surfaces at the order's commit
   (`MembershipBenefitUsageRepository.cs:109-112`, and the short-circuit at `:74-77`).
5. The nullable `TenantId` parameter is sent as an **explicit `NpgsqlDbType.Text`** —
   **`MembershipBenefitUsageRepository.cs:95-101`**, whose own comment names the failure: `@tenantId`
   is used bare in the `INSERT … SELECT` list *and* in `IS NOT DISTINCT FROM`, so untyped with a NULL
   value PostgreSQL deduces two types for one parameter and refuses the whole statement with `42P08`
   — **in single-tenant mode only**, which is why the promo path shipped that bug past a tenanted test
   run. `FiscalCounterRepository.cs:49-54` pins the same parameter pre-emptively and says why.
   ⚠️ **[CITATION WAS DEAD]** *This invariant cited "`PromoCodeRedemptionRepository.cs:85-93`".* That file
   is **65 lines** and has held no raw SQL since `da88b695`. The invariant is true; only its evidence
   had rotted — the worst combination, because a reader who checks a dead citation concludes the
   invariant is dead too.
6. The reservation **auto-commits outside the MediatR `UnitOfWork` pipeline** — a *declared* exception,
   required for atomicity. The declaration is on the **interface**,
   **`IMembershipBenefitUsageRepository.cs:25-29`** (*"Declared unit-of-work exception (ADR-0035 D3)"*),
   and the statement it governs is `MembershipBenefitUsageRepository.cs:105-107`. Every other write here
   rides the pipeline — including the order stamp, which is a change-tracked update
   (`ExpressWaiverConsumer.cs:57-70` loads tracked, then `AttachOrder`).
   It is **family A entry 3** on `consistency.md`'s roster of sanctioned self-committing writes; if the
   interface doc-comment is deleted the write becomes a deviation.
   ⚠️ **[CITATION WAS DEAD]** *This invariant cited "`PromoCodeRedemptionRepository.cs:99-109`" and
   `IPromoCodeRedemptionRepository:34-39`.* Neither survives: the impl has no such lines, and the
   interface member at `:33-39` now documents the **change-tracked interim**, i.e. the opposite
   property.

## Watch-list

- **The smallest-free-ordinal derivation is a deliberate deviation from the promo archetype's
  `MAX(…)+1`**, required because this ledger has a release path and promo does not. If anyone "fixes"
  it to `MAX+1` — or to a count of live rows — releases stop restoring capacity silently.
  **[WAS WRONG]** *This entry used to call the shipped mechanism "the `COUNT`-of-live ordinal".* A
  count is exactly what AM-5 **rejected** and what the pin falsifies: `TC-BENEFIT-SLOTREUSE-0`
  (`MembershipBenefitReservationTests.cs:42-53`) reserves **2 of 2**, releases the **lower** ordinal,
  and asserts the next reservation takes ordinal **0** — *"a count-of-live ordinal passes the naive
  version of this test … and fails this one"*. The wording of that test is load-bearing; do not
  simplify it to reserve-one-release-one.
- **The second guard is not redundant.** The statement carries *both* the smallest-free derivation and
  a live-count-under-quota check (`…Repository.cs:53-58`), and they answer different questions: with a
  constant quota the second is implied, but a **mid-month plan downgrade** shrinks the quota within a
  period, and without the count guard a release-then-reserve grants a fourth waiver on a two-waiver
  plan (`:17-37`, item 2). Deleting either one is a defect.
- **A second metered benefit** adds an enum value + a `MembershipPlan` column + a resolver — and
  nothing here. If a second benefit needs a *column* on this row, the responsibility above is wrong.
  Note the entity's own qualifier (`MembershipBenefitUsage.cs:9-12`): this is an **order-linked** ledger
  and the whole release rule is in `Order` vocabulary, so a benefit that is not order-shaped reuses the
  entity, index, statement and period key but **owes its own release rule**.
