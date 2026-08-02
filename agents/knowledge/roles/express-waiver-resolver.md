# Role — `ExpressWaiverResolver` / `IExpressWaiverResolver` (CRC card)

> **⚠️ PROPOSED — not yet the standard.** Introduced by **ADR-0035**
> (`agents/backlog/adr/0035-metered-membership-benefit-usage.md`), `proposed`, not yet adjudicated.
> **Direct sibling of `CancellationPolicyResolver`** (`Core.AppServices/Services/`) — same shape, same
> namespace family, same null-user short-circuit, same "returns a record the policy takes as a
> parameter" contract. Read `CancellationPolicyResolver.cs:14-45` and `BookingPolicy.cs:101-111` first.

## Responsibility (one sentence)

Answer, **as a pure read with no side effects**, the question *"does this customer's plan waive the
express surcharge on this booking, and how many waivers are left in this period?"* — returning
`ExpressWaiver(Waived, Remaining, Quota, PeriodKey)`.

## Collaborators

- `IUserMembershipRepository.GetActiveForUserNoTrackingAsync` — the **one** live-membership predicate
  (`UserMembershipRepository.ActiveForUserQuery:20-29` / `UserMembership.IsActive:84-85`). It creates
  no second predicate.
- `MembershipPlan.AllowsExpressUpgrade` + `ExpressUpgradesPerMonth` — the gate and the number.
- `ICountryConfigurationRepository` → `CountryConfiguration.TimeZoneId` — the zone the calendar month
  is evaluated in (UTC fallback, mirroring `GetDashboardStats.ResolveTimeZone:252-266`).
- `IMembershipBenefitUsageRepository` — **read only**: the live slot count for the current `PeriodKey`.
- `BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc, waiverApplies)` — the caller passes this
  resolver's answer as the `bool`.
- Callers: `QuoteOrder`, `CreateOrder.Validator`'s pricing path, `OrderPricingCalculator`, `OrderFactory`.

## Does NOT know

- **How to consume a credit.** It **never** reserves, never writes, never commits. Consumption is one
  call, in `OrderFactory`, to `TryReserveBenefitSlotAsync`. **A resolver that consumes burns a credit
  on every quote and on every price-mismatch rejection** — that is the single hardest constraint this
  seam exists to satisfy (`CreateOrder.cs:159-176` re-runs pricing on every submit).
- **The surcharge rate or the express window.** `BookingPolicy.ExpressSurchargeRate` /
  `ExpressLeadTimeHours` / `StandardLeadTimeHours` are the policy's, not the resolver's.
- **The order's price.** It returns a verdict; the pricing path applies it.
- **Which tenant.** The global query filter scopes its reads.
- **The customer's device time zone.** `X-Time-Zone` / `IUserSessionProvider.GetTimeZoneId()` are
  **forbidden here** — the header is client-supplied and unauthenticated, and a member could straddle
  the month boundary to draw four credits from two months (ADR-0035 D2, alternative A7).
- **The cancellation policy.** Different resolver, different benefit; they share only a shape.

## Invariants a reviewer checks

1. **Zero writes.** Grep the implementation for `Add` / `Commit` / `ExecuteSql` / `TryReserve` — none.
2. **Short-circuits like its sibling:** `string.IsNullOrEmpty(userId)` → no waiver
   (`CancellationPolicyResolver.cs:27-30`); no active membership, `!AllowsExpressUpgrade`, or
   `ExpressUpgradesPerMonth <= 0` → no waiver (`:35-39`'s shape).
3. **Never throws on a bad/missing zone** — null / blank / `TimeZoneNotFoundException` /
   `InvalidTimeZoneException` → `TimeZoneInfo.Utc`. A pricing call site must not 500 over a config row.
4. **Its answer reaches `BookingPolicy` as a `bool`, not as a membership.** `BookingPolicy` must have
   zero occurrences of `Membership` (ADR-0035 §verify #7).
5. **`Remaining` is server-computed and returned**, never recomputed by a client (T-0514 AC4).

## Watch-list

- The resolver is called on the **quote** path, which is anonymous-friendly and hot. Its extra work for
  a guest is one `IsNullOrEmpty` check; for a member it is two indexed reads. If it ever needs a third
  round-trip, revisit — the pricing path is the most latency-sensitive surface in the product.
- If a second metered benefit lands, **do not generalize this class into an
  `IBenefitResolver<TKind>`** on the first repeat. Two sibling resolvers mirroring
  `CancellationPolicyResolver` is the pattern; a generic one hides the per-benefit gate semantics that
  make each one reviewable.
