# ADR-0035 — Challenger C: the seam and the concurrency (D3, D3.2, D6)

**Lane:** D3 / D3.2 (filtered partial unique index, the atomic reservation statement, the two
out-of-band statements, the orphan window, `COUNT` vs `MAX`) and D6 (does `IExpressWaiverResolver`
mirror `CancellationPolicyResolver` or only claim to).

**Method, as the author instructed:** `PromoCodeRedemptionRepository.cs` and
`CancellationPolicyResolver.cs` read side by side with the ADR, plus the entity configurations, the
Initial migration, the UoW pipeline, and every production call site of the pricing calculator and
`OrderFactory`. **Nothing was run** — no build, no test, no SQL, no DB query. Every claim below is a
read of source at `master`.

**Headline:** the ADR says *"The database is the arbiter. There is no `SELECT`-then-`INSERT` anywhere
in the consuming path."* In the deployment mode this platform actually runs in, the arbiter is a
no-op (CH-C1); the ordinal derivation the ADR deviates to cannot restore capacity (CH-C2); the
"stamp" statement fires against a row that does not exist yet (CH-C3); and the sweep that bounds the
orphan window does not read the table it is supposed to sweep (CH-C4). Four of the five load-bearing
mechanisms in D3/D3.2 do not hold as written. D6 is not a mirror (CH-C6/C7/C8).

---

### CH-C1 — The filtered partial UNIQUE index is a no-op in single-tenant mode, which is the mode this platform runs in. D3's only concurrency arbiter does not exist where it runs.

**The hole.** D3's guarantee is `UNIQUE (TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal) WHERE
"IsActive" = TRUE`, with `ON CONFLICT DO NOTHING` turning a concurrent same-ordinal insert into "0
rows ⇒ no slot". `ITenantEntity.TenantId` is `string?`
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Common/ITenantEntity.cs:5`),
and PostgreSQL treats NULLs as **distinct** in a UNIQUE index by default. Two rows with
`TenantId IS NULL` and otherwise identical key columns **do not conflict**, so `ON CONFLICT DO
NOTHING` never fires and both inserts land.

This is not my inference — the codebase states it, in a comment written for exactly this class of
index:

> `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/EntityConfigurations/UserMembershipEntityConfiguration.cs:100-109`
> *"NOTE: Postgres treats NULLs as DISTINCT in a UNIQUE index by default, so two NULL-TenantId active
> rows for the same user are NOT rejected by this index (single-tenant mode); **there the app-level
> `GetActiveForUserAsync` assert + the `StripeSubscriptionId` unique index are the guards**, and the
> index hardens multi-tenant mode. This is the SAME tradeoff every other tenant-scoped unique index in
> this repo makes (LoyaltyTransaction `(TenantId, IdempotencyKey)`, PromoCode/ReferralCode `(TenantId,
> Code)`); we stay consistent rather than introduce a one-off NULLS NOT DISTINCT."*

And single-tenant mode **is** `TenantId == null` — the global query filter is built around that
assumption explicitly
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/CleansiaDbContext.cs:239-246`:
*"Single-tenant mode: callers without a tenant claim should see entities that were also created
without one"*), and `CLAUDE.md` states *"Backward compatible: `null` TenantId = single-tenant mode."*

**Why it matters.** The `UserMembership` trade-off is defensible because that index is a *backstop*
behind an app-level assert. ADR-0035 explicitly removes the app-level guard — D3: *"There is no
`SELECT`-then-`INSERT` anywhere in the consuming path"* — and makes the index the **sole** arbiter.
Under `READ COMMITTED`, two concurrent reservations take their snapshot before either commits, so both
see the same live count, both pass `HAVING`, both derive the same ordinal, and with a NULL `TenantId`
both rows land. **Quota 2 becomes quota 3+ under concurrency, in the platform's default deployment.**
That is the precise defeat this ADR exists to prevent, and §verify #1 ("compare against
`UserMembershipEntityConfiguration`'s filtered index") will pass a review of an index that does not
work.

Note the promo archetype has the same property
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/EntityConfigurations/PromoCodeRedemptionEntityConfiguration.cs:66-67`
— unfiltered `(TenantId, PromoCodeId, UserId, SlotOrdinal)` unique), but promo retains a cheap
app-level pre-check (`PromoCodeService.cs:120-128`, *"Cheap in-memory FAST PATH for the per-user
cap"*) and a global-cap conditional `UPDATE` in front of it. ADR-0035 keeps neither.

**What I want changed (blocking).** D3 must decide, in the ADR, one of:
1. `NULLS NOT DISTINCT` on this index (PostgreSQL 16 per `CLAUDE.md`, so it is available) — and say
   plainly that this ADR *does* introduce the one-off `UserMembershipEntityConfiguration.cs:108-109`
   declined, because here the index is the only guard, not a backstop; or
2. index on `COALESCE("TenantId", '')` instead of the bare column; or
3. a per-`(UserId, BenefitKind, PeriodKey)` advisory lock / `SELECT … FOR UPDATE` around the
   reservation (the fallback the ADR already reserves in *§What this panel did not examine*).

Whichever is chosen, §verify #1 must be rewritten to check **NULL-tenant behavior**, and
TC-BENEFIT-RACE-0 (§verify #12) must be specified to run with `TenantId = NULL` — as written it would
be run in a tenanted fixture and would pass against a broken index. That is a test that proves the
opposite of what it claims.

---

### CH-C2 — The `COUNT`-of-live ordinal derivation does not restore capacity. It permanently blocks the freed slot whenever the released row is not the highest ordinal — and TC-BENEFIT-SLOTREUSE-0, as written, cannot detect that.

**The hole.** D3 derives the ordinal as `COUNT(*) FILTER (WHERE u."IsActive")` and justifies the
deviation from `MAX(SlotOrdinal)+1` (`PromoCodeRedemptionRepository.cs:65,71`) as *"a released row
would leave a gap that `MAX+1` skips, so capacity would not come back."* Correct diagnosis, wrong
cure. `COUNT`-of-live is the **cardinality** of the live set, not the smallest free ordinal. Walk it
with quota 2:

| step | live rows | `COUNT` live | derived ordinal | outcome |
|---|---|---|---|---|
| reserve #1 | {} | 0 | 0 | row `0` live |
| reserve #2 | {0} | 1 | 1 | row `1` live |
| D4 releases row `0` (e.g. `CancelledBy.Cleaner`) | {1} | — | — | ordinal `0` is free |
| reserve #3 | {1} | **1** | **1** | **collides with live row `1` → `ON CONFLICT DO NOTHING` → 0 rows → `null` → no waiver** |

The `HAVING COUNT(*) FILTER (…) < @maxPerPeriod` guard passes (1 < 2) — the member genuinely has
capacity — and the statement then aims at an occupied ordinal and loses to its own index. The freed
ordinal `0` is never targeted again for the rest of the period. Meanwhile D7 reports
`ExpressUpgradesRemaining = max(0, 2 − 1) = 1`.

**Why it matters.** Three ways, compounding:
1. **D4's release becomes a no-op in exactly the cases D4 exists for.** The ADR's own table (D4) gives
   `CancelledBy.Cleaner` / `.Admin` / `.System` back to the member because *"Our failure. Charging the
   customer's perk for it is indefensible."* Under this derivation, a cleaner no-show on the member's
   *first* express booking of the month leaves them with 1 nominal credit they can never spend. That
   is the indefensible outcome, shipped, with a release path that appears to work.
2. **D7's number lies.** The ADR's own framing — *"Price and consume in one transaction or they
   drift"*, *"a client that counts the member's own orders disagrees with the server"* — is violated by
   the server disagreeing with itself: "1 left", then charged.
3. **The ADR's pinning test does not pin it.** §verify #17 TC-BENEFIT-SLOTREUSE-0 says *"After a
   release, the next reservation succeeds and takes the freed ordinal."* The natural implementation —
   reserve one, release it, reserve again — has live set `{}`, `COUNT = 0`, freed ordinal `0`: **it
   passes.** The defect needs ≥2 reservations and release of the *lower* one. A test written from this
   AC will be green over a broken mechanism. This is worse than no test.

**What I want changed (blocking).** Either:
- derive the **smallest free ordinal**, not the count — e.g.
  `SELECT MIN(g) FROM generate_series(0, @max - 1) g WHERE NOT EXISTS (SELECT 1 FROM "MembershipBenefitUsages" u WHERE u."UserId" = @userId AND u."BenefitKind" = @kind AND u."PeriodKey" = @periodKey AND u."TenantId" IS NOT DISTINCT FROM @tenantId AND u."IsActive" AND u."SlotOrdinal" = g)`
  which makes the `HAVING` guard redundant (no free ordinal ⇒ no row ⇒ `null`) and is the honest
  generalization of the archetype; or
- drop `SlotOrdinal` reuse entirely: keep `MAX+1` and make release *not* free the ordinal, then the cap
  must be enforced by something other than the ordinal space (and D4's "release restores capacity" is
  simply false and must be rewritten); or
- name it as a retry loop, which I would reject on the hot path of a paid booking.

And **TC-BENEFIT-SLOTREUSE-0 must be respecified**: reserve 2 of 2, release ordinal **0**, then assert
a third reservation succeeds. As currently worded it is unfalsifiable against the defect it is named
for.

---

### CH-C3 — D3.2 step 5 (`AttachOrderAsync`, out-of-band) fires while the `Orders` row does not exist. With the `OnDelete.Restrict` FK the ADR itself specifies, that statement raises `23503`.

**The hole.** D3.2 orders the create path as `… 4. Order.Create(...) 5. usageRepo.AttachOrderAsync(reserved.Id, order.Id, ct)`,
and Consequences calls both statements *"out-of-band … bypass the UoW pipeline."* D3's table specifies
`OrderId … FK OnDelete.Restrict`. But in this codebase the `Order` row is **not in the database** at
that point:

- `OrderFactory.CreateAsync` ends at `orderRepository.Add(order)` — change-tracking only, no write
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs:167`).
- `CreateOrder.Handler` then dispatches payment and returns; nothing commits
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs:283-321`).
  `OrderPaymentDispatcher.DispatchAsync` only mints a Stripe session or enqueues
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs:30-74`).
- The insert happens **after the handler returns**, in
  `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Behaviors/UnitOfWorkPipelineBehavior.cs:27-30`
  → `CleansiaDbContext.CommitAsync` → `SaveChangesAsync`
  (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/CleansiaDbContext.cs:67-99`).
  `CommitAsync` opens **no transaction**; the only `BeginTransactionAsync` caller in production code is
  `Cleansia.Functions.Core/Handlers/GenerateReceiptHandler.cs:108`, not this path.

So an out-of-band `UPDATE "MembershipBenefitUsages" SET "OrderId" = @orderId` auto-commits on its own
against `FK_MembershipBenefitUsages_Orders_OrderId` while `Orders.Id = @orderId` does not exist →
`23503 foreign_key_violation`, thrown out of `OrderFactory`, 500, order never persisted, Stripe session
already minted.

**The archetype does not prove otherwise — it has the same untested ordering.** The promo reservation
takes `orderId` as an argument and inserts it into a column with `FK_PromoCodeRedemptions_Orders_OrderId`
… `onDelete: ReferentialAction.Restrict`
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/Migrations/20260723182623_Initial.cs:2005-2010`)
— and its only caller runs **before the commit**, at `CreateOrder.cs:315`, via
`OrderPromoApplier.ApplyAsync` whose own comment says *"Apply runs post-persist so the redemption row
gets the order id"* (`OrderPromoApplier.cs:50-53`). "Post-persist" is true of `orderRepository.Add`,
not of the database. I could not find anything that commits between `OrderFactory.CreateAsync` and
`CreateOrder.cs:315`. And nothing exercises it against a real PostgreSQL: the only tests touching
`TryReserveRedemptionSlotAsync` are `src/Cleansia.Tests/Features/PromoCodes/PromoCodeServiceRedeemTests.cs`
and `.../Admin/GetPromoCodeRedemptionsHandlerTests.cs` — unit tests with mocked repositories. There is
no `IntegrationTests`/`HostTests` coverage of this statement at all.

**Why it matters.** The ADR's central defense of D3 is *"that is this exact problem, already solved, in
production, with the S7 check-then-act race already closed."* The `42P08` bug it cites
(`PromoCodeRedemptionRepository.cs:85-93`) is a **parse-time** failure — it proves the statement
reached PostgreSQL's parser, not that it ever passed constraint checking. So the archetype's out-of-band
FK ordering is **unverified in either direction**, and ADR-0035 makes it load-bearing for a second
feature.

**What I want changed (blocking).**
1. D3.2 step 5 must not be out-of-band. `AttachOrderAsync` has no atomicity requirement — nothing races
   on it — so it should be an ordinary change-tracked write riding the UoW commit, which also makes the
   stamp atomic with the order insert and shrinks the orphan class to exactly "the order never
   committed". Consequences' *"two out-of-band SQL statements"* then reverts to one, which also answers
   the author's own CH-2 properly instead of conceding it.
   - If it rides the UoW, the reserved row must be *loaded into the context* first (it was inserted by
     raw SQL and is not tracked — `PromoCodeRedemptionRepository.cs:99-109` returns a detached object
     built by `CreateReserved`, deliberately). The ADR must say which.
2. Alternatively, make `OrderId` part of the reservation `INSERT` (as promo does) and move the whole
   reservation after the order commits — but that is **Mode B**, contradicts D3.1's "Mode A,
   claim-before-act, mandatory", and reopens the price/claim gap. The ADR must pick one and say so.
3. Either way: **the FK ordering must be proven by an integration test against real PostgreSQL before
   T-0512 is done**, and the ADR should note that the promo archetype currently has no such proof.
   (I did not run anything; I am reporting an unverified hazard in a path the ADR treats as verified.)

---

### CH-C4 — The sweep that bounds the orphan window reads `Orders`, not `MembershipBenefitUsages`. It cannot see an orphan. "No new job" is false, and D3.2's only bound is a hope.

**The hole.** D3.2: *"an `OrderId IS NULL` row older than **1 hour** is released by the sweep that
already exists for exactly this failure class (`CleanupStalePendingOrders` … `OlderThanHours = 1`).
**No new job.**"* I read that job:

```
/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/CleanupStalePendingOrders.cs:50-55
var stale = await orderRepository.GetQueryableIgnoringTenant()
    .Where(o => o.PaymentStatus == PaymentStatus.Pending
        && o.PaymentType == PaymentType.Card
        && o.CreatedOn < cutoff)
```

Three independent reasons it cannot do the job:
1. **It queries `Orders`.** An orphan usage row is by definition one whose order **never committed** —
   there is no `Order` row for this predicate to match. The sweep iterates orders and calls
   `order.UpdatePaymentStatus` / `order.AddOrderStatus` (`:76-77`); it never touches another table.
2. **It is scoped to `PaymentType == Card`.** Even a hypothetical order-driven variant would miss every
   cash booking.
3. **Its tenant handling is order-shaped**: it groups by `o.TenantId` and sets a per-group override
   (`:58-67`) so child writes inherit the tenant. A usage-release arm would need
   `GetQueryableIgnoringTenant()` on a **new** `IMembershipBenefitUsageRepository` method (the global
   tenant filter is applied to every `ITenantEntity`,
   `CleansiaDbContext.cs:201-268`) plus its own grouping.

So D3.2 needs: a new repository method, new handler logic inside a command whose name and validator
(`OlderThanHours`, `InclusiveBetween(1,168)`) are about orders, or a genuinely new job. **"No new job"
is not true**, and D8's scope ("the existing stale-order sweep") under-scopes T-0512.

**Why it matters.** This is the *only* bound the ADR places on the orphan window it deliberately
creates. Consequences prices it as *"a credit can be live for up to an hour against an order that never
existed. Reclaimed by the existing hourly sweep."* If the sweep does not read the table, the orphan is
not reclaimed in an hour — it is reclaimed **never**, and a member who abandons two payment sheets in a
month has silently lost the month's entire quota with no row a support agent can even find by order id
(`OrderId IS NULL`). The author's CH-3 defense (*"an independent, idempotent, already-scheduled sweep
whose input is a durable predicate"*) is a correct description of a sweep that does not currently exist.

**What I want changed (blocking).** Either
(a) delete the orphan by construction — CH-C3's fix (reserve and attach in the same durable step, or
reserve only after the order row is committed) removes the `OrderId IS NULL` state entirely and this
whole section with it; or
(b) if the orphan stays, D3.2 must name the **actual** mechanism: a new
`IMembershipBenefitUsageRepository.ReleaseOrphansOlderThanAsync(cutoff, ct)` (tenant-ignoring), a
supporting index on `("OrderId", "ReservedAtUtc") WHERE "OrderId" IS NULL AND "IsActive"`, the
command/function it hangs off, and its cadence. And Consequences must stop saying "no new job".
Also state what happens to an orphan row's audit trail: `OrderId` stays NULL forever, so *"support can
answer 'did I really use both?' from the row today, because it carries `OrderId`"* (D7) is false for
precisely the rows support will be asked about.

---

### CH-C5 — A lost reservation makes the persisted price higher than the one `PriceMatchesAsync` just approved and the customer consented to; and quota exhaustion between quote and submit throws `TotalPriceNotMatch`. D7's "the booking is never blocked … no new failure path" is wrong on both counts.

**The hole.** D7 states: *"The booking is never blocked. A quota loss is a price, never an error. There
is no new `BusinessErrorMessage` and no new failure path."* Set against the real create path:

```
/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs:159-176
var result = await _pricingCalculator.CalculateAsync(…, command.CleaningDate, cancellationToken);
return result.TotalPrice == command.TotalPrice;      // → BusinessErrorMessage.TotalPriceNotMatch
```

Exact decimal equality between the server's recomputed total and the client's submitted total, wired as
a hard validation failure at `:121-126`.

**Failure 1 — the booking IS blocked.** Once the calculator is waiver-aware (§verify #4), a member who
quotes at 23:59 with 1 credit left and submits at 00:01 after a concurrent booking (a second device, a
recurring materialization, an admin action) took it gets: client `TotalPrice` = waived, server
recompute = charged, `result.TotalPrice != command.TotalPrice` → **400 `TotalPriceNotMatch`**. Not a
new error code — a pre-existing one, which is *worse*: the customer is told "the total price does not
match" when the truth is "you ran out of free express bookings". Every client has that string mapped to
a generic re-quote message. The ADR's D7 claims this state is rendered as *"you've used both free
express bookings this month"* — that rendering never runs, because the request never reaches the
handler.

**Failure 2 — a silent upcharge on the race path.** Worse. D3.2 step 3 prices from `reserved != null`,
and step 4 freezes `finalTotalPrice`. So when the validator (which *cannot* reserve — it must stay a
pure read, §verify #5) approves the **waived** price and the factory's reservation then returns `null`
(race loser, or CH-C2's blocked-ordinal case), the order is created with `finalTotalPrice = waived +
20%`. `Order.TotalPrice` is what `OrderPaymentDispatcher` charges
(`CreateOrder.cs:305-306` → `stripeClient.CreateCheckoutSessionAsync(order, …)`). **The customer is
charged 20% more than the price they submitted and the server approved, with no error, no
confirmation, and no field in the response to notice it by** — `CreateOrder.Response` is
`(Id, ConfirmationCode, StripeSessionId)` (`CreateOrder.cs:226-229`). This is a consent defect on a
money path, and it is created by the ADR's own Mode-A ordering, which the ADR presents as the safe
choice.

**Why it matters.** The platform has one invariant here — *"the gross … is what the client must
resubmit — `CreateOrder.PriceMatchesAsync` compares it against the same calculator call"*
(`QuoteOrder.cs:27-31` doc). ADR-0035 introduces the first pricing input in the system that can change
**between the validator and the factory within a single request**, and does not mention it. Every other
input (services, packages, extras, currency, cleaning date) is deterministic for a fixed command.

**What I want changed (blocking).** D7 must be rewritten to name both states and decide them:
1. **Quota exhausted at submit** (validator disagrees with the client): the ADR must choose an explicit
   error (`BusinessErrorMessage.ExpressWaiverNoLongerAvailable`, re-quote) or a rule that the client's
   submitted price is honored. "There is no new failure path" is not one of the options.
2. **Reservation lost after validation** (validator said waived, factory could not reserve): the order
   must **not** silently price higher than `command.TotalPrice`. Either fail the command (re-quote), or
   honor the validated price and eat the surcharge, or move the reservation ahead of validation. Pick
   one in the ADR; do not leave it to T-0493.
3. §verify must gain a mechanical check: *"no path persists an `Order.TotalPrice` greater than the
   `command.TotalPrice` the validator approved"*, and a test `TC-BENEFIT-RACE-1` asserting the race
   loser's **charged amount equals what they consented to**. TC-BENEFIT-RACE-0 as written (*"the
   loser's order carries the surcharge"*) currently **specifies the defect as the expected behavior**.

---

### CH-C6 — D6 does not mirror `CancellationPolicyResolver`. The mirror table compares the resolver's *body*; the difference is entirely in *who calls it*, and that difference forces a signature change to `IOrderPricingCalculator` the ADR never scopes.

**The hole.** `CancellationPolicyResolver` (read in full,
`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Services/CancellationPolicyResolver.cs:14-46`)
is a 1-dependency, 1-DB-read service with **exactly one caller**, and that caller is a handler:
`CancelOrder.cs:51` (grep for `ICancellationPolicyResolver` across `src/` returns the interface, the
implementation, the DI registration `Cleansia.Config/Services/ServiceExtensions.cs:242`, and
`CancelOrder.cs:51` — nothing else). It is called **once per cancel**, in a handler that already has
the order and the user.

`IExpressWaiverResolver` is specified as *"Safe to call from the quote path, from
`CreateOrder.Validator`, and from the pricing calculator"* with 3 dependencies. Concretely:

1. **`IOrderPricingCalculator.CalculateAsync` has no `userId` and no country**
   (`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs:14-22`:
   `selectedServiceIds, selectedPackageIds, selectedExtraSlugs, rooms, bathrooms, currencyId,
   cleaningDateUtc, ct`). §verify #4 asserts *"`OrderPricingCalculator.cs:65` … passes
   `waiverApplies:`"* — that is impossible without **changing the interface** to take the user and the
   country (or the waiver itself). That is an `IOrderPricingCalculator` contract change, absent from
   D8's in-scope list, and it makes the *pricing calculator* membership-aware — which is the coupling
   A11 rejects for `BookingPolicy`, relocated one class over rather than avoided.
2. **There are four production call sites, not three.** `CalculateAsync` is called from
   `CreateOrder.cs:165` (validator), `CreateOrder.cs:266` (handler), `QuoteOrder.cs:101`, and
   `MaterializeRecurringBookings.cs:105`. The last is a **background job with no HTTP request, no
   `IUserSessionProvider` user and no tenant**, and it also calls `orderFactory.CreateAsync` in a loop
   (`MaterializeRecurringBookings.cs:141`). If the reservation lives inside `OrderFactory.CreateAsync`
   (D3.2), the recurring materializer becomes a second consuming call site — §verify #6 (*"Grep
   `TryReserveBenefitSlotAsync` — one caller, in `OrderFactory`"*) would pass while a batch job silently
   reserves slots with a null tenant. It happens to be inert today only because that job passes
   `cleaningDateUtc: null` (`:106-112`) so nothing is ever express — an accident of the current
   template shape, not a guarantee. The ADR must state it.
3. **Cost.** `CancellationPolicyResolver` = 1 read per cancel. `IExpressWaiverResolver` = up to 3
   memberships + 3 usage counts + 3 country configs per `CreateOrder` (validator, handler calc,
   factory), plus 2 per quote, on the wizard's hot path. The ADR nowhere costs this and D6 presents the
   resolver as free because the ancestor was.

**What I want changed.** D6 must (a) state the `IOrderPricingCalculator` signature change explicitly
and put it in D8's in-scope list; (b) enumerate **four** call sites and rule on
`MaterializeRecurringBookings` (I recommend: the factory takes the already-resolved waiver as an input
on `CreateOrderInput` rather than resolving it itself, which keeps the batch job unchanged and makes
"exactly one consuming call site" true by construction rather than by grep); (c) either resolve **once
per request** and thread the answer, or state the read cost and accept it. The mirror table should be
retitled — this resembles `CancellationPolicyResolver`; it does not mirror it, and "mirrors X exactly"
is what makes a reviewer stop checking.

---

### CH-C7 — D2's country-anchored `PeriodKey` cannot be computed at 3 of the 4 pricing sites nor at the read site. The key would be produced by different rules in different places — the exact drift the stored key was chosen to prevent.

**The hole.** D2 pins the key to the **order's service-address country** (`Address.CountryId` →
`CountryConfiguration.TimeZoneId`, verified at
`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs:27`).
Only one of the places that needs it has an address:

| site | has an address/country? | evidence |
|---|---|---|
| `OrderFactory.CreateAsync` (reservation) | **yes** | `input.Address.CountryId`, `OrderFactory.cs:152` |
| `QuoteOrder` (preview + D7's "1 left" on the quote) | **no** | `QuoteOrder.Command` is `(SelectedServiceIds, SelectedPackageIds, Rooms, Bathrooms, CurrencyId, SelectedExtraSlugs?, CleaningDate?)` — `QuoteOrder.cs:13-25`. No address, no country. |
| `CreateOrder.Validator.PriceMatchesAsync` | **no** | it calls the calculator with `command.*` only (`CreateOrder.cs:165-173`); the address is resolved later, in the handler, by `orderAddressResolver.ResolveAsync` (`CreateOrder.cs:248`). A `SavedAddressId` command carries no country at all. |
| `GetMyMembership` (D7's remaining count) | **no** | `Query` is parameterless; the handler has only `userId` (`GetMyMembership.cs:11, 33-34`). |

So the preview paths fall to D2's `TimeZoneInfo.Utc` fallback while the reservation uses
`Europe/Prague`. In the ~2-hour sliver at every month boundary the two produce **different keys** — the
preview counts month N's slots and the reservation writes month N−1. Result: quote says "waived, 1
left", reservation lands in the exhausted month, and CH-C5's silent upcharge or `TotalPriceNotMatch`
fires. This is not a rare race; it is deterministic for every booking made between 00:00 and 02:00
local on the 1st of the month — which D2 itself identifies as the case bare UTC gets wrong ("a support
ticket every single month") and then re-creates in the preview layer.

Same for D7's membership screen: it will show a *different month's* remaining count than the booking
will consume, on the same two-hour sliver, forever.

**Why it matters.** D2's whole justification for a stored string key is *"computed by one function,
once, at reservation. Never recomputed for an existing row."* True of the row; the **count** is
recomputed on every read, at three sites that cannot apply the rule. §verify #8 ("the key is called at
reservation and (read-only) in the remaining-count query for the *current* period") does not catch it
because both calls exist — they just disagree about which period is current.

**What I want changed.** D2 must name the key-resolution rule for a context that has **no order**:
either (a) the platform's default country (a single `CountryConfiguration` lookup with no address —
correct for a CZ-only launch and honest about the multi-country gap), or (b) the user's profile
country, or (c) accept UTC everywhere and delete the country anchoring (A8, which D2 rejected). Any of
the three is defensible; **silently different rules per call site is not**. And §verify #9 must be
extended: *"the key builder is called with the same country in the quote, the validator, the factory and
`GetMyMembership`"*, with `TC-BENEFIT-PERIOD-0` extended to assert quote-vs-reservation agreement across
the boundary, not just two reservations.

Minor, same section: D2 says *"Reuse that helper rather than writing a second one"* about
`GetDashboardStats.ResolveTimeZone`. It is `private static` inside the nested handler class
(`/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Dashboard/GetDashboardStats.cs:252-266`)
— reuse requires extracting it to a shared helper, which is a real (small) refactor T-0512 must be told
to do, not an instruction to "reuse".

---

### CH-C8 — `nowUtc` is not threaded anywhere in this codebase. The resolver's "inside the express window" and `BookingPolicy`'s are two separate evaluations at two different instants, so a slot can be burned on an order that carries no surcharge — a state D1 forbids and D4 does not release.

**The hole.** `ExpressWaiver.Waived` is defined (D6) as *"this booking is inside the express window AND
the member has a live slot"*, so the resolver evaluates the window. D3.2 step 3 then evaluates it again
inside `BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc, waiverApplies)`. Today every
evaluation reads the clock independently:

- `OrderPricingCalculator.cs:65` — `BookingPolicy.RequiresExpressSurcharge(cleaningDateUtc.Value, DateTime.UtcNow)`
- `OrderFactory.cs:102` — `BookingPolicy.RequiresExpressSurcharge(input.CleaningDate, DateTime.UtcNow)`
- `CreateOrder.cs:92,94` — `GreaterThan(DateTime.UtcNow)` / `IsBelowMinimumLeadTime(cleaningDate, DateTime.UtcNow)`

There is no captured `nowUtc` on `CreateOrderInput` and none in the command. The window is
`[2h, 4h)` (`BookingPolicy.cs:68-72`), and lead time **shrinks** as the request proceeds, so a booking
sitting a few milliseconds above the 2h floor can be in-window for the resolver and below-window for
the policy a few statements later. Then: `reserved != null`, `RequiresExpressSurcharge` returns `false`
anyway, the order carries **no surcharge**, and a live usage row is attached to it. D1 says explicitly
*"If no surcharge would have applied, nothing is consumed"* — violated. D4's release table has no row
for it (the order is real and the customer keeps it), and the orphan sweep never sees it (`OrderId` is
stamped). The member is charged a credit for a booking that was free for everybody.

**Why it matters.** Small probability, permanent effect, and it is the one failure mode that has no
release path at all — every other loss in this design is either bounded (D4) or reclaimed (D3.2). It
also means the ADR contains **two independent encodings of the express window** (the resolver's and
`BookingPolicy`'s), which is exactly the duplication `BookingPolicy` exists to prevent
(`BookingPolicy.cs:3-5`: *"Central place for all booking-time / cancellation business rules"*).

**What I want changed.** D6 must state that (a) the resolver derives "in window" by calling
`BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc)` — never its own comparison — and (b) a
single `nowUtc` is captured once per request and threaded through the resolver, the calculator and the
factory (which means `CreateOrderInput` gains `NowUtc` and `CalculateAsync` takes it instead of reading
`DateTime.UtcNow` inline). Add a §verify item: *"grep `DateTime.UtcNow` in the express path — zero hits
below the capture point"*, and a test that a reservation is never attached to an order whose persisted
price carries no surcharge.

---

### CH-C9 — `Remaining` is defined twice, with two different meanings, and both ship on client DTOs under names that will be read as the same number.

**The hole.** D6: *"`Remaining` = live slots left in `PeriodKey` **AFTER** this booking would be
granted (0 when not a member)."* D7: *"`int? ExpressUpgradesRemaining` = `max(0, quota − live slots in
the current PeriodKey)`"* — i.e. **before**. D7 then says *"`ExpressUpgradesRemaining` rides the quote
too, so the wizard can say '1 left' at the moment of choice"* — the quote's value comes from the
resolver (post-grant) and the membership screen's from the handler (pre-grant), and they differ by
exactly 1 for the same member at the same instant. For quota 2 with 1 used, the membership screen says
"1 left" and the quote says "0 left" while offering the waiver.

Also, D6 returns `PeriodKey` — a storage detail — out of a "pure resolver" into the pricing layer, where
`CancellationPolicy` returns only policy numbers. Whether the key is a DTO field is undecided; if it
ever reaches a client it is a new leak.

**What I want changed.** Pick one definition, name the other differently (`RemainingBeforeThisBooking`
/ `RemainingAfterThisBooking`), and state which one each of the two DTO fields carries. T-0514 renders
this number; an off-by-one in an ADR becomes an off-by-one in five locales. And say whether `PeriodKey`
crosses the DTO boundary (I recommend: no).

---

### CH-C10 — Smaller, but they belong in the record

1. **There is no global `IsActive` query filter.** `ApplyTenantQueryFilters`
   (`CleansiaDbContext.cs:201-268`) filters on `TenantId` only; `BaseEntity.IsActive`
   (`Cleansia.Core.Domain/Common/BaseEntity.cs:7`) is not filtered anywhere. So "soft delete" here is a
   convention every read must implement by hand: the remaining-count query, any admin/support read, and
   any future consumer must each write `.Where(u => u.IsActive)` or silently count released rows.
   D3's *"Soft-delete per the B6 judgment call"* should say so, and §verify should carry
   *"every read of `MembershipBenefitUsages` filters `IsActive`"*.
2. **The reservation SQL's shape is copied from a statement whose only proof is production traffic, and
   the ADR's own §"did not examine" admits the `COUNT(*) FILTER … HAVING … ON CONFLICT … RETURNING`
   combination is unverified.** Given CH-C1 + CH-C2 + CH-C3, I would make the integration test against
   real PostgreSQL a **precondition of accepting the ADR**, not a T-0512 acceptance criterion — three of
   the four defects above are only visible against a real database.
3. **`RETURNING "Id", "SlotOrdinal"`** (D3) vs the archetype's `RETURNING "SlotOrdinal" AS "Value"`
   (`PromoCodeRedemptionRepository.cs:73`). The archetype's alias exists because
   `SqlQueryRaw<int>` materializes a scalar projection named `Value` (`:99-101`). A two-column
   `RETURNING` needs a keyless entity type or a different call. Trivial, but it is the kind of "adapted,
   not copied" detail that turned into the `42P08` production bug last time — and the reserved `Id` is
   already known client-side (`@id`), so returning it buys nothing.
4. **`GetMyMembership.Handler` gaining "one collaborator (3 total)"** (D7) understates it: to compute a
   country-anchored `PeriodKey` (CH-C7) it needs the usage repo **and** a country/config source, i.e.
   4 dependencies — unless CH-C7 is resolved by dropping the country anchor for reads.

---

### What I checked and found sound

Named explicitly, because silence is not assent.

- **`PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync` is accurately described in every
  detail the ADR cites.** The single-statement `INSERT … SELECT … HAVING … ON CONFLICT DO NOTHING …
  RETURNING` exists at `:60-74`; the ordinal is derived in SQL, never pre-read (`:37-46`); the
  `NpgsqlDbType.Text` tenant parameter and the `42P08` story are real and correctly quoted (`:85-93`);
  the "0 rows ⇒ null, a RESULT not an exception" contract is real (`:97-106`); the declared UoW
  exception is real and documented as such (`:48-53`). D3's insistence on the explicit tenant
  parameter (§verify #3, "a hard reject") is correct and worth keeping exactly as written.
- **The `SELECT`-then-`INSERT` race the ADR uses to kill A1 is real and was really paid for.**
  `PromoCodeService.cs:120-128` keeps the pre-read explicitly demoted to a "cheap in-memory FAST PATH …
  NOT the source of truth", and `TryIncrementGlobalRedemptionsAsync` is a conditional `UPDATE` with a
  compensating decrement on the per-user failure (`PromoCodeService.cs:150-173`). CH-1's rebuttal of A1
  stands on evidence; I have no challenge to A1's rejection.
- **D6's gate semantics genuinely match the ancestor.** `CancellationPolicyResolver.cs:27-30`
  (`string.IsNullOrEmpty(userId)` → default) and `:35-39` (`activeMembership == null ||
  FreeCancellationWindowHours <= 0` → default) are exactly as the mirror table claims, so
  `!AllowsExpressUpgrade || ExpressUpgradesPerMonth <= 0` → no waiver is a faithful adoption, and the
  fail-closed `0` default (D2.1) is consistent with it.
- **The "active membership predicate already exists once" claim is true.**
  `UserMembershipRepository.ActiveForUserQuery` (`:20-30`) is the single SQL expression of
  `Status == Active && CurrentPeriodEnd > UtcNow`, with `GetActiveForUserNoTrackingAsync` (`:15-18`)
  as the read-only entry point `CancellationPolicyResolver.cs:32` already uses. A `PastDue`/`Paused`
  member gets nothing without a new rule, as D6 says. No challenge.
- **`BookingPolicy` taking a `bool` parameter rather than a membership is right, and the precedent is
  real.** `CalculateCancellationFeeRate`'s `freeCancellationHoursOverride` (`BookingPolicy.cs:101-119`)
  is exactly that shape, and the class is a `static class` of consts and pure functions
  (`BookingPolicy.cs:12`) that would have to become async and DB-bound under A11. A11's rejection is
  correct. The optional-parameter-with-`false`-default choice is also right for greppability — my only
  concern about it is CH-C8 (which instant it is evaluated at), not the signature.
- **`CountryConfiguration.TimeZoneId` exists and is nullable** (`:27`), and
  `GetDashboardStats.ResolveTimeZone` (`:252-266`) really does catch `TimeZoneNotFoundException` and
  `InvalidTimeZoneException` and fall back to `TimeZoneInfo.Utc`. D2's fallback posture ("a pricing call
  site must never throw over a time zone") is sound; my only objection is accessibility (CH-C7, minor)
  and which country (CH-C7, blocking).
- **A7's rejection of the `X-Time-Zone` header is correct.** `IUserSessionProvider.GetTimeZoneId()` is
  used at `GetDashboardStats.cs:64` for read-only presentation, and using a client-controlled header to
  gate an entitlement would be exactly the S1/S7 defect D2 describes. No challenge.
- **The UoW pipeline behaves as the ADR assumes for *ordinary* writes.**
  `UnitOfWorkPipelineBehavior.cs:11,35-38` keys the commit on the request type name ending in
  `Command`, and commits only on `BusinessResult { IsSuccess: true }` (`:27-30`). `CommitAsync` is
  audit-stamping plus `SaveChangesAsync` with no transaction (`CleansiaDbContext.cs:67-99`). So the
  ADR's *rule* — "everything except the reservation rides the pipeline" — is compatible with the
  pipeline. It is the ordering relative to that commit that breaks (CH-C3).
- **Tenant scoping the entity (`ITenantEntity`, T-0512 AC3) is the right call and D3.1's tenancy
  argument against `ProcessedMessage` is sound.** The global filter is applied to every `ITenantEntity`
  automatically (`CleansiaDbContext.cs:203-206`), single-tenant mode is handled
  (`:239-246`), and a request-scoped entitlement genuinely should not import the queue consumer's
  tenant-global posture. **D3.1's rejection of `IIdempotencyGuard`/`ProcessedMessage` I find sound, not
  convenient** — the cardinality argument alone is decisive (a binary claim on one opaque key cannot
  answer "how many left", which T-0514 AC4 requires from the server), and the transaction-scope
  argument is confirmed by the pipeline reading above. I would keep D3.1 as written. **But note the
  irony CH-C1 creates:** having rejected the tenant-global mechanism for being tenant-global, the ADR's
  replacement is unenforced precisely when the tenant is null.
- **`CleanupStalePendingOrders` is accurately described *as an order sweep*** — hourly cutoff semantics,
  `OlderThanHours = 1` default, the abandoned-PaymentSheet rationale (`:13-23`, `:26`, `:45`). The ADR's
  characterization of *the failure class* is right; only its claim that this job can reclaim usage rows
  is wrong (CH-C4).
- **D5's one-table-plus-discriminator choice, and the T-0517 cross-check rule** ("one table +
  discriminator when the rows have the same shape; config-driven when the fields differ") I have no
  challenge to — it is a clean, checkable distinction and the honest statement of the per-benefit plan
  column cost is the right kind of honesty.

**Out of my lane, deliberately not attacked:** D4's release table and the accepted/rejected exploits
(Challenger B), A1/A2 and the mid-month plan-swap gap (Challenger A), and the §Copy sequencing.
