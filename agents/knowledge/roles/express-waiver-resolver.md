# Role — `ExpressWaiverResolver` / `IExpressWaiverResolver` (CRC card)

> **⚠️ NOT YET BUILT — but the decision is settled.** Introduced by **ADR-0035**
> (`agents/backlog/adr/0035-metered-membership-benefit-usage.md`), **`accepted`** 2026-08-02 with 16
> binding amendments, **amended 2026-08-03 by owner instruction (AM-17/AM-18/AM-19)**. T-0493 builds it.
> **Direct sibling of `CancellationPolicyResolver`** (`Core.AppServices/Services/`) — same shape, same
> namespace family, same null-user short-circuit, same "returns a record the policy takes as a
> parameter" contract. Read `CancellationPolicyResolver.cs:14-45` and `BookingPolicy.cs:101-111` first.

## Responsibility (one sentence)

Answer, **as a pure read with no side effects**, the question *"does this customer's plan waive the
express surcharge on this booking, and how many waivers are left in this period?"* — returning
`ExpressWaiver(Waived, Remaining, Quota, PeriodKey)`.

## Collaborators

- `IUserMembershipRepository.GetActiveForUserNoTrackingAsync` — the **one** live-membership predicate
  (`UserMembershipRepository.ActiveForUserQuery:20-31` / `UserMembership.IsActive:84-85`). It creates
  no second predicate. **`PastDue` and `Paused` are excluded by it** — settled by owner ruling
  2026-08-03 (ADR-0035 AM-17); this resolver adds **nothing** for that case.
- `UserMembership.IsInTrial` (`TrialEndsAtUtc`) — **the one conjunct this resolver adds on top of the
  shared predicate**, per the owner's 2026-08-03 ruling: *no express waivers during the 14-day trial*.
  The trial **keeps** the discount and the cancellation window, so this narrowing lives **here and
  nowhere else**. ⚠️ **Never push `IsInTrial` down into `ActiveForUserQuery`** — that would strip the two
  benefits the owner preserved (ADR-0035 AM-18).
- `MembershipPlan.AllowsExpressUpgrade` + `ExpressUpgradesPerMonth` — the gate and the number.
  **Read from the CURRENT plan at the moment of the call**, so a mid-month plan swap changes the quota
  without touching the count (ADR-0035 AM-19).
- `ICountryConfigurationRepository` → `CountryConfiguration.TimeZoneId` — the zone the calendar month
  is evaluated in (UTC fallback, mirroring `GetDashboardStats.ResolveTimeZone:252-266`).
- `IMembershipBenefitUsageRepository` — **read only**: the live slot count for the current `PeriodKey`.
- `BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc, waiverApplies)` — the caller passes this
  resolver's answer as the `bool`.
- Callers: `QuoteOrder`, `CreateOrder.Validator`'s pricing path, `OrderPricingCalculator`, `OrderFactory`.

## Does NOT know

- **How to consume a credit.** It **never** reserves, never writes, never commits. Consumption is one
  call, in **`CreateOrder.Handler`** (AM-9 moved it out of `OrderFactory` so the factory gains zero
  collaborators), to `TryReserveBenefitSlotAsync`. **A resolver that consumes burns a credit on every
  quote and on every price-mismatch rejection** — that is the single hardest constraint this seam
  exists to satisfy (`CreateOrder.cs:159-176` re-runs pricing on every submit).
- **Which enrolment earned the credit.** `UserMembershipId` is written once, at reservation, for human
  support use only. **The resolver must never filter, count, group or join on it** — the quota belongs
  to the **calendar month**, not to a membership row (owner ruling 2026-08-03, ADR-0035 AM-19). Counting
  key: `(TenantId, UserId, BenefitKind, PeriodKey)` + `IsActive`, always.
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
   (`CancellationPolicyResolver.cs:27-30`); no active membership (**incl. `PastDue`/`Paused`, by the
   shared predicate**), **`membership.IsInTrial`**, `!AllowsExpressUpgrade`, or
   `ExpressUpgradesPerMonth <= 0` → no waiver (`:35-39`'s shape).
2b. **`UserMembershipId` appears nowhere in this class.** Grep it: zero hits. Any occurrence is the
   AM-19 violation — a quota that silently resets on a plan swap or a re-subscribe.
3. **Never throws on a bad/missing zone** — null / blank / `TimeZoneNotFoundException` /
   `InvalidTimeZoneException` → `TimeZoneInfo.Utc`. A pricing call site must not 500 over a config row.
4. **Its answer reaches `BookingPolicy` as a `bool`, not as a membership.** `BookingPolicy` must have
   zero occurrences of `Membership` (ADR-0035 §verify #7).
5. **`Remaining` is server-computed and returned**, never recomputed by a client (T-0514 AC4). It is
   `max(0, currentPlan.ExpressUpgradesPerMonth − live rows in the PeriodKey)` — **live rows in the
   period**, not live rows with ordinal `< quota`, or it disagrees with the claim path after a plan
   downgrade (AM-19).
6. **During the trial it returns `Remaining = 0`, not `null`.** `null` means *no membership*; a trialing
   customer has one. The client renders "starts on {`TrialEndsAtUtc`}", not a bare zero.

## Watch-list

- The resolver is called on the **quote** path, which is anonymous-friendly and hot. Its extra work for
  a guest is one `IsNullOrEmpty` check; for a member it is two indexed reads. If it ever needs a third
  round-trip, revisit — the pricing path is the most latency-sensitive surface in the product.
- If a second metered benefit lands, **do not generalize this class into an
  `IBenefitResolver<TKind>`** on the first repeat. Two sibling resolvers mirroring
  `CancellationPolicyResolver` is the pattern; a generic one hides the per-benefit gate semantics that
  make each one reviewable.
