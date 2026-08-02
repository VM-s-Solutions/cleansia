# Role — `PreferredCleanerHoldResolver` / `IPreferredCleanerHoldResolver` (+ `OrderVisibility`) (CRC card)

> **⚠️ PROPOSED — not yet the standard.** Introduced by **ADR-0036**
> (`agents/backlog/adr/0036-preferred-cleaner-first-refusal-hold.md`), `proposed`, not yet adjudicated.
> **Third sibling of `CancellationPolicyResolver` and `IExpressWaiverResolver`** — same shape, same
> namespace family, same "returns a record the caller acts on" contract. Read
> `CancellationPolicyResolver.cs:14-45`, `agents/knowledge/roles/express-waiver-resolver.md` and
> `TakeOrder.cs` in full first.

## Responsibility (one sentence)

Answer, **as a pure read with no side effects**, the question *"may this order be held exclusively for
this customer's preferred cleaner, and until when?"* — returning
`PreferredCleanerHold(Granted, HoldUntilUtc, Reason)`.

Its companion, `OrderVisibility.NotHeldFromEmployee(employeeId, nowUtc)`, is the **one** expression that
answers *"is this order open to this cleaner right now?"* — a `static Expression<Func<Order,bool>>` in
the Domain, next to `Order`.

## Collaborators

- `IUserMembershipRepository.GetActiveForUserNoTrackingAsync` — the **one** live-membership predicate
  (`UserMembershipRepository.ActiveForUserQuery:20-29`). It creates no second predicate; the same call
  already serves `CancellationPolicyResolver:32`, `OrderFactory:76`, `QuoteOrder:141`,
  `CreateRecurringBooking:84-85`.
- `IEmployeeRepository` — `ContractStatus` and `WorkCountryId` for the preferred cleaner.
- `IUserNotificationPreferencesRepository` — whether `NotificationCategory.NewJobsAvailable` is muted
  (default-allow when the row is absent, matching `NewJobsDigestService.cs:151-158`).
- `BookingPolicy.ComputePreferredHold(cleaningUtc, nowUtc)` — the pure window function.
- Callers: `OrderFactory` (one call, at creation) and, through it, `CreateOrder` and
  `MaterializeRecurringBookings`.
- Consumers of `OrderVisibility`: `OrderSpecification`, `OrderAccessService.CanBrowseOrderAsync`,
  `NewJobsDigestService`, `TakeOrder.Validator`.

## Does NOT know

- **How to assign an order.** Nothing in this role assigns anyone anything. `TakeOrder` remains the only
  path by which an order acquires a cleaner (ADR-0036 AC3). If a scenario needs this role to place a
  cleaner on an order, the responsibility is wrong.
- **How to write.** It never sets `PreferredHoldUntilUtc`; it returns a value the factory stores. It
  never commits, never issues SQL, never enqueues a notification.
- **How to expire a hold.** There is nothing to expire — expiry is `now >= PreferredHoldUntilUtc` in a
  `WHERE` clause. **If anything ever needs to "release" a hold, the shape is wrong.** (The one sanctioned
  future write is a cleaner-side *decline*, which sets the deadline to `now` — still not this role's.)
- **`TakeOrder`'s dynamic gates.** The weekly order limit (`TakeOrder.cs:125-143`) and the time conflict
  (`:145-161`) are **deliberately not consulted** — they change between creation and the moment the
  cleaner opens the app, so a creation-time answer would be wrong in both directions. A hold never
  overrides a `TakeOrder` gate.
- **Whether a preference is *eligible*.** `UserHasCompletedOrderWithEmployeeAsync` lives in
  `CreateOrder.Validator` (`:150-154`) and stays there. This role assumes the preference is already
  legitimate.
- **What the customer is told.** The customer sees no hold state at all (ADR-0036 D6). Any copy question
  belongs to T-0491.
- **The country's time zone.** Everything here is a UTC instant arithmetic; no local calendar is
  involved. (Contrast `ExpressWaiverResolver`, which needs `CountryConfiguration.TimeZoneId`.)
- **Which tenant.** The global query filter scopes its reads.

## Invariants a reviewer checks

1. **Zero writes.** Grep the implementation for `Add` / `Commit` / `ExecuteSql` / `Notify` — none.
2. **`OrderVisibility.NotHeldFromEmployee` keys on the DEADLINE, never on `PreferredEmployeeId` alone.**
   A predicate of the form `o.PreferredEmployeeId == x` without a `PreferredHoldUntilUtc` term
   retroactively switches behaviour on for every historical row. **Hard reject.**
3. **`null` deadline ⇒ always open.** Every legacy row and every order without a granted hold must be
   visible to everyone. TC-PREF-LEGACY-0.
4. **Four call sites for the shared expression** (`OrderSpecification`, `CanBrowseOrderAsync`,
   `NewJobsDigestService`, `TakeOrder.Validator`) plus the definition. Fewer is a leak; a hand-rolled
   copy anywhere is a hard reject.
5. **The hold floor is `BookingPolicy.StandardLeadTimeHours`, not a literal `4`** — the same constant
   that defines the express band, so the two can never drift.
6. **No hold without a signal.** A muted cleaner ⇒ `Granted == false`,
   `Reason == CleanerMutedNewJobs`.
7. **The refusal agrees with the read.** `TakeOrder` returns the existing
   `BusinessErrorMessage.OrderNotFound` for a held order — no new partner-facing error key mentioning a
   hold, a reservation or a preference.
8. **`PreferredEmployeeId` reaches no partner-facing DTO.**

## Watch-list

- **`OrderFactory` is accumulating resolvers** — discounts, VAT, the ADR-0035 express waiver, now this.
  A **third** resolver on the factory should trigger a look at the factory itself rather than a fourth.
  Flagged in ADR-0036 CH-9 as a watch-list item, not a blocker.
- **The two constants are uncalibrated.** `PreferredHoldFraction = 0.10` and
  `PreferredHoldCeilingHours = 12` are reasoned defaults; nothing measured real fill times. They are
  single constants precisely so tuning them touches no live order and no schema — keep it that way.
- **If a per-country window is ever wanted**, it belongs in this resolver reading
  `CountryConfiguration` (the ADR-0017 seam), **never** as a country-code branch and **never** as a
  per-plan number (a longer hold is worse for fill rate, so it is a bad upsell lever).
- **`CanBrowseOrderAsync` evaluates the shared rule in memory**, not as a composed expression — shape
  shared, evaluation not. The one place this role's guarantee rests on review rather than on the
  compiler.
