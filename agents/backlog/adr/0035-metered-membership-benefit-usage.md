# ADR-0035 — A metered membership benefit is a reserved-slot ledger row: `MembershipBenefitUsage`, keyed by a stored period key, reserved atomically before the price is waived, released only when the customer did not consume the booking

- **Status:** proposed   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-02
- **Supersedes:** —
- **Superseded by:** —
- **Backs / extends:** **ADR-0023 D1** (the repeatable-effect test — this decision selects **Mode A,
  claim-before-act**, and explains why the ADR-0010 `IIdempotencyGuard` *mechanism* is nonetheless the
  wrong tool here). Mirrors the **`PromoCodeRedemption` per-user slot-reservation archetype**
  (`PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync`) and the **`UserMembership` filtered
  partial unique index** (`UserMembership.cs:14-20`). Adopts the **`CancellationPolicyResolver` benefit
  seam** unchanged (`BookingPolicy.cs:101-111`). Does not change ADR-0006/ADR-0009 (the refund money
  path) — it only re-uses their "price is frozen at purchase" principle.
- **Applies to:** backend | database | cross-cutting (client read surface)
- **Ticket:** T-0511 (this ADR) · **Consumers:** T-0512 (entity + EF config + ⚠️ `ef-migration`),
  T-0493 (the three pricing call sites + the consumption call), T-0514 (client render, ⚠️ `nswag-regen`),
  T-0513 (the copy — see §Copy, which this ADR *constrains* but does not own)

> **⚠️ ADR number reservation.** Two sibling architect panels are in flight this sprint (T-0495 dispatch,
> T-0517 payout details). `0034` was free at authoring time (verified: no `ADR-003[4-9]` reference exists
> anywhere under `agents/`). If a sibling claims it first, this file renumbers — the number is not
> load-bearing until `accepted`.

> **One decision:** how the platform **counts, consumes, resets and reverses** a metered membership
> benefit. This is one decision and not five because the parts are inseparable: the counted unit
> *is* the row, the period boundary *is* a column on that row, the concurrency guarantee *is* the unique
> index over those columns, and the reversal rule *is* whether that row stays live. Splitting them
> produces four decisions that can each be individually correct and jointly wrong. (Same
> inseparability argument ADR-0010 recorded for the entity + index + own-commit algorithm.)

> **Owner rulings this ADR builds on (2026-08-02):** *"You can upgrade"* (the express perk becomes
> real code — T-0493), *"Let's do 2 times per month and reset once per calendar month"*, and **the perk
> is Plus-only**. The quota's *value* and *boundary* are therefore settled; this ADR designs the
> **mechanism** and pins the four things the owner's answer does **not** settle: the counted unit, the
> timezone the calendar month is evaluated in, the concurrency guarantee, and the reversal rule.

---

## Context

### The gap (PM-verified at `master`, re-verified by this panel by reading)

| Claim | Evidence |
|---|---|
| `MembershipPlan.AllowsExpressUpgrade` is read by zero pricing code | **CONFIRMED.** `MembershipPlan.cs:105` is returned by `GetMyMembership.cs:60` and `GetMembershipPlans.cs`; no pricing path reads it. |
| The domain explicitly defers the counter | **CONFIRMED.** `MembershipPlan.cs:99-104` — *"When true, usage is capped — see the future 'membership benefit usage' tracker."* **This ADR is that tracker's decision.** |
| `BookingPolicy.RequiresExpressSurcharge` is a pure time function | **CONFIRMED.** `BookingPolicy.cs:68-72` — `(DateTime cleaningUtc, DateTime nowUtc)`. No membership parameter, no membership call site. |
| A Plus member pays the same +20% | **CONFIRMED.** `OrderPricingCalculator.cs:64-69` and `OrderFactory.cs:100-102` are the two live call sites (`QuoteOrder.cs:168` consumes the calculator's `ExpressSurchargeApplied`, it does not re-evaluate the policy). |
| An order's cleaning time cannot be moved | **CONFIRMED.** `Order.CleaningDateTime` (`Order.cs:40`) has a private setter and **no mutator anywhere in `src/`** (grep: no `UpdateCleaningDate` / `SetCleaningDate` / `Reschedule*` on `Order`). There is no reschedule feature. |

### The archetype this platform already has — and the one it does not

**It already has a per-user metered cap enforced by a row.** `PromoCode.MaxRedemptionsPerUser` +
`PromoCodeRedemption.SlotOrdinal` + the tenant-scoped unique index
(`PromoCodeRedemptionEntityConfiguration.cs:58-67`) + the single-statement atomic reservation
(`PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync`, `:29-110`). That is *this exact
problem*, already solved, in production, with the S7 check-then-act race already closed and a real
production bug already paid for (the `42P08` untyped-tenant-parameter defect, `:85-93`).

**It already has a membership benefit threaded into a policy.** `CancellationPolicyResolver`
(`:17-45`) answers "what does this customer's plan do here"; `BookingPolicy.CalculateCancellationFeeRate`
takes the answer as a parameter (`BookingPolicy.cs:101-119`). One resolver, one parameter, no branch
on membership inside the policy.

**What it does not have is a period.** Promo redemptions are lifetime-capped; nothing in this codebase
resets a per-user counter on a calendar boundary. That — and the reversal rule — is the genuinely new
part, and it is why this is an ADR and not a ticket.

### The constraint nobody has costed yet: the waiver must be *previewable* without being *consumed*

`CreateOrder.Validator.PriceMatchesAsync` (`CreateOrder.cs:159-176`) re-runs the pricing calculator and
**rejects the order** with `TotalPriceNotMatch` unless the server's total equals the client's
`TotalPrice`. `QuoteOrder` runs the same calculator earlier. So a waived member's price is computed
**at least three times before the order exists** — in the quote, in the validator, and in the factory.

**A design where "resolve the waiver" and "consume a credit" are the same call burns a credit on every
quote and on every rejected order.** This is the single hardest constraint on the seam and it forces
the shape below: a **pure resolver** used by every pricing path, and **exactly one** consuming call at
the persist point.

---

## Decision

### D1 — The counted unit: one unit = **one granted waiver**, decided at order creation

**One unit is consumed when, and only when, the platform charges a member 0 instead of
`ExpressSurchargeRate` on a booking that genuinely fell inside the express window.** Not "an express
booking". Not "a completed booking". **The waiver itself.**

- **If no surcharge would have applied, nothing is consumed.** A 09:00 booking for 18:00 has a 9-hour
  lead time — it is not express (`BookingPolicy.cs:68-72`), it is already free for everybody, and it
  costs a member nothing. This is the *only* definition under which the ledger matches the customer's
  mental model of "I got a free one". (It is also why the shipped copy is wrong — see §Copy.)
- **If the member has no quota left, nothing is consumed** — they pay the surcharge and no row is
  written. A row exists only where a waiver was actually granted.
- **A guest / anonymous booking consumes nothing** (no `UserId`, no membership) — the resolver
  short-circuits on a null user exactly as `CancellationPolicyResolver.cs:27-30` does.

**The lifecycle point is order creation (`New`), not `Completed`.** Against the
`New → Pending → Confirmed → OnTheWay → InProgress → Completed / Cancelled` states (`CLAUDE.md`):

- The waived price is what the customer is **charged** at creation — `CreateOrder` persists the order
  and dispatches payment in the same request (`CreateOrder.cs:283-310`). A benefit *priced* at creation
  and *counted* days later at `Completed` can disagree with itself: the order is cancelled, the
  membership lapses (`UserMembership.IsActive` is time-dependent, `UserMembership.cs:84-85`), or the
  plan's quota is edited by an admin in between. **Price and consume in one transaction or they drift.**
- Every state after creation changes the *count* only via D4's release rule, never via a recount.

**"Created inside the window" vs "would have carried a surcharge" — the divergence case, pre-answered.**
The two definitions can only differ if a booking is *moved*, and **there is no reschedule feature**
(`Order.cs:40`, verified above). This ADR pins the answer in advance so the first reschedule ticket does
not get to re-decide it: **the slot binds to the order at reservation and is never re-evaluated.** A
move out of the express window does not refund the slot; a move into it does not consume one. This is
the same freeze the refund path already relies on — `Order.TotalPrice` embeds discount and surcharge at
purchase and **no downstream actor re-applies them** (ADR-0009 D2, `patterns-backend.md` §B8). Re-deriving
the waiver on a move would mean re-pricing a paid order.

### D2 — The period: a **stored `PeriodKey`**, computed in the **order country's** time zone

**Owner's ruling — 2 per calendar month, reset on the 1st.** The stored shape:

| | |
|---|---|
| **Key format (calendar)** | `"C:2026-08"` — a `C:` discriminator + `yyyy-MM` of the **local** wall-clock instant |
| **Key format (billing-anchored, if a future benefit ever needs it)** | `"B:{UserMembershipId}:{periodStartUtc:yyyyMMdd}"` |
| **Column** | `PeriodKey`, `[Required] [MaxLength(64)]`, part of the unique index |
| **Computed by** | one function, once, at reservation. **Never recomputed for an existing row.** |

**Why a stored string key and not a `(PeriodStart, PeriodEnd)` pair or a rolling 30-day count:**

- It survives **both** answers to `Q-PLUS-02(3)` with **no migration** (T-0511 AC2). Calendar and
  billing-anchored differ only in the *rule that computes the key*; the column, the index, the
  reservation statement, the release path and the remaining-count query are byte-identical. A
  `(Start, End)` pair would need a range-overlap query and a different index; a rolling window would
  need a scan and could never answer "which month was that?".
- Because it is **stored**, a later change to the country's zone, a DST rule revision, or a
  configuration correction **cannot retroactively move a past reservation into a different month.**
  Same principle as the frozen `TotalPrice`.
- **No rollover falls out for free** (`Q-PLUS-02(2)`, this ADR's stated default): an unused slot has
  nowhere to accumulate — next month is simply a different key. Adding rollover later is a *real*
  change (a carry column, or a multi-key count), and that cost is the honest price of this shape.

**The timezone — the part the owner's answer does not settle, decided here.**

> **The period key is computed in the time zone of the ORDER'S COUNTRY —
> `CountryConfiguration.TimeZoneId` (`CountryConfiguration.cs:27`), resolved from the order's service
> address country — with `TimeZoneInfo.Utc` as the documented fallback.**
> **Not** the customer's device zone. **Not** bare UTC.

Three reasons, in the order that decides it:

1. **The customer's zone is client-supplied and unauthenticated, and this is a money boundary.**
   `IUserSessionProvider.GetTimeZoneId()` reads the `X-Time-Zone` **request header**
   (`UserSessionProvider.cs:39-50`). A member at 23:30 on the 31st can send
   `X-Time-Zone: Pacific/Kiritimati` and collect next month's two credits ~13 hours early, then send
   `Pacific/Midway` the next day and still be inside the old month — **four free upgrades from a header
   they control**. The header is legitimate for a dashboard's "this week" (`GetDashboardStats.cs:64` —
   read-only presentation); it must never gate an entitlement. Using it here would be an S1/S7-shaped
   defect shipped on purpose.
2. **Bare UTC is wrong for the first two hours of every month, forever.** Czech local time is UTC+1/+2.
   A member booking at 00:45 CEST on 1 September is at 22:45 UTC on 31 August — told they have no
   credits on a day their own calendar calls the 1st. That is a support ticket every single month.
3. **The country's zone is server-held, non-spoofable, and is the seam the platform already uses for
   per-country variation** (`CLAUDE.md`: read `CountryConfiguration`; never branch on a country code in
   a handler). For a CZ-only launch it is `Europe/Prague` and the question is invisible; the seam is
   what makes DE/PL/UA correct for free later.

Pinned details:
- **Which country:** the **order's service-address country** (`Address.CountryId`) — already the key
  `OrderFactory.cs:152-157` uses to resolve `CompanyInfo` + `CountryConfiguration` for the VAT
  breakdown. Same resolution, same call site, same failure posture. Not the user's profile country: the
  booking is where the cleaning happens, and the platform already resolves country from the address for
  its other money-shaped decision.
- **Fallback:** `TimeZoneId` is nullable and `TimeZoneInfo.FindSystemTimeZoneById` throws on unknown /
  invalid ids. Null / blank / `TimeZoneNotFoundException` / `InvalidTimeZoneException` → **`TimeZoneInfo.Utc`**,
  exactly as `GetDashboardStats.ResolveTimeZone` (`:252-266`) already does. **A pricing call site must
  never throw over a time zone.** Reuse that helper rather than writing a second one.
- **Storage stays UTC.** Every timestamp column is UTC; the local calendar is materialized in exactly
  one place — the key — and only once, at reservation. The "UTC internally, local presentation" rule is
  untouched.

**Owner confirmation requested (non-blocking).** The owner ruled the *boundary*, not the *zone*. The
default taken is the order country's zone; the fallback is UTC; for a CZ-only launch the two differ by a
~2-hour sliver at each month boundary. Recorded here for the PM to carry into `questions/open.md` as a
follow-on to `Q-PLUS-02`; **this ADR does not block on it**, because both candidate answers use the same
stored shape.

### D2.1 — The quota's value lives on `MembershipPlan`, not in `BookingPolicy`

`MembershipPlan` gains **`int ExpressUpgradesPerMonth`**, mirroring `FreeCancellationWindowHours`
(`MembershipPlan.cs:91-97`) field-for-field in role and semantics:

- `AllowsExpressUpgrade == false` → **no waiver, ever**; the number is ignored. (The flag stays: it is
  already on three clients' DTOs via `GetMyMembership.cs:60` and in admin CRUD — removing it is a
  client-breaking change for zero gain.)
- `AllowsExpressUpgrade == true && ExpressUpgradesPerMonth <= 0` → **no waiver** (fail-closed), the
  exact semantic `CancellationPolicyResolver.cs:36` already uses for `FreeCancellationWindowHours <= 0`.
- `MembershipPlan.UpdateBenefits(...)` (`:166-175`) gains the fourth parameter; the admin plan CRUD and
  its i18n label (`admin en.json:1961`) follow.
- **Existing rows default to `0`** (fail-closed). The class doc says the table starts empty
  (`MembershipPlan.cs:20-25`); DEV may hold seeded rows, and `0` on those is the safe default —
  they simply keep paying the surcharge until an admin sets the number. (T-0512 AC6.)

**"Unlimited" is deliberately not expressible.** The web copy currently promises an uncapped discount
(`cleansia.app en.json:1095`); the owner ruled 2/month, so uncapped is now a *rejected product*, and
T-0513 fixes that string. Making `0`/`-1`/`null` secretly mean "infinite" is how a seeding mistake
becomes an unbounded discount. If the owner ever wants unlimited, that is a nullable-column migration
plus a superseding ADR — not a sentinel.

Rejected: **a `const ExpressUpgradesPerMonth = 2` in `BookingPolicy`.** `BookingPolicy` holds *platform*
numbers that are the same for everyone; a **per-plan** benefit number belongs on the plan, next to the
benefit it meters, where an admin can change it without a deploy and where a second tier (Plus / Pro)
can differ. A const also cannot vary per plan, which is the first thing a second tier needs.

### D3 — Concurrency: a filtered partial UNIQUE index + one atomic reservation statement

**The guarantee, named at the DB level:**

> **A tenant-scoped, `IsActive`-filtered UNIQUE index on
> `(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)`, backing a single-statement
> `INSERT … SELECT … HAVING <live count> < @maxPerPeriod … ON CONFLICT DO NOTHING RETURNING`
> reservation that derives the ordinal in SQL.** The database is the arbiter. There is no
> `SELECT`-then-`INSERT` anywhere in the consuming path.

`MembershipBenefitUsage` (`Cleansia.Core.Domain/Memberships/`, sibling to `UserMembership`):

| Field | Type | Notes |
|---|---|---|
| `Id` | string PK (ULID 26) | `Auditable`, as `PromoCodeRedemption` |
| `TenantId` | via `ITenantEntity` | **tenant-scoped** — a benefit counter that leaks across tenants is a billing defect (T-0512 AC3). This is *not* the ADR-0010 S8 exception: the reservation runs inside a request that has a JWT and a tenant. |
| `UserId` | string, required, 26 | FK → `User`, `OnDelete.Restrict` (mirror `PromoCodeRedemptionEntityConfiguration.cs:48-51`) |
| `BenefitKind` | enum stored as int | `ExpressUpgrade = 1`. **Never reorder** (the `BillingInterval` convention, `MembershipPlan.cs:10`). |
| `PeriodKey` | string, required, 64 | D2 |
| `SlotOrdinal` | int, required | 0-based, in `[0, MaxPerPeriod-1]`, **derived inside the reservation statement**, never from a pre-read count (`PromoCodeRedemption.cs:39-41` documents why) |
| `OrderId` | string**?**, 26 | the order the waiver was granted on. **Nullable by design** — see D3.2. FK `OnDelete.Restrict`. |
| `UserMembershipId` | string, required, 26 | which enrolment earned it — makes a billing-anchored key computable and makes support answerable |
| `ReservedAtUtc` | `DateTime` (UTC) | audit + the orphan-reclaim cutoff |
| `IsActive` | via `BaseEntity` | `true` = a live consumed slot; `false` = released (D4). **Soft-delete per the B6 judgment call** (`consistency.md` §Judgment calls). |

**Index (T-0512 AC2):**
```
UNIQUE (TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal) WHERE "IsActive" = TRUE
```
A **filtered partial** unique index — the exact shape `UserMembership` already uses for
"at most one active membership per user" (`UserMembership.cs:14-20`,
`UserMembershipEntityConfiguration`). The filter is what makes D4's release restore capacity: a
released row keeps its ordinal for the audit trail without occupying the slot. A second, non-unique
index on `(TenantId, UserId, BenefitKind, PeriodKey)` serves the remaining-count read.

**Reservation sketch** (T-0512/T-0493 write it; this is the contract, not the code):
```sql
INSERT INTO "MembershipBenefitUsages" (…)
SELECT @id, @userId, @kind, @periodKey, COUNT(*) FILTER (WHERE u."IsActive"), …
FROM   "MembershipBenefitUsages" u
WHERE  u."UserId" = @userId AND u."BenefitKind" = @kind AND u."PeriodKey" = @periodKey
  AND  u."TenantId" IS NOT DISTINCT FROM @tenantId
HAVING COUNT(*) FILTER (WHERE u."IsActive") < @maxPerPeriod
ON CONFLICT DO NOTHING
RETURNING "Id", "SlotOrdinal";
```

- **`COUNT(*)`-of-live-rows, not `MAX(SlotOrdinal)+1` — a named, deliberate deviation from the promo
  archetype.** Promo redemptions are never released, so `MAX+1` is safe there. Here a released row
  would leave a gap that `MAX+1` skips, so capacity would **not** come back and D4's release would be a
  no-op. `COUNT`-of-live is equally atomic (it is inside the statement, not a pre-read — the thing
  `PromoCodeRedemption.cs:39-41` actually warns about) and the unique index still catches a
  same-ordinal race via `ON CONFLICT DO NOTHING`.
- **`@tenantId` MUST be sent as an explicit `NpgsqlDbType.Text` parameter.** Not optional style: an
  untyped null tenant makes PostgreSQL deduce two different types for the same parameter and fails the
  whole statement with `42P08` — a real production 500 that only fires in single-tenant mode and
  survived a tenanted test run (`PromoCodeRedemptionRepository.cs:85-93`). Do not re-earn that bug.
- **0 rows returned ⇒ no slot ⇒ `null` — a RESULT, never an exception.** Cap reached or a race loser.
  The caller does not waive.
- **Declared UoW exception.** Like the promo reservation, this statement issues SQL immediately and
  auto-commits **outside** the MediatR `UnitOfWork` pipeline. That is required for atomicity and is the
  same sanctioned exception `IPromoCodeRedemptionRepository:34-39` already carries. Every other write in
  this design rides the pipeline.

### D3.1 — Why **not** `IIdempotencyGuard` / `ProcessedMessage` (ADR-0010 / ADR-0023)

T-0511's brief said to reuse the existing idempotency posture rather than invent. **The posture is
reused; the class is not, and the distinction is the decision.**

**Reused — ADR-0023 D1's repeatable-effect test, and its answer:** *would a second grant need un-doing?*
Yes — a duplicate free upgrade is a discount given twice, and the cap is defeatable on purpose by firing
concurrent bookings. → **Mode A, claim-BEFORE-act, mandatory.** The reservation is taken **before** the
waived price is computed. There is no path in which a price is waived without a committed slot (D3.2).

**Not reused — the `ProcessedMessage` mechanism**, for three reasons that are each disqualifying:

1. **Wrong tenancy.** `ProcessedMessage` is **tenant-global by design** (ADR-0010 D1's reasoned S8
   exception) because a queue consumer runs with no JWT. This reservation runs inside a request that
   *has* a tenant, and T-0512 AC3 requires tenant scoping. Using the guard imports the wrong posture.
2. **Wrong cardinality.** The guard is a **binary** claim on one opaque key. A quota of N needs N
   distinguishable slots **and a count** — the client must be told "1 left" (AC7). You would have to
   encode user + benefit + period + ordinal into `MessageKey` and then answer "remaining" with a
   `LIKE` scan on a table that has no such index.
3. **Wrong transaction.** The guard commits in its **own** unit of work as a consumer-side control
   (ADR-0010 D2). The reservation must be visible to the *same request* that prices the order.

Recorded so the next panel does not re-litigate it: **the ADR-0023 rule is universal; the ADR-0010
backing is for queue consumers.** A request-scoped, tenant-scoped, counted entitlement uses the
`PromoCodeRedemption` archetype.

### D3.2 — The ordering, and the orphan it creates

```
CreateOrder → OrderFactory.CreateAsync:
  1. waiver = resolver.ResolveForUserAsync(...)          // PURE READ — no write, safe in quote+validator
  2. if (waiver.Waived)                                   // Mode A: reserve BEFORE pricing
        reserved = usageRepo.TryReserveBenefitSlotAsync(userId, kind, periodKey, max, ct)   // may be null
  3. surchargeApplies = BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc, waiverApplies: reserved != null)
  4. order = Order.Create(..., finalTotalPrice, ...)      // price is now final and frozen
  5. usageRepo.AttachOrderAsync(reserved.Id, order.Id, ct)  // stamp — the row stops being an orphan
```

- **Step 2 returning `null` means the surcharge applies.** A cap loss is a price, never an error; the
  booking always proceeds (D7).
- **The orphan, named:** step 2 commits out-of-band, so a failure between step 2 and step 5 (a
  downstream validation failure, a payment-dispatch failure, a crash) leaves a **live row with
  `OrderId IS NULL`** — a credit spent on a booking that never existed. **Bounded by D4's release
  rule:** an `OrderId IS NULL` row older than **1 hour** is released by the sweep that already exists
  for exactly this failure class (`CleanupStalePendingOrders` — *"users who opened PaymentSheet but
  closed it without confirming"*, `:13-23`, `OlderThanHours = 1`). No new job.
- **Not chosen: a compensating delete on the failure path.** ADR-0023 CH-2 already rejected
  compensation for this shape — a crash between the failure and the compensating write strands the
  artifact and reproduces the bug. The sweep is idempotent and does not depend on the failing process
  surviving.

### D4 — The reversal rule, and the exploit accepted

> **A reserved slot is released if and only if the booking it paid for was never consumed by the
> customer. Concretely: release when the order ends with `hasBeenAccepted == false` (no cleaner ever
> took it), OR when the cancellation is not the customer's doing (`CancelledBy != Customer`), OR when
> the row is an unstamped orphan (D3.2). Otherwise the slot is consumed permanently.**

Release = **`IsActive = false`** (soft-delete, B6), which frees the ordinal through the filtered index
(D3) while keeping the audit row.

| Case | Released? | Why |
|---|---|---|
| Customer cancels, **no cleaner had accepted** | **YES** | Nothing was consumed: no cleaner was pulled onto a 2-hour-notice job, no capacity was held. This is the same line `BookingPolicy` itself already draws — `CalculateCancellationFeeRate` returns `0m` immediately when `!hasBeenAccepted` (`:121-125`). Reusing that exact flag is consistency, not invention. It also subsumes the "oops window" mis-tap (`:59-62`) without a second rule. |
| Customer cancels, **a cleaner had accepted** | **NO** | **This is the exploit being accepted — see below.** |
| `CancelledBy.Cleaner` (cleaner cancel / no-show) | **YES** | Our failure. Charging the customer's perk for it is indefensible, and the customer cannot cause it, so there is nothing to farm. |
| `CancelledBy.Admin` | **YES** | Same. Admin cancellation is a platform action. |
| `CancelledBy.System` (stale unpaid order swept) | **YES** | The order never entered the fulfilment pipeline; the customer was never charged. |
| Refund (partial or full) on a **completed** order | **NO** | The clean happened. A refund is a money adjustment, not an un-booking. |
| Orphan (`OrderId IS NULL` > 1h) | **YES** | No booking ever existed (D3.2). |

`CancelledBy` already exists with exactly these four values (`CancelledBy.cs:10-13`) and is already
recorded by `Order.Cancel(...)` (`CancelOrder.cs:122-127`). `hasBeenAccepted` is already computed by
`CancelOrder.cs:103-104`. **The rule adds no new state — it reads two things the cancel path already
has in hand.**

#### The exploit accepted, named

**A Plus member who cancels a real, accepted express booking for a completely legitimate reason
(illness, a genuine change of plan) loses the credit they paid for.** They spent a perk on a cleaning
that did not happen. That is a failure *against* the customer and I am choosing it.

**What bounds it:** the cancellation-fee schedule already prices this case, and it prices it hard. An
express booking is by construction 2–4 hours before its start (`BookingPolicy.cs:18-24`), so a customer
cancelling an *accepted* express booking is inside `PartialCancellationHours = 4` and pays
`LastMinuteCancellationFeeRate = 0.50` — **half the order** (`:136-141`). A customer facing a 50% fee is
not casually cancelling; the lost credit is the smaller of the two costs and is not the deciding one.
And the customer is *told* the fee before confirming (the cancel flow returns `FeeRate`,
`CancelOrder.cs:171-176`).

> **⚠️ SEEDING CONSTRAINT this ADR pins, because it is invisible otherwise.**
> `MembershipPlan.FreeCancellationWindowHours` **must be seeded strictly greater than
> `BookingPolicy.StandardLeadTimeHours` (4)** — 24 is the natural value; 4 is the minimum that still
> works. A *smaller* threshold is *more* generous (`BookingPolicy.cs:106-110`), so a Plus plan seeded at
> **2** makes an accepted express booking free to cancel — at which point the fee schedule bounds
> nothing and the farming loop below becomes free. **A seeding value ≤ 4 silently converts this ADR's
> accepted exploit into the rejected one.** T-0512 must carry this as a seed-data note and the admin
> plan form should refuse it.

#### The exploit rejected, named

**If the credit came back on every cancellation:** a Plus member (especially one on a generous
free-cancel window) could loop *book express → surcharge waived → cleaner accepts → cancel free →
credit returns → repeat*, at zero cost, **indefinitely**. The harm is not the discount — it is
repeatedly yanking cleaners onto 2-hour-notice jobs that evaporate. That is an attack on supply, which
is the scarce side of this marketplace, and it is unbounded. **Rejected.**

The asymmetry is the whole argument: the accepted exploit is **bounded by an existing 50% fee and
costs at most two credits a month**; the rejected one is **unbounded and costs cleaner trust**.

### D5 — One table keyed by benefit, not a column per benefit

`MembershipBenefitUsage` + a `BenefitKind` discriminator. Not `ExpressUpgradesUsedThisMonth` on
`UserMembership`; not a table per benefit.

The domain comment already names the tracker generically (`MembershipPlan.cs:102`), Plus advertises five
perks, and a second metered one is plausible.

**Extension cost, stated honestly (AC5).** A second metered benefit costs: **one enum value + one
`MembershipPlan` column + one resolver.** The table, the index, the reservation statement, the release
path, the orphan sweep and the remaining-count query are **reused unchanged**. What is *not* avoided is
the per-benefit plan column — a generic `MaxPerPeriod` cannot live on the usage row (it is a *plan*
property, and putting it on the row would let it drift per row). So each benefit still carries one
additive `MembershipPlan` migration; the generality saves the *usage-side* migration and the
duplicated concurrency machinery, which is the expensive half.

**Cross-check with T-0517 (AC5's "do not answer generality differently by accident").** T-0517 designs
payout details, where the owner said "CZ first, built to extend". These two **should** answer
differently, and here is the rule that makes the difference principled rather than accidental:

> **One table + a discriminator when the rows have the same shape and differ only in meaning. A
> `CountryConfiguration`-driven per-country shape when the fields themselves differ.**

Benefit-usage rows are homogeneous (user, period, ordinal) and differ only in *which* benefit →
discriminator. Payout details are heterogeneous (IBAN vs. a domestic account/bank-code pair vs. a card
token) and differ *structurally per country* → config-driven variation. Same generality question, two
correct and different answers.

### D6 — The resolver seam: `IExpressWaiverResolver`, mirroring `CancellationPolicyResolver` exactly

```csharp
// Cleansia.Core.AppServices/Services/Interfaces/IExpressWaiverResolver.cs
public interface IExpressWaiverResolver
{
    /// PURE READ. Never writes, never consumes. Safe to call from the quote path,
    /// from CreateOrder.Validator, and from the pricing calculator.
    Task<ExpressWaiver> ResolveForUserAsync(
        string? userId,
        string? countryId,
        DateTime cleaningUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

/// Waived = this booking is inside the express window AND the member has a live slot.
/// Remaining = live slots left in PeriodKey AFTER this booking would be granted (0 when not a member).
public record ExpressWaiver(bool Waived, int Remaining, int Quota, string? PeriodKey);
```

Point-by-point mirror of `CancellationPolicyResolver` (`:14-45`) — AC6 satisfied by adoption, not by
argument:

| `CancellationPolicyResolver` | `ExpressWaiverResolver` |
|---|---|
| ctor takes `IUserMembershipRepository` only | ctor takes `IUserMembershipRepository` + `IMembershipBenefitUsageRepository` + `ICountryConfigurationRepository` |
| builds a `defaultPolicy` first (`:21-25`) | builds a "no waiver" default first |
| `string.IsNullOrEmpty(userId)` → default (`:27-30`) | identical short-circuit (guest booking) |
| `activeMembership == null \|\| FreeCancellationWindowHours <= 0` → default (`:35-39`) | `== null \|\| !AllowsExpressUpgrade \|\| ExpressUpgradesPerMonth <= 0` → default |
| returns a record the policy takes as a parameter | identical |

**`BookingPolicy` learns nothing about memberships.** `RequiresExpressSurcharge` gains one **optional**
parameter, exactly as `CalculateCancellationFeeRate` did:

```csharp
public static bool RequiresExpressSurcharge(
    DateTime cleaningUtc, DateTime nowUtc, bool waiverApplies = false)
    => !waiverApplies
       && (cleaningUtc - nowUtc).TotalHours >= ExpressLeadTimeHours
       && (cleaningUtc - nowUtc).TotalHours <  StandardLeadTimeHours;
```

A **parameter, not an `&&` at each call site**, for the same reason `freeCancellationHoursOverride` is a
parameter: the parameter makes every caller answer the question and makes the omission greppable
(§verify #3). Default `false` = "no benefit", the same default direction as
`freeCancellationHoursOverride = null`. A `bool`, not the record — `BookingPolicy` gets the *answer*,
never the *reason*.

**The "active Plus membership" predicate (T-0511's carried warning): it already exists, once, and
nothing new is created.** `UserMembershipRepository.ActiveForUserQuery` (`:20-29`) is the single SQL
expression of `UserMembership.IsActive` (`UserMembership.cs:84-85`: `Status == Active &&
UtcNow < CurrentPeriodEnd`), consumed by `CancellationPolicyResolver.cs:32`, `GetMyMembership.cs:35`,
`OrderFactory.cs:76`, `QuoteOrder.cs:141` **and by the already-shipped T-0494 recurring gate**
(`CreateRecurringBooking.cs:84-85`). `ExpressWaiverResolver` calls
`GetActiveForUserNoTrackingAsync` and adds nothing. A `PastDue`/`Paused` member gets no waiver, by
the existing predicate — no new rule.

### D7 — The read path, and what an exhausted member is told

**No new endpoint.** `GetMyMembership.Response` (`GetMyMembership.cs:13-25`) gains two nullable fields:

| Field | Meaning |
|---|---|
| `int? ExpressUpgradesPerMonth` | the plan's quota (null when no membership) |
| `int? ExpressUpgradesRemaining` | `max(0, quota − live slots in the current PeriodKey)` |

`GetMyMembership.Handler` gains one collaborator (3 total) — well inside the handler-dependency bar.

**`QuoteOrder.Response` gains one bool** — `bool ExpressSurchargeWaivedByMembership`. Without it, a
waived member's quote carries `ExpressSurchargeApplied: false, ExpressSurchargeAmount: 0`, which is
**indistinguishable from "this is not an express slot at all"**, and T-0514 AC1 (*"show the waiver, not
the surcharge"*) cannot be built. The existing two fields keep meaning "what you are charged"; the new
bool means "and here is why it is zero". `ExpressUpgradesRemaining` rides the quote too, so the wizard
can say "1 left" at the moment of choice rather than making the client re-fetch the membership.

> ⚠️ Both are DTO changes → **`manual_steps: nswag-regen`, owner-only** (already carried on T-0514).

**An exhausted member is TOLD, never silently charged.** The decision, so no client has to guess:

- **The booking is never blocked.** A quota loss is a price, never an error. There is no new
  `BusinessErrorMessage` and no new failure path. (Same instinct as the fiscal seam: a secondary
  concern never blocks the customer's primary flow — `docs/architecture/fiscal-compliance.md`.)
- **The client has everything it needs to say why**: `ExpressUpgradesRemaining == 0` plus
  `ExpressSurchargeApplied == true` plus `ExpressSurchargeWaivedByMembership == false` is exactly the
  state T-0514 AC2 renders as *"you've used both free express bookings this month"*.
- **The remaining count is server-computed and client-rendered, never client-counted** (T-0514 AC4). A
  client that counts the member's own orders disagrees with the server the first time D4 releases a
  slot.

**Where the count is visible:** the membership screen and the booking quote (both above). **Not on any
partner/cleaner surface** — a cleaner must never see a customer's entitlements. **No admin endpoint is
decided here** (out of scope); support can answer "did I really use both?" from the row today, because
it carries `OrderId`, `PeriodKey`, `ReservedAtUtc` and `IsActive`. If an admin view is wanted, it is a
separate ticket, and this shape already supports it.

### D8 — Scope boundary

- **In scope:** the entity + index + reservation/release/attach repository contract, the resolver seam,
  the `BookingPolicy` parameter, the two DTO deltas, the release hooks in the cancel paths + the
  existing stale-order sweep, the `MembershipPlan.ExpressUpgradesPerMonth` column, and the catalog +
  living-doc updates.
- **Byte-untouched:** `IIdempotencyGuard` and both its backings, `ProcessedMessage`, `PromoCodeRedemption`
  and its repository (mirrored, not modified), `UserMembership`, `RefundPolicy` / `IRefundService`,
  `LoyaltyService`, and every fiscal path.
- **Not decided here:** the copy (T-0513 — but see §Copy for the constraints this ADR imposes on it),
  the client rendering (T-0514), the other four Plus perks, and whether the favourite-cleaner perk is
  Plus-gated (`Q-PLUS-03`).

---

## Alternatives considered

| # | Alternative | Why not |
|---|---|---|
| **A1** | **Derive the count from `Order` rows at query time — no new table at all.** `COUNT(Orders WHERE UserId=@u AND CreatedOn in [month) AND (CleaningDateTime − CreatedOn) ∈ [2h,4h))`, and "the first 2 are the waived ones". **Genuinely attractive: zero schema, zero migration, no reversal bookkeeping.** | **Four disqualifying defects, in order of severity.** (1) **No arbiter.** It is a `SELECT`-then-act with nothing at the DB to break a tie — the exact S7 check-then-act race the platform already had to *fix* for promo codes (`PromoCodeRedemptionRepository.cs:37-46`). Two concurrent bookings both read "1 used" and both get waived. There is no index you can add to a *computed expression over two columns* that closes it. (2) **It re-computes history.** The predicate embeds `BookingPolicy.ExpressLeadTimeHours` / `StandardLeadTimeHours`. Tune 2→3 or 4→6 and **every past month's count silently changes**, retroactively. A quota that moves when a constant is tuned is not a quota — and it is the same defect ADR-0009 D2 already forbade for money (`Order.TotalPrice` is frozen; nothing re-applies discount/surcharge). (3) **It cannot distinguish waived from charged.** The predicate matches express orders, not *waived* ones — so a member who paid the surcharge (quota exhausted, or membership lapsed at booking time) still counts against next month if the plan number later changes. (4) **It loses history to GDPR.** `Order.AnonymizeCustomerData()` nulls `UserId` **and** `MembershipPlanIdAtPurchase` (`Order.cs:613-621`); an anonymized order silently drops out of the count. Plus: no reversal rule is expressible at all (there is no row to release), and `CreatedOn` is not set until the pipeline commits, so the *current* order cannot be counted at pricing time. |
| **A2** | **A counter column on `UserMembership`** (`ExpressUpgradesUsedThisPeriod` + a reset when the period rolls). | Loses the audit trail entirely — nobody can answer *"which booking used my free one?"*, which is precisely the support question a metered perk generates. Reversal becomes a decrement, and a decrement has no idempotency: a retried cancel path double-refunds the credit. Concurrency needs an atomic conditional `UPDATE`, which works but caps at one benefit per column, so D5's generality dies. And the reset has to be *driven* (a sweep, or an opportunistic check on read) rather than falling out of a key — a reset job that misses a month is silently wrong. `UserMembership` already carries two reminder stamps that are reset on period rollover (`:60-76`, `:129-136`); adding a *money* counter to that same rollover path couples the quota to Stripe webhook timing. |
| **A3** | **`IIdempotencyGuard` / `ProcessedMessage` with a composed key.** | D3.1 — wrong tenancy (tenant-global by design), wrong cardinality (binary claim, no count, no "remaining" query), wrong transaction scope (consumer-side own-commit). The ADR-0023 *rule* is reused; the ADR-0010 *backing* is for queue consumers. |
| **A4** | **Consume at `Completed` instead of at creation.** | The price is charged at creation (`CreateOrder.cs:283-310`); counting days later lets price and count disagree whenever the order is cancelled, the membership lapses (`UserMembership.IsActive` is time-dependent), or the plan is edited in between. It also makes the quota unknowable at the moment the customer needs to know it — you cannot show "1 left" if the count only firms up after the clean. |
| **A5** | **Release the credit on every cancellation (fully symmetric refund).** | D4 — creates the unbounded supply attack: waive → cleaner accepts → free cancel → credit returns → repeat, yanking cleaners onto 2-hour-notice jobs at zero cost. The bounded alternative (never release on a customer cancel of an *accepted* booking) costs the customer at most two credits a month, already alongside a 50% fee. |
| **A6** | **Never release under any circumstance (fully asymmetric).** | Simplest, and wrong. It charges the member's perk for **our** failures — a cleaner no-show, an admin cancel, a Stripe checkout the customer abandoned before anything was dispatched. `CancelledBy` (`CancelledBy.cs:10-13`) already distinguishes these at zero cost; ignoring it would be a choice to be unfair with information in hand. |
| **A7** | **Evaluate the calendar month in the CUSTOMER'S time zone** (`X-Time-Zone`). | D2 — the header is client-supplied and unauthenticated. A member can straddle the boundary and draw four credits from two months by changing one header. Fine for a dashboard (`GetDashboardStats.cs:64`); disqualifying for an entitlement. |
| **A8** | **Evaluate the calendar month in bare UTC.** | D2 — "the 1st" is then wrong for the first 1–2 hours of every month in every European zone we serve. Cheap to get right; expensive to explain in a support thread every month. |
| **A9** | **Store `(PeriodStart, PeriodEnd)` instead of a key.** | Needs a range-overlap predicate and a different index; makes "which month was that?" a computation rather than a read; and does **not** buy the both-ways property the key gets for one discriminator character (T-0511 AC2). |
| **A10** | **Put `2` in `BookingPolicy` as a const.** | D2.1 — `BookingPolicy` holds platform-wide numbers; a per-plan benefit number belongs on the plan next to `FreeCancellationWindowHours`, changeable by an admin without a deploy, and differentiable when a second tier lands. |
| **A11** | **Give `BookingPolicy.RequiresExpressSurcharge` the membership itself** (or an `IUserMembershipRepository`). | Breaks the policy class's whole point: it would become async, DB-bound, untestable as a pure function, and would know about memberships. The archetype is explicit — the resolver knows the plan, the policy takes the *answer* (`BookingPolicy.cs:101-111`). |
| **A12** | **A dedicated `POST /membership/express-credits/consume` endpoint / a separate quota service.** | Two round-trips, a second source of truth for the price, and a window in which a credit is consumed but the order is never created that is *wider* than D3.2's (a whole user-interaction, not a few milliseconds). The reservation belongs in the same request as the price. |

---

## Consequences

**Cheaper / safer**
- The quota is enforced **by the database**, in one statement, on the archetype the platform already
  runs in production for promo codes. There is no code path where a `SELECT` decides a money question.
- The period boundary is a **stored label**, so a zone change, a DST revision, or a future switch to a
  billing anchor cannot rewrite history — and switching a *future* benefit to a billing anchor is a
  resolver change with **no migration**.
- The reversal rule reads two things the cancel path **already computes** (`hasBeenAccepted`,
  `CancelledBy`), so it adds no state and cannot drift from the fee decision made three lines above it.
- A second metered benefit is an enum value + a plan column + a resolver. The concurrency machinery,
  the release path and the orphan sweep are written once.
- The resolver seam is a byte-for-byte adoption of `CancellationPolicyResolver`, so a reviewer checks
  it by diffing shapes rather than reasoning about it.

**More expensive (accepted)**
- **Two out-of-band SQL statements** in the create path (reserve, attach) that bypass the UoW pipeline.
  Declared, mirroring the promo exception, and required for atomicity — but it is now **two** such
  exceptions in `CreateOrder`, not one. A reviewer must know both are deliberate.
- **The orphan window** (D3.2): a credit can be live for up to an hour against an order that never
  existed. Reclaimed by the existing hourly sweep; visible in the interim as a wrong "remaining" count.
- **A member can lose a credit to a legitimate cancellation** (D4, the accepted exploit) — bounded by
  the 50% last-minute fee they are already paying, and by the seeding constraint on
  `FreeCancellationWindowHours`.
- **A seeding constraint that a person must respect:** `FreeCancellationWindowHours > 4`. This is the
  one place the design depends on data being sane; T-0512 carries it as a note and the admin form
  should refuse it.
- **One `ef-migration` (owner-only)** — one new table + two indexes + one additive `MembershipPlan`
  column (default `0`, fail-closed). **One `nswag-regen` (owner-only)** — two fields on
  `GetMyMembership.Response`, two on `QuoteOrder.Response`.
- **A `MembershipBenefitUsage` row per granted waiver, forever.** At 2/member/month this is trivial;
  no prune is specified, and the rows are audit-valuable. Revisit only if a future benefit is metered
  in the hundreds.

---

## How a reviewer verifies compliance

**Mechanical**
1. **The index is a FILTERED PARTIAL UNIQUE** on `(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)`
   `WHERE "IsActive" = TRUE`, and `MembershipBenefitUsage` implements **`ITenantEntity`** (not the
   ADR-0010 tenant-global exception). Compare against `UserMembershipEntityConfiguration`'s filtered
   index and `PromoCodeRedemptionEntityConfiguration.cs:58-67`.
2. **The reservation is ONE statement.** Grep the repository: there is no `CountAsync`/`AnyAsync`
   followed by an `Add` in the consuming path. The ordinal is computed **in SQL**, the guard is a
   `HAVING … < @maxPerPeriod` over **live** rows, and there is `ON CONFLICT DO NOTHING` +
   `RETURNING`. `null` is returned for "no slot" — **no exception escapes to the order's commit**.
3. **`@tenantId` is an explicit `NpgsqlDbType.Text` parameter.** If it is inferred, the reviewer has
   found the `42P08` bug again (`PromoCodeRedemptionRepository.cs:85-93`). This one is a hard reject.
4. **Every `RequiresExpressSurcharge` call site passes the resolver's answer.** Grep
   `RequiresExpressSurcharge(` across `src/` — the pricing call sites (`OrderPricingCalculator.cs:65`,
   `OrderFactory.cs:102`) each pass `waiverApplies:`; any call site relying on the `false` default is a
   finding unless it is provably non-pricing.
5. **The resolver never writes.** Grep `IExpressWaiverResolver`'s implementation for `Add`/`Commit`/
   `ExecuteSql`/`Try*Reserve*` — there must be none. It is called from `QuoteOrder`, from the
   `CreateOrder` validator's pricing path, and from the factory; a consuming resolver burns a credit
   on every quote.
6. **Exactly one consuming call site.** Grep `TryReserveBenefitSlotAsync` — one caller, in
   `OrderFactory`, **before** `Order.Create`.
7. **`BookingPolicy` has no membership type in scope.** Grep the file for `Membership` — zero hits;
   the new parameter is a `bool`.
8. **The period key is never recomputed for an existing row.** Grep for the key-builder: it is called
   at reservation and (read-only) in the remaining-count query for the *current* period. No `UPDATE`
   sets `PeriodKey`.
9. **The timezone comes from `CountryConfiguration.TimeZoneId`, not from `GetTimeZoneId()`.** Grep the
   resolver for `X-Time-Zone` / `GetTimeZoneId` — zero hits. The unknown-zone fallback is
   `TimeZoneInfo.Utc` and does not throw (mirror `GetDashboardStats.cs:252-266`).
10. **The release hooks match D4's table exactly** — `CancelOrder` releases only when
    `!hasBeenAccepted`; the cleaner/admin/system cancel paths release unconditionally; no release on
    refund of a completed order.

**Test contract (red first — `TC-BENEFIT-*`)**
11. **TC-BENEFIT-QUOTA-0.** Third express booking in one period is **charged** the surcharge; first two
    are not. Assert on the persisted `Order.TotalPrice`, not on the resolver.
12. **TC-BENEFIT-RACE-0.** Two concurrent reservations with **one** slot left, on separate scopes →
    exactly **one** non-null result; exactly **one** live row; the loser's order carries the surcharge.
    Mirrors `TC-IDEMP-RACE-0` / the promo race test.
13. **TC-BENEFIT-PERIOD-0.** A reservation at 23:59:59 local on the last day of the month and one at
    00:00:01 local on the 1st land in **different** `PeriodKey`s — and the same two instants expressed
    in UTC do **not** decide it. This is the test that pins D2's timezone ruling; it must fail against a
    bare-UTC implementation.
14. **TC-BENEFIT-PREVIEW-0.** N quote calls + a validator run for the same member consume **zero**
    slots. (The regression this design exists to prevent.)
15. **TC-BENEFIT-REVERSAL-0..3.** (0) customer cancel, **not accepted** → released, remaining restored.
    (1) customer cancel, **accepted** → **not** released. (2) `CancelledBy.Cleaner` and `.Admin` →
    released. (3) stale-pending system sweep → released.
16. **TC-BENEFIT-ORPHAN-0.** A reservation whose order never persists is `OrderId IS NULL` and is
    released by the sweep after the cutoff; before the cutoff, "remaining" is legitimately one lower.
17. **TC-BENEFIT-SLOTREUSE-0.** After a release, the next reservation succeeds and takes the freed
    ordinal — the test that proves the filtered index + `COUNT`-of-live derivation actually restores
    capacity (it fails under a `MAX(SlotOrdinal)+1` derivation, which is why D3 deviates).
18. **TC-BENEFIT-GATE-0.** `AllowsExpressUpgrade == false`, `ExpressUpgradesPerMonth == 0`, a `PastDue`
    membership, and a guest booking each waive nothing and write no row.

---

## The copy — what this ADR constrains, and the sequencing ruling

**Two facts, both load-bearing, and they have different urgencies.**

**Fact 1 — "same-day" is wrong and it is shipping.** All five locales on both mobile clients promise
*"One free same-day booking per month, no surcharge"* (`values/strings.xml:844`,
`Localizable.xcstrings:14121`, + `values-cs/:832`, `values-sk/:829`, `values-uk/:829`, `values-ru/:829`).
**Express in this codebase is a 2–4 hour lead window** (`BookingPolicy.cs:18-30`). A 09:00 booking for
18:00 is same-day and carries **no surcharge for anybody**. Meanwhile the web client advertises a third,
different product — *"Pay less for last-minute bookings inside the express window"*, an **uncapped
discount** (`cleansia.app en.json:1095`). Three clients, three promises, none matching the mechanic.

**Fact 2 — the owner's answer makes the number wrong too.** *"2 times per month"*, not one. Every
mobile string says **one**.

**The ruling on sequencing — the two errors do not have the same urgency, and they must not be
batched as if they did:**

> **The corrective half ships immediately and does NOT wait for the implementation. The affirmative
> half ships only with T-0493.**

- **Corrective (ship now, ahead of T-0512/T-0493):** delete *"same-day"* and delete the web client's
  uncapped-discount claim. *"Same-day"* is a promise that is **false against the customer** in the case
  they actually care about: someone who reads it and books at 09:00 for 12:00 — which **is** express —
  is charged +20% having been told it was free. That is a live misrepresentation on a **paid
  subscription**, it costs a real customer real money today, and **removing it requires no backend at
  all** (T-0513 is explicitly dependency-free). Waiting for the mechanism to ship is choosing to keep a
  false statement live for the length of a build.
- **Affirmative (ship with T-0493, not before):** *"Two free express bookings each calendar month."*
  Until T-0493 lands, **nothing waives anything** — a client claiming a waiver that does not exist
  replaces one misrepresentation with another. T-0513 AC4 already asks for this sequencing statement;
  this is the architecture's answer to it.
- **"One" → "two" is the low-urgency half of the affirmative change.** It is false **in the customer's
  favour**: a member told "one" who receives two is not wronged and will not complain. It is known now
  and should ride the same T-0513 pass, but **if it slips, it slips harmlessly** — which is exactly why
  it must not be allowed to hold up the corrective half.

**Constraints this ADR places on T-0513's canonical sentence** (the analyst owns the wording; these are
the checkable facts it must not contradict):
1. It names the **lead-time window**, not "same-day" — the number is `BookingPolicy.ExpressLeadTimeHours`
   (2) to `StandardLeadTimeHours` (4).
2. It says **"each calendar month"**, not "every 30 days" and not "per billing period" — D2.
3. It states the cap as **2**, sourced from `MembershipPlan.ExpressUpgradesPerMonth`, and does not
   promise an uncapped discount anywhere (D2.1: unlimited is not expressible).
4. It does not promise that a cancelled booking returns the credit — D4 says it usually does not.

A sentence that satisfies all four, offered as an anchor and not as the decision:
> *"Two free express bookings each calendar month — we waive the 20% surcharge on cleanings booked
> 2 to 4 hours ahead."*

**Also:** the Android in-code comment at `values/strings.xml:846-847` (*"No express pill: nothing in
pricing reads AllowsExpressUpgrade…"*) is a **correct description of a live defect** and must be
updated in the same wave that makes it false — T-0513 AC5 already holds this.

---

## Roles affected

Role cards written with this ADR (marked proposed until it is accepted):
- **`agents/knowledge/roles/membership-benefit-usage.md`** — the ledger row + its repository.
- **`agents/knowledge/roles/express-waiver-resolver.md`** — the pure resolver.

Existing cards touched on acceptance: none. `refund-policy.md`'s "does NOT know" list already says
*"Discount / express-surcharge math — those are already embedded in `Order.TotalPrice`"* — which stays
true and is, in fact, the principle D1 extends.

**Catalog edit to land ON ACCEPTANCE (not before — a `proposed` ADR must not become enforceable):**
`agents/knowledge/patterns-backend.md`, a new section *"Per-user metered entitlements — the
reserved-slot ledger"*, prepared here verbatim so acceptance is a paste, not a re-decision:

> ### Per-user metered entitlements — the reserved-slot ledger (ADR-0035)
> A cap on how many times **one user** may receive **one benefit** in **one period** is a row, never a
> counter and never a derived count.
> - **Shape:** an `Auditable` + `ITenantEntity` ledger row carrying `UserId`, a **`Kind`
>   discriminator** (int-stored, never reordered), a **stored `PeriodKey`** string, and a 0-based
>   `SlotOrdinal`. Reference: `MembershipBenefitUsage`; ancestor: `PromoCodeRedemption`.
> - **Concurrency:** a **filtered partial UNIQUE index** `(TenantId, UserId, Kind, PeriodKey,
>   SlotOrdinal) WHERE IsActive` + a **single-statement** `INSERT … SELECT <ordinal computed in SQL> …
>   HAVING <live count> < @max … ON CONFLICT DO NOTHING RETURNING`. Never `SELECT`-then-`INSERT`. A
>   full slot returns **null**, a result, never an exception at the caller's commit. Send a nullable
>   `TenantId` as an **explicit `NpgsqlDbType.Text`** parameter (`42P08`).
> - **Ordering:** ADR-0023's repeatable-effect test applies — an entitlement grant is money-shaped, so
>   **Mode A**: reserve **before** the benefit changes the price. Never price first and reserve after.
> - **Period:** a **stored key**, computed once at reservation from the **country's**
>   `CountryConfiguration.TimeZoneId` (UTC fallback, `GetDashboardStats.ResolveTimeZone`) — **never**
>   from the client `X-Time-Zone` header, which is unauthenticated. Never recompute the key for an
>   existing row.
> - **Preview vs consume are different calls.** The "does this user get it" question is answered by a
>   **pure resolver** (the `CancellationPolicyResolver` shape) that every pricing path may call freely;
>   consuming happens **once**, at persist. A resolver that consumes burns the entitlement on every
>   quote.
> - **Reversal:** release (soft-delete → the filtered index frees the ordinal) only when the user did
>   not consume the thing the entitlement bought. Whatever the rule is, **the ADR names the exploit it
>   accepts**; "decide later" means "decide in production".

Living companion updated in the same change: **`agents/architecture/decisions/membership-benefits.md`**.

---

## Challenge

> **⚠️ PROCESS STATE — read this before treating the section below as a deliberation trail.**
> `agents/process/deliberation.md` requires the author, the challengers, and the lead to be **different
> instances**. **Only the author has run.** The entries below are **AUTHOR-RAISED** — the attacks I
> could see against my own draft, pre-answered so a challenger starts past them rather than at them.
> **They are not independent challenges and they do not satisfy T-0511 AC9.** This ADR stays
> `proposed` until real challengers and a lead have run (see §Verdict).

| # | Challenge (AUTHOR-RAISED) | Where it bites |
|---|---|---|
| CH-1 | *"A1 (derive from `Order` rows) is dismissed with four bullets, but three of them are hypotheticals. Ship the zero-schema option and add a table only when a race is observed."* | AC10 — the alternative the ticket said must be argued against, not ignored. |
| CH-2 | *"Two out-of-band SQL statements in the create path is one more than the codebase has ever accepted. The promo exception was grudging; you just doubled it."* | D3.2 / the UoW rule. |
| CH-3 | *"You accept an orphan window and then reclaim it with a sweep — that is compensation, which ADR-0023 CH-2 explicitly rejected."* | D3.2 vs. ADR-0023. |
| CH-4 | *"D4 releases on `!hasBeenAccepted` but consumes on an accepted booking. A customer cannot see whether a cleaner accepted at the moment they cancel, so the rule is unobservable to the person it affects."* | D4 / deliberation.md's "an AC a challenger showed is unobservable does not survive". |
| CH-5 | *"The order's country decides the month, so a Czech member booking a clean in Poland gets a different month boundary than the one their app shows."* | D2. |
| CH-6 | *"`COUNT`-of-live instead of `MAX+1` departs from the promo archetype you spent a page adopting. Pick one."* | D3 / AC6. |
| CH-7 | *"`ExpressUpgradesPerMonth` on `MembershipPlan` names the period in the column. The moment a benefit is billing-anchored, the column lies."* | D2.1 vs. D2's both-ways claim. |
| CH-8 | *"The seeding constraint (`FreeCancellationWindowHours > 4`) is a comment. A design whose safety depends on a comment is not safe."* | D4. |
| CH-9 | *"Four new DTO fields across two responses is a lot of contract for one perk, and each one is an owner-only NSwag regen."* | D7. |
| CH-10 | *"Nobody checked whether `MembershipPlan` has live DEV rows, so the `0` default may silently switch off something that is on."* | D2.1 / T-0512 AC6. |

## Defense

- **CH-1 — REBUT, on evidence, not preference.** Defect (1) is not hypothetical: it is the *same* race
  the platform **already shipped, hit, and fixed** for promo codes — `PromoCodeRedemptionRepository.cs:37-46`
  and `PromoCodeService.cs:120-128` exist because the `SELECT`-then-`INSERT` version was wrong in
  production. Re-deriving it for a second entitlement would be knowingly re-introducing a closed bug.
  Defect (4) is also not hypothetical: `Order.AnonymizeCustomerData()` nulls `UserId` *today*
  (`Order.cs:613-621`), so a derived count is already lossy. And defect (2) is the decisive one on its
  own: a quota whose historical value changes when someone tunes `ExpressLeadTimeHours` is not a
  quota — it is a query. The codebase already refused this shape for money (`Order.TotalPrice` frozen,
  ADR-0009 D2). **Conceded to A1:** it *is* the cheapest option and it would work for a single-threaded
  single-instance deployment — which this is not (Azure DEV is deployed and scales).
- **CH-2 — CONCEDE + BOUND.** The challenge stands as a cost and is now recorded in Consequences
  ("two such exceptions in `CreateOrder`, not one"). It does not stand as a blocker: the alternative to
  an out-of-band atomic reservation is a check-then-act race on a money decision, and the codebase has
  already ruled that trade once (`IPromoCodeRedemptionRepository:34-39`). **Revised:** D3 now states the
  exception explicitly and §verify #2/#3/#6 make both statements greppable, so a reviewer sees two
  deliberate exceptions rather than one deliberate and one accidental.
- **CH-3 — REBUT with the distinction.** ADR-0023 CH-2 rejected compensation *by the same process that
  just failed* — a crash between the failed send and the compensating delete strands the claim, so the
  compensation is only as reliable as the process that must survive to run it. D3.2's reclaim is the
  opposite shape: an **independent, idempotent, already-scheduled sweep** whose input is a durable
  predicate (`OrderId IS NULL AND ReservedAtUtc < cutoff`), not an in-flight continuation. It does not
  depend on the failing request surviving anything. That is a *sweep*, not a compensation.
- **CH-4 — CONCEDE the observability point, REBUT the conclusion.** The customer does see it: the
  cancel flow already surfaces `FeeRate` before/with the cancellation (`CancelOrder.cs:171-176`), and
  `hasBeenAccepted` is the *same* input that drives that fee (`BookingPolicy.cs:121-125`) — so "a
  cleaner has taken your booking" is already the thing the fee is telling them. **Revised:** the rule is
  now stated as "released iff the booking was never consumed" with `hasBeenAccepted` as its mechanism,
  and D7 requires the exhausted/consumed state to be surfaced rather than silent. **Flagged for the
  challenger round:** whether the *cancel confirmation sheet* must also say "this will use up your free
  express booking" is a client decision (T-0514) that this ADR should probably mandate and currently
  only implies.
- **CH-5 — REBUT.** The order's country is where the *service* happens, and it is already the country
  the platform uses for the other money-shaped per-order decision (VAT — `OrderFactory.cs:152-157`).
  Using the *user's* country instead would split the two and create a case where an order's VAT country
  and its benefit country disagree. The cross-border case is also currently empty: the platform is
  CZ-only at launch. **Recorded as the escalation in D2** — if the owner prefers the membership's
  country, the change is one argument at one call site and **no schema change**, which is exactly what
  the stored-key shape was chosen to buy.
- **CH-6 — CONCEDE it is a deviation; DEFEND it as required.** D3 now labels it as a **named deviation**
  with the reason: promo has no release path, this does, and under `MAX+1` a released slot is
  permanently lost — which would make D4 a no-op and silently re-introduce the "member loses a credit
  to our own cancellation" unfairness that D4 exists to prevent. TC-BENEFIT-SLOTREUSE-0 (§verify #17)
  is the test that fails under `MAX+1`, so the deviation is pinned, not asserted.
- **CH-7 — CONCEDE + REVISE.** The challenge is right about the name. **Revised:** the column stays
  `ExpressUpgradesPerMonth` **for this benefit only**, because for this benefit the owner has ruled the
  period is a calendar month and a vaguer name (`ExpressUpgradesPerPeriod`) would be *less* honest about
  what is actually guaranteed. A future billing-anchored benefit gets its own column with its own
  period in its own name. The **both-ways claim in D2 is about the usage table's schema**, which is
  where a migration would hurt — not about a plan column, where adding one is already the per-benefit
  cost D5 states plainly.
- **CH-8 — CONCEDE, PARTIALLY UNRESOLVED.** A comment is not an enforcement. Mitigations now in the
  ADR: T-0512 carries it as a seed-data note, and the admin plan form should refuse it. **What is
  missing and I am not hiding it:** a validator on `MembershipPlan.UpdateBenefits` /
  the admin update command that rejects `FreeCancellationWindowHours <= BookingPolicy.StandardLeadTimeHours`
  when `AllowsExpressUpgrade` is true. **I recommend the lead make that a blocking amendment** — it is
  a small validator and it converts the design's one data-dependency into a checked invariant.
- **CH-9 — REBUT on count, CONCEDE on cost.** Two of the four are *required* by tickets that already
  exist (T-0514 AC2 needs "why is there no waiver", AC4 forbids client-side counting). The
  `ExpressSurchargeWaivedByMembership` bool is the one a challenger could argue away — and it cannot be,
  because without it `ExpressSurchargeApplied: false` is ambiguous between "waived" and "not an express
  slot", which is precisely the screen T-0514 AC1 has to render. The regen cost is real and is already
  owned by T-0514's `manual_steps`.
- **CH-10 — CONCEDE, and it is a real gap in this panel's evidence.** Nobody queried DEV. `0` is the
  fail-closed default (members keep paying the surcharge until an admin sets the number), so the failure
  mode is "the perk stays off", not "a perk turns on by accident". **T-0512 AC6 must confirm the DEV row
  count rather than assume it** — recorded in §What this panel did not examine.

## Verdict

**NOT REACHED. Status stays `proposed`.**

`agents/process/deliberation.md` step 5 requires a **lead** to adjudicate challenges raised by
**independent challengers**. Only the author has run. Per the ticket's own AC9 and the ADR record
discipline, this artifact cannot be `accepted` on an author's self-review — that is exactly the
"individual judgment carried straight into code" failure the panel exists to prevent.

**What must happen before `accepted`:**

1. **2–3 challenger instances**, each attacking and each recording what they checked (silence is not
   assent). Suggested split so they do not collide:
   - **Challenger A — the alternative.** Attack A1 (derive from `Order`) and A2 (counter column). The
     bar: show the race or the history-recompute is *not* real, or that its cost is lower than a table.
   - **Challenger B — the reversal and the exploit.** Attack D4's line. Model a Plus member with
     `FreeCancellationWindowHours` seeded at 24, at 4, and at 2, and at each value decide whether the
     fee schedule actually bounds the farming loop. **If it does not bound it at 24, D4 falls.**
   - **Challenger C — the seam and the concurrency.** Attack D3/D3.2 (the two out-of-band statements,
     the orphan window, the `COUNT` vs `MAX` deviation) and D6 (whether the resolver really mirrors
     `CancellationPolicyResolver` or only claims to). Verify by reading
     `PromoCodeRedemptionRepository.cs` and `CancellationPolicyResolver.cs` side by side.
2. **The author defends** each challenge in writing (rebut with evidence / concede + revise /
   escalate).
3. **A lead adjudicates.** Two points are pre-flagged as candidates for a **blocking amendment** rather
   than a defence:
   - **CH-8** — add a validator so `FreeCancellationWindowHours > StandardLeadTimeHours` is enforced,
     not commented. (Author recommends: **make this blocking**.)
   - **CH-4** — decide whether the cancel confirmation must warn "this will use up your free express
     booking" (a T-0514 AC, but it is this ADR's rule that creates the need).
4. **On acceptance, in the same change:** the `patterns-backend.md` section above is pasted in, the two
   role cards drop their "proposed" banner, and
   `agents/architecture/decisions/membership-benefits.md` flips from "tracking a proposed ADR" to
   "current shape".

**Not blocking acceptance:** the owner's confirmation of the timezone anchor (D2). Both candidate
answers use the same stored shape, so the design is stable either way.

---

## What this panel did NOT examine (T-0511 AC11 · Gate 0.5 leg 3)

**Every claim in this ADR is a READ of source at `master`. Nothing was run** — no build, no test, no
query, no migration. Specifically:

- **Not run:** `dotnet build`, `dotnet test`, any EF command, any SQL. The reservation SQL sketch in D3
  is **unverified against PostgreSQL** — in particular the `COUNT(*) FILTER (…)` + `HAVING` +
  `ON CONFLICT DO NOTHING` + `RETURNING` combination is *adapted* from a statement that is known to work
  (`PromoCodeRedemptionRepository.cs:60-74`, which uses `MAX(…)+1`), **not the statement that works**.
  **T-0512 must prove it against a real PostgreSQL** (an integration test, not a unit test) before the
  entity is considered done. If it does not compose, D3's *guarantee* still holds — the fallback is an
  advisory-lock or a `SELECT … FOR UPDATE` on a per-(user, period) row — but the statement changes.
- **Not queried:** whether DEV has live `MembershipPlan` rows (CH-10), and what
  `FreeCancellationWindowHours` any of them carry (CH-8's constraint). **Both are assumptions.**
- **Not examined:** the admin membership-plan UI (web) — D2.1 adds a field to a form nobody on this
  panel opened. The three clients' membership screens beyond the string files. The Stripe webhook
  reconciliation path (`UpdateFromStripeWebhook`, `ApplyPlanSwap`) — a **mid-month plan swap**
  (`UserMembership.ApplyPlanSwap`, `:180-197`) changes `MembershipPlanId` and therefore the quota,
  while the period key stays the same. **This ADR does not decide what happens to already-consumed
  slots on a mid-month upgrade/downgrade.** That is a real gap and a good target for Challenger A.
- **Not decided (deliberately, out of scope):** rollover (`Q-PLUS-02(2)` — default "no", and the shape
  makes it structural), an admin view of usage, the other four Plus perks, and `Q-PLUS-03`.
- **Read but not deeply verified:** the iOS/Android string files were read for the express keys only;
  the claim "all five locales say *one*" is based on the five `membership_perk_express_desc` values
  found by grep, not on a locale-by-locale rendering check.
