# Role — `MembershipBenefitUsage` + `IMembershipBenefitUsageRepository` (CRC card)

> **⚠️ PROPOSED — not yet the standard.** Introduced by **ADR-0035**
> (`agents/backlog/adr/0035-metered-membership-benefit-usage.md`), which is `proposed` and has **not**
> been through challengers + lead. Do not build against this card until ADR-0035 is `accepted`.
> Ancestor archetype: `PromoCodeRedemption` + `IPromoCodeRedemptionRepository`.

## Responsibility (one sentence)

**The row:** be the durable, tenant-scoped, append-then-soft-release record that **one user** received
**one metered benefit** **once** inside **one period** — and, by occupying a slot in a filtered unique
index, *be* the cap rather than merely describe it.

**The repository:** reserve the next free slot **atomically in a single SQL statement** (returning the
row or `null`), stamp the resulting order onto it, release it, and count the live slots for a period.

## Collaborators

- `User` (`UserId`, FK `Restrict`) and `UserMembership` (`UserMembershipId`) — who earned it.
- `Order` (`OrderId`, **nullable**, FK `Restrict`) — the booking the benefit was granted on. Null until
  stamped; null past the orphan cutoff means the booking never existed.
- The **filtered partial unique index** `(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)
  WHERE IsActive` — the actual enforcement. The row without the index is a log line.
- `ExpressWaiverResolver` — the only reader that decides a price; `GetMyMembership` — the read surface.
- `OrderFactory` — the **only** caller of the consuming reservation.
- `CancelOrder` / the cleaner + admin cancel paths / `CleanupStalePendingOrders` — the release callers.

## Does NOT know

- **What the benefit means, or what it is worth.** It stores a `BenefitKind` discriminator and a slot.
  The 20% surcharge, the express window, and the price live in `BookingPolicy` and the pricing path.
- **How many are allowed.** `MaxPerPeriod` is passed *in* to the reservation; it is a `MembershipPlan`
  property (`ExpressUpgradesPerMonth`), never a column on the row and never a constant in the repo.
- **How the period is computed.** It stores a `PeriodKey` string it is handed. Calendar-vs-billing,
  and the time zone the calendar is evaluated in, belong to the resolver (ADR-0035 D2).
- **Whether the user has an active membership.** That predicate exists once, in
  `UserMembershipRepository.ActiveForUserQuery` (`:20-29`) / `UserMembership.IsActive`.
- **Why a release happened.** The caller decides (`!hasBeenAccepted`, `CancelledBy != Customer`,
  orphan); the row only knows `IsActive` flipped.
- **The order's price.** It is written *before* the price is final and never reads `Order.TotalPrice`.

## Invariants a reviewer checks

1. `ITenantEntity` — **not** the ADR-0010 tenant-global exception. The reservation runs inside a request
   that has a JWT and a tenant; a benefit counter that leaks across tenants is a billing defect.
2. The unique index is **filtered on `IsActive`**. Without the filter a released row keeps its ordinal
   and the release frees nothing.
3. The ordinal is derived **inside** the reservation statement over **live** rows, never from a
   pre-read count in application code (`PromoCodeRedemption.cs:39-41` documents why).
4. A full quota returns **`null`** — a result, never an exception that surfaces at the order's commit.
5. The nullable `TenantId` parameter is sent as an **explicit `NpgsqlDbType.Text`**
   (`PromoCodeRedemptionRepository.cs:85-93` — the `42P08` production 500 that only fires in
   single-tenant mode).
6. The reservation **auto-commits outside the MediatR `UnitOfWork` pipeline** — a *declared* exception,
   required for atomicity, mirroring `IPromoCodeRedemptionRepository:34-39`. Every other write here
   rides the pipeline.

## Watch-list

- **The `COUNT`-of-live ordinal is a deliberate deviation** from the promo archetype's `MAX(…)+1`,
  required because this ledger has a release path and promo does not. If anyone "fixes" it back to
  `MAX+1`, releases stop restoring capacity silently. `TC-BENEFIT-SLOTREUSE-0` is the pin.
- **A second metered benefit** adds an enum value + a `MembershipPlan` column + a resolver — and
  nothing here. If a second benefit needs a *column* on this row, the responsibility above is wrong.
