# Challenger A — "the alternative" · ADR-0035

**Lane assigned by the author's §Verdict:** attack **A1** (derive the count from `Order` history) and
**A2** (a counter column); then attack **D5** (one table keyed by benefit) on whether the generality is
earned. **The bar set for me:** *show that the race or the history-recompute is not real, or that its cost
is lower than a table.*

**I could not clear the bar, and I tried the honest way — not by re-arguing the race, but by reading what
`Order` actually persists.** The table survives. But it survives for a reason the ADR does not give, the
ADR's stated reasons for killing A1 and A2 are partly wrong, the cheapest alternative to a table is not in
the table of alternatives, and the machinery I was told to attack (nullable `OrderId` + orphan sweep +
a second out-of-band statement) turns out to be **self-inflicted by a silent inversion of the very
archetype the ADR claims to mirror**. Nine findings.

**Every claim below is a read of source at `master`. I ran nothing** — no build, no test, no query.

---

### CH-A1 — A1 is dead, but the ADR kills it with the wrong weapon. The decisive fact is that the express decision is **never persisted** and is evaluated against **three different clocks** — so A1 cannot reproduce the past even single-threaded on one machine.

**The hole.** The ADR's A1 row leads with the S7 race ("no arbiter") and treats non-derivability as
defects (2)/(3). That ordering invites exactly the rebuttal the ticket feared — *"the race is
hypothetical, ship the cheap thing"* — and the author's own CH-1 defense has to fall back on "Azure DEV
is deployed and scales", which is an argument about deployment topology, not about the data. The
mechanical, deployment-independent killer was available and was not used.

**Why it matters (citations).**

1. **Nothing about the express decision is persisted.** `grep ExpressSurcharge` across `src/**/*.cs`
   returns **zero hits inside `Cleansia.Core.Domain`**. `Order` snapshots *every other* price adjustment
   — `TierDiscountAmount` (`Order.cs:179`), `PromoDiscountAmount` (`:192`), `MembershipDiscountAmount`
   (`:207`), `MembershipPlanIdAtPurchase` (`:215`) — and **no express field of any kind**. The +20% is
   folded into `TotalPrice` at `OrderPricingCalculator.cs:64-71` and the `ExpressSurchargeApplied` /
   `ExpressSurchargeAmount` pair is returned in a DTO (`IOrderPricingCalculator.cs:25-26`) that no
   persistence path consumes. **The information is computed and thrown away.** This is the single fact
   the panel asked me to establish, and it is unambiguous.
2. **`CreatedOn` is stamped after the pricing decision, not at it.** `CleansiaDbContext.CommitAsync`
   captures `var currentTime = DateTime.UtcNow` (`:71`) and stamps `entity.Entity.Created(stateUser,
   currentTime)` (`:86`) for every `Added` `Auditable` whose `CreatedBy` is empty (`:84`) — which is the
   case for `Order`. So `Order.CreatedOn` is the **commit** instant, strictly later than the instant the
   surcharge was decided. A1's predicate `(CleaningDateTime − CreatedOn) ∈ [2h,4h)` therefore reports a
   **systematically smaller** lead time than the one that actually priced the order, by the full duration
   of the request. At the 4h boundary that flips the classification, in the direction of counting orders
   that were *never* surcharged.
3. **Three clocks, one order.** `BookingPolicy.RequiresExpressSurcharge` is called with an independently
   read `DateTime.UtcNow` in the validator's calculator run (`OrderPricingCalculator.cs:65`, reached from
   `CreateOrder.cs:159-175`), again in the handler's calculator run (same line, `CreateOrder.cs:266-274`),
   and a third time in `OrderFactory.cs:100-102`. A derived count is a fourth. The express classification
   is not a property of the order; it is a property of *when you ask*.

**What I want changed.** Rewrite A1's "why not" so defect order is: **(1) the platform does not persist
the fact you would be counting** (`ExpressSurcharge*` absent from `Core.Domain`); **(2) `CreatedOn` is a
commit stamp, not a decision stamp** (`CleansiaDbContext.cs:71,86`), so the reconstruction is off by the
request duration and wrong at the boundary; **(3) the S7 race**; (4) GDPR. This is stronger, it is
mechanical, and it does not require anyone to agree about how many instances Azure runs. Keep the race —
demote it.

---

### CH-A2 — The cheapest real alternative is not in the table. It is **not** "derive at query time"; it is **two nullable columns on `Order` + a filtered partial unique index**. The ADR must kill *that*, because it is the version a reviewer will propose.

**The hole.** A1 as written is a straw alternative — nobody who has read CH-A1 would propose deriving
from a time subtraction. The steelman is: *persist the decision on the row you are already inserting.*

```
Order.ExpressWaiverPeriodKey   string?   -- the D2 key, or NULL
Order.ExpressWaiverOrdinal     int?      -- 0-based, or NULL
UNIQUE (TenantId, UserId, ExpressWaiverPeriodKey, ExpressWaiverOrdinal) WHERE "ExpressWaiverOrdinal" IS NOT NULL
```

This beats the ledger on every axis the ADR prices: **zero new tables, zero new repository, zero new
entity, zero new role card.** The audit trail is intact (the waiver is on the order it paid for, which is
*better* than a join). D4's release is `ExpressWaiverOrdinal = NULL` on the existing cancel path — one
assignment, no soft-delete, no filtered-index subtlety. GDPR is already handled because
`Order.AnonymizeCustomerData()` is already the erasure hook. **And there is no orphan class at all**,
because the row *is* the order — the failure mode D3.2 spends a section bounding cannot exist.

**Why it dies — and the citation the ADR should use.** The `Order` insert rides EF change tracking inside
the MediatR UnitOfWork pipeline (`CreateOrder.cs:283-303` adds via `OrderFactory` → `orderRepository.Add`
at `OrderFactory.cs:167`; the commit is `CleansiaDbContext.CommitAsync`). You therefore **cannot compute
the ordinal inside that INSERT** — EF emits the parameter values it was handed. So the ordinal must come
from a pre-read count (the race), and the unique index fires as a **constraint violation at the pipeline
commit**. The codebase has already ruled on precisely that outcome, in writing:

> `PromoCodeRedemptionRepository.cs:48-53` — *"…is REQUIRED for atomicity — the reservation must land (or
> be rejected) on its own, not deferred to the order's UoW commit (**a unique violation there would roll
> back the whole paid order, which is worse than the bug**…)"*

That is the sentence that kills the column variant, and it is a decision the platform already paid for.

**What I want changed.** Add this as **A1b** to the Alternatives table with the
`PromoCodeRedemptionRepository.cs:48-53` citation. Without it the ADR looks like it compared a table
against a bad idea. With it, the ADR shows it compared a table against the *best* cheap idea and named the
line that rules it out. This is the single highest-value edit in my lane.

---

### CH-A3 — The ADR asserts the `PromoCodeRedemption` archetype is "Mode A, claim-before-act". **It is not.** Production is price-first / reserve-after / fail-soft. Every piece of machinery I was sent to attack exists only because the ADR silently inverted the archetype — and the inverted alternative is not in the table.

**The hole.** The header says this decision *"Mirrors the `PromoCodeRedemption` per-user slot-reservation
archetype"*, and D3.1 says *"→ **Mode A, claim-BEFORE-act, mandatory.** The reservation is taken **before**
the waived price is computed."* Those two sentences are not both true of the same archetype.

**What the archetype actually does (citations).**

- `CreateOrder.Handler` calls `orderFactory.CreateAsync` (`CreateOrder.cs:283`) → then
  `orderPaymentDispatcher.DispatchAsync` (`:305`) → and **only then** `orderPromoApplier.ApplyAsync`
  (`:315`). The discount is in `Order.TotalPrice` and the customer has been charged **before** any slot
  is reserved.
- `OrderPromoApplier.ApplyAsync:50-53` states it as policy: *"Best-effort: failure logs but never rolls
  back — the customer already paid and the promo just doesn't get tracked. **Apply runs post-persist so
  the redemption row gets the order id.**"* Failure is a `LogWarning` (`:61-66`).
- Consequently `PromoCodeRedemption.OrderId` is `[Required]` (`PromoCodeRedemption.cs:23-24`; EF config
  `PromoCodeRedemptionEntityConfiguration.cs:25-27`), `CreateReserved` **throws** on a blank order id
  (`PromoCodeRedemption.cs:70-73`), and the reservation takes `orderId` as an input parameter
  (`PromoCodeRedemptionRepository.cs:33`). There is even a **unique index on `OrderId` alone**
  (`PromoCodeRedemptionEntityConfiguration.cs:72-73`). **The archetype is structurally incapable of
  producing an orphan.**

**Why it matters.** Everything the panel flagged as "a lot of machinery" is downstream of the inversion,
not of the table: the **nullable `OrderId`** (D3 table), the **`AttachOrderAsync` stamp** (D3.2 step 5),
the **orphan class**, the **1-hour reclaim dependency on `CleanupStalePendingOrders`**, and the **second
out-of-band statement** the author concedes in Consequences. The ADR names exactly one deviation from the
archetype (`COUNT`-of-live vs `MAX+1`, CH-6) and presents the ordering as adoption. It is the larger
deviation and it is unnamed.

**The alternative that is missing — A13, reserve-after-persist.** Order the calls exactly as the promo
path does: price with the pure resolver's answer → `Order.Create` → persist → reserve with the known
`OrderId` → on `null`, log. Cost: `OrderId` non-nullable, no `AttachOrderAsync`, **no orphan class at
all**, no sweep dependency, **one** out-of-band statement instead of two, and byte-consistency with the
archetype. Price: the cap becomes **soft** — a concurrent race loser keeps a waiver with no slot, exactly
the trade the platform already accepted for promo discounts.

**I am not claiming A13 wins.** Mode A genuinely narrows the resolver-read→reserve window from *the whole
request* (order insert + Stripe dispatch — seconds) to *a few statements*. That is a real defense and the
author should write it down. What I am claiming is that the ADR currently gets Mode A for free by
mislabelling the archetype.

**What I want changed.** Three things, all small: (a) stop describing the archetype as claim-before-act —
say plainly *"the archetype is reserve-after-persist and fail-soft (`OrderPromoApplier.cs:50-53`); this
ADR deliberately inverts it"*; (b) add **A13** to the Alternatives table with its failure class named
(soft cap under concurrency) against Mode A's failure class (orphan credit for ≤1h); (c) answer the
asymmetry in one sentence — **why does an express waiver get a hard cap when a promo discount, which is
also money and also per-user-capped, was given a soft one?** If the honest answer is "a promo needs a code
you must possess, an express waiver needs nothing but a Plus subscription", say that — it is a good answer
and it is currently missing. If the answer is "promo is wrong too", say that and file it.

---

### CH-A4 — A2's rejection rests on two claims that are wrong for the steelmanned form, and **omits the two facts that actually kill it**.

**The hole — two of the ADR's four arguments do not survive contact with the strong version of A2.**

The ADR attacks a counter *without* a period stamp. Nobody would propose that. The steelman is a counter
**plus** a stored period key on `UserMembership`, consumed by one atomic conditional statement:

```sql
UPDATE "UserMemberships"
SET "ExpressUsed" = CASE WHEN "ExpressPeriodKey" = @k THEN "ExpressUsed" + 1 ELSE 1 END,
    "ExpressPeriodKey" = @k
WHERE "UserId" = @u AND ("ExpressPeriodKey" IS DISTINCT FROM @k OR "ExpressUsed" < @max)
RETURNING "ExpressUsed";
```

- **"the reset has to be *driven* (a sweep, or an opportunistic check on read) rather than falling out of
  a key — a reset job that misses a month is silently wrong"** — **false** for this form. The reset falls
  out of the `CASE` exactly as it falls out of the key in D2. No sweep, no job, nothing to miss.
- **"Concurrency needs an atomic conditional `UPDATE`, which works"** — the ADR **concedes** concurrency.
  So concurrency, the headline argument for the whole design, is *not* a discriminator between D3 and A2.
  Leaving that concession sitting in the table while D3 claims the DB-arbiter high ground reads as
  inconsistent, and a lead should notice.

**The two facts that do kill A2, both citable, both absent from the ADR.**

1. **A yearly Plus member would get two express upgrades per YEAR.** If the counter lives on
   `UserMembership`, the only period-rollover hooks on that row are `UpdateFromStripeWebhook:133-136`
   (*"Detect period rollover… Stripe sends a period-rolled webhook on each renewal, which moves
   `CurrentPeriodEnd` forward"*) and `ApplyPlanSwap:194`. Both key on the **Stripe billing period**.
   `BillingInterval.Yearly` is a first-class value (`MembershipPlan.cs:16`) with dedicated pricing
   support (`MonthlyEquivalentPriceCzk`, `:69-73`) and dedicated plan ordering
   (`UserMembershipRepository.cs:61`). The owner ruled a **calendar month**. A calendar-month quota
   living on a billing-period row is a category error for every annual subscriber. Fixing it means adding
   a period stamp — at which point A2 **is** D2's key, denormalized onto a row that Stripe webhooks
   mutate.
2. **Churn resets the quota.** `UserMembership.cs:14-20` documents the filtered partial unique index and
   states its purpose: *"filtered to Active so a cancelled/expired membership plus a new active
   subscription is still allowed — a full unique index would wrongly block that legitimate
   re-subscribe-after-cancel case."* A re-subscribe is therefore a **new row**, and a counter on it starts
   at zero. A quota keyed to the *enrolment* resets on churn; a quota keyed to *(user, period)* — which is
   what D3 does — does not. **The ADR's own shape is immune to an exploit it never claims credit for.**

**Also, the responsibility argument the ADR should be making and isn't.** `UserMembership` states its own
card at `:8-11`: *"Backed by a Stripe subscription — the local row is a mirror, with Stripe as the
authoritative source for billing state."* "How many benefits this human consumed this calendar month" is
not on that card; Stripe does not know it and cannot arbitrate it. Putting entitlement state on a mirror
row is the RDD violation, and it is a cleaner argument than "couples the quota to Stripe webhook timing".

**What I want changed.** Replace A2's "why not" with (1) the yearly-billing-interval category error and
(2) the churn reset, both cited; delete the "reset must be driven" claim; either delete or explicitly
label the concurrency concession so it does not read as an argument for D3; keep the audit-trail argument,
which is the one that survives intact and which D7 depends on (*"support can answer 'did I really use
both?' from the row today"*).

---

### CH-A5 — D5's generality is **speculative**, and the ADR's own supporting evidence refutes it once you read the five perks. AC5's "reused unchanged" is false as written.

**The hole.** D5 justifies the `BenefitKind` discriminator with: *"The domain comment already names the
tracker generically (`MembershipPlan.cs:102`), **Plus advertises five perks, and a second metered one is
plausible**."* I read the five.

| Perk | Where | Shape |
|---|---|---|
| Member discount | `values/strings.xml:841-842`; `MembershipPlan.DiscountPercentage:89` | a **percentage**, applied to every order — unmetered |
| Free cancellation | `:839-840`; `MembershipPlan.FreeCancellationWindowHours:97` | a **window in hours** — unmetered |
| Recurring bookings | `:837-838` | a **capability gate** (boolean) |
| Favourite cleaner | `:835-836` | a **capability gate** (boolean) |
| Express upgrade | `MembershipPlan.AllowsExpressUpgrade:105` | **the one being metered** |

**Not one of the other four is countable.** `MembershipPlan` carries exactly one countable-looking benefit
flag. The sentence cited as evidence *for* generality is, read carefully, evidence *against* it: five
perks, one meterable. "A second metered one is plausible" has **no candidate**, in the plan entity or in
the shipped copy on any of the three clients.

**I am not asking you to drop the discriminator.** An int column with one value is nearly free and
removing it later costs a migration. What I am attacking is the **overclaim in D5/AC5**:

> *"The table, the index, the reservation statement, the release path, the orphan sweep and the
> remaining-count query are **reused unchanged**."*

That is false as written, and the ADR's own D3/D4 prove it: the row carries an **`OrderId` FK**, and D4's
entire release rule is expressed in `Order` vocabulary — `hasBeenAccepted` (`CancelOrder.cs:103-104` per
the ADR), `CancelledBy` (`CancelledBy.cs:10-13`), and the stale-pending order sweep. A second metered
benefit that is **not order-shaped** (say "N priority-support contacts per month") reuses the entity and
the index and gets **nothing** from the release path, while inheriting an `OrderId` column that is
meaningless for it. What you have designed is not a generic benefit ledger; it is an **order-linked
waiver ledger with a discriminator**, and it should say so.

**Third point — this ADR carries *three* speculative hooks, not one.** (a) `BenefitKind` — cheap, keep it.
(b) The `"B:{UserMembershipId}:{periodStartUtc}"` key format in D2's table — documented, used by nothing.
(c) **`UserMembershipId`, `[Required]`** on the row, justified as *"makes a billing-anchored key computable
and makes support answerable"*. A `NOT NULL` constraint bought for a hypothetical is the one hook you
cannot retract without a migration — and it re-introduces exactly the enrolment coupling that CH-A4(2)
identifies as A2's fatal flaw. If it earns its place on **present-day support value alone**, say that and
drop the billing-anchor justification; otherwise make it nullable.

**What I want changed.** (a) Delete *"a second metered one is plausible"* or name the candidate. (b)
Restate AC5 honestly: *reused unchanged for **order-shaped** benefits; a non-order-shaped benefit reuses
the entity and the index and needs its own release rule.* (c) Justify `UserMembershipId [Required]` on
present-day value or make it nullable. (d) Consider naming the entity for what it is
(`MembershipBenefitUsage` is fine, but the class doc must say "order-linked").

---

### CH-A6 — The ADR checks the handler-dependency bar against the wrong class. `OrderFactory` goes from **8 collaborators to 10**, and nothing in the ADR notices.

**The hole.** D7 carefully checks one collaborator count: *"`GetMyMembership.Handler` gains one
collaborator (3 total) — well inside the handler-dependency bar."* Meanwhile D3.2 and §verify #6 put both
new collaborators into `OrderFactory`.

**Why it matters.** `OrderFactory`'s primary constructor already takes **eight**: `IOrderRepository`,
`IServiceRepository`, `IPackageRepository`, `ICompanyInfoRepository`, `ICountryConfigurationRepository`,
`IVatCalculator`, `ILoyaltyService`, `IUserMembershipRepository` (`OrderFactory.cs:22-30`). D6 adds
`IExpressWaiverResolver` and D3.2 adds `IMembershipBenefitUsageRepository` → **ten**. The architect
charter's smell threshold is eight. `OrderFactory.CreateAsync` would then own: tier discount, membership
discount, the LOY-003 cap, promo resolution, express waiver resolution, slot reservation, price
finalization, order construction, services, packages, estimated time, employee count, VAT breakdown,
status track, persistence, **and** an out-of-band slot attach.

Note also that `ICountryConfigurationRepository` is already there (`:27`, used at `:157`), so D2's
timezone resolution has a home — good. But it means the resolver and the factory will each resolve the
country config unless the ADR says which one owns it.

**What I want changed.** Either state explicitly that `OrderFactory` is a *builder*, not a handler, and
that the bar does not apply to it (with a reason), or move the reservation out — the natural seam is a
small `IExpressWaiverConsumer` collaborator that owns resolve+reserve+attach as one responsibility, which
keeps `OrderFactory` at nine and gives the reviewer one grep target instead of three (§verify #5 and #6
currently ask them to grep two different classes for two opposite properties). Also say who resolves
`CountryConfiguration` for the period key so it is not fetched twice.

---

### CH-A7 — The ledger **re-creates the `user → order` link that `Order.AnonymizeCustomerData()` deliberately severs**. That is a real cost of choosing a table over A1, and it is not in Consequences.

**The hole.** The ADR uses GDPR as A1's defect (4) — *"an anonymized order silently drops out of the
count"* — without noticing that the same fact cuts the other way.

**What I verified.**
- `Order.AnonymizeCustomerData()` nulls `UserId` (`Order.cs:618`) **and** `MembershipPlanIdAtPurchase`
  (`:620`), and `GdprDeletionService.AnonymizeUserDataAsync` calls it for every order the user ever placed
  (`GdprDeletionService.cs:181-192`). A1's defect (4) is **CONFIRMED**.
- **The erasure is an in-place anonymization of the `User` row, not a delete** (`:240` `user.Anonymize()`;
  `:241` `Deactivated(...)`). I checked this specifically because it *would* have been a blocking defect:
  `MembershipBenefitUsage.UserId` with `OnDelete.Restrict` (D3) is therefore **safe** — nothing blocks
  erasure. That part of D3 is sound.
- But `GdprDeletionService`'s constructor lists fifteen repositories (`:14-32`) and
  **`IPromoCodeRedemptionRepository` is not among them.** `PromoCodeRedemptions` already survives an
  erasure carrying the exact `(UserId, OrderId)` pair that `Order.AnonymizeCustomerData()` just removed
  from the order. `MembershipBenefitUsage` as specified would be a **second** such table.

**Why it matters.** (a) It is a genuine, un-priced cost of the table that A1 does not have — A1 loses the
count *because the platform deliberately severs the link*, which is a privacy feature, not only a defect.
Stating it makes the trade honest. (b) "Not decided" here means a security reviewer re-opens it after
acceptance, which is exactly what an ADR is for. (c) The precedent (`PromoCodeRedemption`) means either
posture is defensible — but it must be *chosen*.

**What I want changed.** One line in Consequences ("the ledger preserves a user→order link that order
anonymization removes; precedent: `PromoCodeRedemption`"), and one explicit ruling in D8: is
`MembershipBenefitUsage` **in or out** of `GdprDeletionService`'s sweep? If out, say why (the `User` row is
anonymized in place, so `UserId` no longer identifies a natural person) — that is a defensible answer and
it takes one sentence.

---

### CH-A8 — §Copy's "Fact 1" is **refuted at `master`**. The strings the ADR orders shipped-now-as-urgent do not exist, were already removed, and are pinned absent by regression tests in both mobile clients — which will **fail** the moment T-0493's copy lands.

*(Outside my assigned lane. Reporting it under Gate 0 evidence discipline because it drives an urgent
dispatch decision and a ticket's entire premise.)*

**The claim.** §Copy: *"**Fact 1 — 'same-day' is wrong and it is shipping.** All five locales on both
mobile clients promise *'One free same-day booking per month, no surcharge'* (`values/strings.xml:844`,
`Localizable.xcstrings:14121`, + `values-cs/:832`, `values-sk/:829`, `values-uk/:829`, `values-ru/:829`).
… Meanwhile the web client advertises a third, different product — *'Pay less for last-minute bookings
inside the express window'*, an **uncapped discount** (`cleansia.app en.json:1095`)."*

**What is actually there.**

- **`values/strings.xml:844` is not a string. It is the first line of a comment** stating the opposite:
  `:844-847` — *"No express perk anywhere — not on the subscribe screen, the success screen or the
  management pills. Nothing in pricing reads AllowsExpressUpgrade… Restore this perk only together with
  the code that waives the surcharge."* (The ADR cites `:846-847` as that comment three paragraphs later,
  so it contradicts its own citation.)
- `grep -i express` across `customer-app/src/main/res` returns, per locale, only `booking_slot_express`
  (`values/:560`, `values-cs/:557`, `values-sk|uk|ru/:554`) and `booking_summary_express_surcharge`
  (`values/:677`, `values-cs/:667`, `values-sk|uk|ru/:664`) — **charging** labels, all correct. **No
  `membership_perk_express_*` key exists in any of the five Android locales.**
- **iOS is the same.** `MembershipPerks.swift:6-9`: *"Express upgrade is deliberately absent — here, on
  the subscribe screen and on the success screen… Advertising it would promise something the product does
  not deliver."* `enum MembershipPerk` has exactly three cases (`:10-13`).
- **Web has no such claim.** `cleansia.app/src/assets/i18n/en.json:1090-1095` lists exactly three
  membership benefits: `benefit_discount_*`, `benefit_cancel_*`, `benefit_favorite_*`. The ADR cites
  `:1095` for the express sentence; `:1095` is `"benefit_favorite_body": "Pick a cleaner you've worked
  with before…"`. `grep -i express` in that file returns only `:762 "slot_express": "Express +20%"` and
  `:764 "express_surcharge_label": "Express slot (+20%)"`.
- Repo-wide, `membership_perk_express` appears in **exactly five files**, three of which are this ADR,
  `agents/archive/2026-08/backlog/status/sprint-15.md`, and `T-0513`. **The other two are regression guards asserting its
  absence**: `MembershipExpressClaimTest.kt` (three tests, three screens × five locale bundles, `:33-75`)
  and `MembershipExpressClaimTests.swift` (`:19-57`, including `testTheL10nAccessorsAreGone`).

**Why it matters — two consequences, the second worse than the first.**

1. The ADR's sequencing ruling — *"The corrective half ships immediately and does NOT wait for the
   implementation… Waiting for the mechanism to ship is choosing to keep a false statement live for the
   length of a build"* — is answering a problem that was already fixed **with tests**. A developer
   dispatched on it will be sent to delete strings that do not exist. `T-0513`'s Context table carries the
   same stale citations under the header *"every file:line is PM-verified"*
   (`T-0513…md:26-32`, citing Android `:843-844` and web `en.json:1094-1095`).
2. **The guard tests will block the affirmative half.** `MembershipExpressClaimTest.kt:63-75` and
   `MembershipExpressClaimTests.swift:34-57` fail on *any* `membership_*` string in *any* of the five
   locales containing `express` / `expres` / `експрес` / `экспресс`. The correct new copy — *"Two free
   express bookings each calendar month"* — turns **three green tests red** in each mobile client on the
   day T-0493 lands. Neither the ADR nor T-0513 mentions these tests. This is a concrete, checkable AC
   that is currently owned by nobody.

**What I want changed.** Rewrite §Copy against `master`. The corrective half is **already done**; delete
the urgency ruling or re-target it (the only surviving inaccuracy I found is that the web/mobile perk lists
*omit* the perk, which is honest today and becomes an omission after T-0493). Add the real constraint:
**T-0493/T-0513 must retire or invert `MembershipExpressClaimTest.kt` and `MembershipExpressClaimTests.swift`
in the same wave as the copy**, plus the Android comment at `values/strings.xml:844-847` and the iOS doc
comment at `MembershipPerks.swift:6-9` — four artifacts, not one. And the PM should re-ground T-0513's
Context table before dispatch.

---

### CH-A9 (minor) — `UserMembership.CurrentPeriodEnd`'s doc already commits the codebase to a **billing-anchored, once-per-period** express quota. D2 overrides it and the ADR does not list it as a doc that goes stale.

`UserMembership.cs:46-51`: *"End of the current Stripe billing period. **Used by benefit usage tracking
("free express upgrade once per period")** and by `IsActive` to gate benefits during the grace window."*

That is the domain stating an intent — billing-anchored, and **once** — that D2 (calendar month) and D2.1
(`ExpressUpgradesPerMonth = 2`) both contradict. The ADR cites the sibling comment at
`MembershipPlan.cs:99-104` and correctly claims to be its resolution, but misses this one. On acceptance
there would be two domain comments describing the quota, disagreeing on both the period and the number —
and the CH-7 defense ("the column name is honest because the owner ruled a calendar month") is undermined
by a domain comment saying "per period".

**What I want changed.** Add `UserMembership.cs:46-51` to D8's in-scope list as a doc update landing with
T-0512. One line.

---

## What I checked and found sound

Silence is not assent, so here is what I read and did **not** find a hole in.

- **A1's S7 race is real, not hypothetical.** `PromoCodeRedemptionRepository.cs:37-53` is a fully
  commented, production-hardened closure of the identical check-then-act on the identical shape
  (per-user cap, derived ordinal, `HAVING` guard, `ON CONFLICT DO NOTHING`, `RETURNING`, `null` as a
  result not an exception). The `42P08` untyped-tenant-parameter comment at `:85-93` describes a paid-for
  production defect *"Only ever fired with a NULL tenant, which is why it survived a tenanted test run."*
  The ADR is right to refuse to re-derive this, and §verify #3 (explicit `NpgsqlDbType.Text`, hard reject)
  is the correct enforcement.
- **A1 defect (3) "cannot distinguish waived from charged" — CONFIRMED, and understated.** Nothing about
  the express decision is persisted at all (CH-A1).
- **A1 defect (4) GDPR — CONFIRMED.** `Order.cs:618,620` driven by `GdprDeletionService.cs:181-192`.
- **The new table will not block GDPR erasure.** I checked this because it would have been blocking:
  `GdprDeletionService` anonymizes the `User` row in place (`:240-241`) and never deletes it, so
  `UserId` FK `OnDelete.Restrict` is safe. (The separate re-identification concern is CH-A7.)
- **D1's "no reschedule" pre-answer holds today.** `Order.CleaningDateTime` (`Order.cs:40`) is
  private-set; I grepped for a mutator and found none. Freezing the slot at reservation is safe.
- **D6's mirror claim is accurate line-for-line, not just asserted.** `CancellationPolicyResolver.cs`:
  ctor takes one repository (`:14`), builds a default first (`:21-25`), short-circuits on empty `userId`
  (`:27-30`), fail-closes on `<= 0` (`:35-39`), returns a record the policy takes as a parameter. D2.1's
  proposed `ExpressUpgradesPerMonth <= 0` → no waiver is the *same* semantic as `:36`. I checked this
  because D5's per-benefit extension cost estimate ("one resolver") depends on the shape being real. It is.
  (The full seam attack is Challenger C's.)
- **The "active membership" predicate exists exactly once** and is `AsNoTracking`-able, so D6's pure-read
  claim is achievable: `UserMembershipRepository.ActiveForUserQuery:20-31`, exposed via
  `GetActiveForUserNoTrackingAsync:15-18`.
- **D2.1's `UpdateBenefits` claim is accurate** — it currently takes three parameters
  (`MembershipPlan.cs:166-175`), so "gains the fourth parameter" is right.
- **The new entity will pick up tenant scoping for free** by implementing `ITenantEntity`:
  `CleansiaDbContext.ApplyTenantQueryFilters:201-269` walks every `ITenantEntity` and calls
  `SetQueryFilter` (`:268`), including the single-tenant `null == null` clause (`:244-246`). D3's tenancy
  posture and §verify #1 are correct, and this is *not* the ADR-0010 tenant-global exception.
- **There is no `IsActive` soft-delete query filter anywhere** (`grep HasQueryFilter` → 0 hits repo-wide;
  the only filter is the tenant one above). So D4's soft-delete release will not be silently hidden — but
  it also means the remaining-count query must filter `IsActive` **explicitly**; nothing does it for you.
  Worth one line in D7. (Flagging for Challenger C, who owns the concurrency read path.)
- **`MembershipPlan.cs:99-104` really does defer this decision** — *"When true, usage is capped — see the
  future 'membership benefit usage' tracker."* The ADR's claim to be that tracker's decision is fair.
- **The three-pricing-evaluations constraint in §Context is real**, and is the correct hardest constraint
  on the seam: `CreateOrder.Validator.PriceMatchesAsync:159-176`, `CreateOrder.Handler:266-274`,
  `OrderFactory.cs:100-102`. A consuming resolver would burn a credit per quote. TC-BENEFIT-PREVIEW-0 is
  the right test.

**Bottom line.** The bar was to show the race or the history-recompute is not real, or that a table costs
more than the alternatives. **I could not.** The table is required — but for CH-A1's reason, not the ADR's;
the best cheap alternative (CH-A2) is missing from the record; and roughly half the machinery the panel
objected to (CH-A3) is optional and comes from a misdescribed archetype. **CH-A2 and CH-A3 are the two I
would ask the lead to treat as blocking amendments** — not because the decision is wrong, but because the
Alternatives table is what makes this ADR trustworthy in eighteen months, and it currently omits the two
alternatives a future reader will actually propose.
