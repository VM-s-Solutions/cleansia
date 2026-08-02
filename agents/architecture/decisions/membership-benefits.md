# Membership benefits — living decision doc

> **Status of this document: ADR-0035 is `accepted` (adjudicated 2026-08-02, with 16 binding
> amendments).** The panel is complete — author + three independent challengers + lead. **§3 below is
> the AMENDED shape**, which differs from the ADR's draft in seven mechanisms; several diagrams and
> tables in earlier revisions of this file described the draft and were wrong. **§1 remains what ships
> TODAY: nothing is metered yet** — T-0512/T-0493 have not landed. This banner comes off when T-0493
> ships.

**ADRs:** `../../backlog/adr/0035-metered-membership-benefit-usage.md` (**accepted**, immutable)
**Tickets:** T-0511 (this decision) → T-0512 (schema) → T-0493 (enforcement) → T-0514 (clients);
T-0513 (copy — **corrective half already shipped**)
**Open owner questions:** `Q-PLUS-02` (partially answered 2026-08-02), `Q-PLUS-03`, `Q-PLUS-01`
(the trial loop — now a dependency, see §5), plus escalations **E-1 … E-6** in the ADR's §Verdict.

---

## 1. The current shape (what is true at `master`, 2026-08-02)

Cleansia Plus advertises five perks. **Two are enforced, one is half-built, two are copy only.**

| Perk | Server enforcement | Where |
|---|---|---|
| Per-cleaning discount | **YES** | `MembershipPlan.DiscountPercentage` → `OrderFactory.cs:76-83` / `QuoteOrder.cs:141-147`, through the LOY-003 best-wins pipeline |
| Wider free-cancellation window | **YES** | `MembershipPlan.FreeCancellationWindowHours` → `CancellationPolicyResolver` → `BookingPolicy.CalculateCancellationFeeRate(freeCancellationHoursOverride:)` |
| Recurring schedules | **YES** (shipped #189) | `CreateRecurringBooking.cs:84-92` — a membership-required gate |
| **Free express upgrade** | **NO** | `MembershipPlan.AllowsExpressUpgrade` (`:105`) is returned to clients (`GetMyMembership.cs:60`) and **read by zero pricing code**. A Plus member pays the same +20%. |
| Favourite cleaner | **NO GATE** | Advertised as a Plus perk on all three clients; the server gates only on "has completed an order with this cleaner" (`CreateOrder.cs:140-154`). `Q-PLUS-03`. |

### The one predicate — "does this user have a live Plus membership"

**It exists exactly once and nothing new should be created.**
`UserMembershipRepository.ActiveForUserQuery` (`:20-29`) is the SQL form of
`UserMembership.IsActive` (`UserMembership.cs:84-85` — `Status == Active && UtcNow < CurrentPeriodEnd`),
reached via `GetActiveForUserAsync` (tracked) / `GetActiveForUserNoTrackingAsync` (read-only).

Current consumers: `CancellationPolicyResolver.cs:32`, `GetMyMembership.cs:35`, `OrderFactory.cs:76`,
`QuoteOrder.cs:141`, `CreateRecurringBooking.cs:84`. **A `PastDue` or `Paused` membership is not
active** — benefits stop, by the predicate, with no per-feature rule.

> ⚠️ **The domain contradicts itself here and it is not settled.** `MembershipStatus.cs:18-19`
> documents *"Benefits still apply during the grace window"* for `PastDue`; the predicate above makes
> that unreachable. The platform ships the predicate's behaviour. **Escalated as E-2 (§5)** — do not
> treat "PastDue gets nothing" as a decided rule until one of the two files changes.

### The benefit seam — resolver answers, policy takes the answer

The platform threads a membership benefit into a policy in exactly one way:

```
CancellationPolicyResolver.ResolveForUserAsync(userId)  →  CancellationPolicy record
        ↓ (the caller passes the answer)
BookingPolicy.CalculateCancellationFeeRate(..., freeCancellationHoursOverride: policy.FreeCancellationHours)
```

`BookingPolicy` never learns about memberships; it takes a value. `BookingPolicy.cs:101-111` documents
the contract in unusual detail — read it before designing any second benefit.

### The metered-benefit gap

`MembershipPlan.cs:99-104` says it out loud: *"When true, usage is capped — see the future 'membership
benefit usage' tracker."* **That tracker does not exist.** Nothing in the platform counts a per-user
benefit against a period; the nearest thing is `PromoCodeRedemption`, which is **lifetime**-capped, not
period-capped.

---

## 2. What the owner has settled

| Date | Question | Ruling |
|---|---|---|
| 2026-08-02 | Should the express perk be built or the copy deleted? | ***"You can upgrade."*** → **build it** (reading A: a price benefit) |
| 2026-08-02 | `Q-PLUS-02(1)` how many? | **2 per month** |
| 2026-08-02 | `Q-PLUS-02(3)` billing-anchored or calendar? | ***"reset once per calendar month"*** → **calendar**, on the 1st |
| 2026-08-02 | Who gets it? | **Plus-only** |
| — | `Q-PLUS-02(2)` rollover? | **not asked/answered.** ADR-0035's default: **no rollover** — and the stored-period-key shape makes it structural (an unused slot has nowhere to accumulate) |
| — | **Which time zone is "the 1st"?** | **not asked.** ADR-0035 D2 **as amended** defaults to the **platform-default** `CountryConfiguration.TimeZoneId` (UTC fallback), built by one factory. Non-blocking — every answer uses the same schema and the same key-builder |
| — | **Does the free trial grant waivers?** (E-1) | **not asked — now escalated.** Holding position: the seeded `ExpressUpgradesPerMonth` stays `0`, so the perk is off until answered |
| — | `Q-PLUS-03` favourite cleaner: universal or Plus-only? | **open**, deliberately not defaulted |

---

## 3. The decided shape (ADR-0035, **accepted as amended**)

### 3.1 The trade-off space, and where each axis landed

| Axis | Options | Landed on | The thing that decided it |
|---|---|---|---|
| **Storage** | derive from `Order` rows · **two nullable columns on `Order`** · counter column on `UserMembership` · a ledger table | **ledger table** (`MembershipBenefitUsage`) | **[amended]** The decisive fact is that **the express decision is never persisted** — `Order` snapshots tier/promo/membership discounts and **no express field** — and `CreatedOn` is a *commit* stamp, so the past cannot be reconstructed at all. The columns-on-`Order` variant (the real steelman) dies because EF cannot compute the ordinal inside its own INSERT, so a unique violation would land at the pipeline commit and **roll back a paid order**. A counter dies on the **yearly** billing interval and on **churn** resetting the quota. |
| **Counted unit** | express order created · express order completed · **the waiver granted** | **the waiver granted** | A booking that would never have carried a surcharge (the 9-hour "same-day" case) must cost nothing. Price and consume must happen in one transaction or they drift. |
| **Period** | `(Start,End)` pair · rolling 30 days · **stored key** | **stored `PeriodKey`** (`"C:2026-08"`) | Survives calendar **and** billing-anchored with no migration; a stored key cannot be retroactively moved by a zone/DST/config change. |
| **Time zone** | customer's (`X-Time-Zone`) · UTC · order's country · **platform-default country** | **[AMENDED] the platform-default `CountryConfiguration.TimeZoneId`**, UTC fallback, built by **one** `IBenefitPeriodKeyFactory` | The header is unauthenticated (4 credits from 2 months by changing one header). Bare UTC makes "the 1st" wrong for the first 1–2h of every month in CET. **And the order's country is not computable at 3 of the 4 sites that need the key** (`QuoteOrder` has no address; `CreateOrder.Validator` resolves the address later; `GetMyMembership` is parameterless) — it would produce *different keys per call site*, deterministically, for ~2h every month. |
| **Concurrency** | app-level check · optimistic version · **three layers: non-authoritative read + one atomic claim + a unique-index backstop** | **[AMENDED] all three, named** — and the index is **`NULLS NOT DISTINCT`** | A bare `(TenantId, …)` unique index **does not fire in single-tenant mode** (`TenantId IS NULL`, Postgres treats NULLs as distinct) — quota 2 would become quota 3+ in the platform's default deployment. `NULLS NOT DISTINCT` is **precedented** (`FiscalCounterEntityConfiguration.cs:23-29`, tenant-scoped, same reasoning). The draft's *"the database is the sole arbiter, there is no `SELECT`-then-`INSERT`"* was struck: there **is** a read, it is the non-authoritative fast path, exactly as `PromoCodeService.cs:120-128` documents. |
| **Ordinal** | `MAX+1` · `COUNT`-of-live · **smallest free** | **[AMENDED] the smallest free ordinal** (`generate_series` + `NOT EXISTS` + `ORDER BY … LIMIT 1`) | `MAX+1` never re-uses a hole; **`COUNT`-of-live is a defect** — it yields the *cardinality*, so after releasing a non-maximal slot it aims at an occupied ordinal, loses to its own index, and **capacity never returns** while the read path still says "1 left". The `HAVING` guard becomes redundant and is deleted. |
| **Ordering** | reserve-then-price · price-then-reserve | **reserve-then-price** (ADR-0023 Mode A) | **[amended reasoning]** The `PromoCodeRedemption` ancestor is **reserve-after-persist and fail-soft**, not claim-before-act — this decision *inverts* it, and the reason is written down: a promo needs a code an operator issued (and carries its own global cap), an express waiver needs nothing but a subscription, so a soft cap is farmable by every subscriber with concurrent requests alone. |
| **Consent** | — | **[NEW] no persisted `TotalPrice` may exceed the approved `command.TotalPrice`** | Mode A introduces the first pricing input that can change *between the validator and the factory inside one request*. Without this rule the race loser is charged +20% silently, with no error and no field to notice it by. A lost slot is a **re-quote** (`ExpressWaiverNoLongerAvailable`), never an upcharge and never an unmetered waiver. |
| **Reversal** | always release · never release · **conditional** | **[AMENDED] release iff no cleaner was ASSIGNED** (`!order.AssignedEmployees.Any()`), or `CancelledBy.Admin`, or orphan | Always-release enables an unbounded supply attack. Never-release charges the member's perk for **our** failures. **The draft's `hasBeenAccepted` predicate is unusable**: `Confirmed` has four writers (Stripe webhook, cash auto-confirm, admin override, cleaner) *and* `TakeOrder` writes its track only conditionally — so the flag is both false-positive and false-negative. The assignment row is the only durable evidence a cleaner exists. |
| **Generality** | column per benefit · table per benefit · **one table + `BenefitKind`** | **one table + discriminator**, honestly scoped | **[amended]** None of the other four Plus perks is countable, so the discriminator earns its place only on "an int column with one value is nearly free". And this is an **order-linked** ledger: a non-order-shaped benefit reuses the entity, the index and the statement but needs its **own** release rule. |

### 3.2 The shape in one picture (AMENDED — the draft's diagram was wrong in four places)

```
                  ONE nowUtc captured per request, threaded everywhere
                         PURE READ (safe everywhere, consumes nothing)
QuoteOrder ───────┐
CreateOrder.Val ──┼──▶ IExpressWaiverResolver.ResolveForUserAsync(userId, cleaningUtc?, nowUtc)
OrderPricingCalc ─┤         │  reads: UserMembership (the ONE predicate) + MembershipPlan
GetMyMembership ──┘         │         + live slot count for PeriodKey  (IBenefitPeriodKeyFactory)
                            │  "in window?" == BookingPolicy.RequiresExpressSurcharge — never re-encoded
                            ▼
      ExpressWaiver(InExpressWindow, Waived, Quota, RemainingBeforeThisBooking, PeriodKey)
                            │
                            │   CONSUMING — one call site: IExpressWaiverConsumer, in CreateOrder.Handler
                            ▼
        TryReserveBenefitSlotAsync(...)   →  row | null        [ONE out-of-band statement]
             INSERT … SELECT <smallest free ordinal from generate_series> …
             ON CONFLICT DO NOTHING RETURNING "SlotOrdinal" AS "Value"
                            │
              null after a WAIVED validation  ──▶  FAIL: ExpressWaiverNoLongerAvailable
                            │                          (never a silent upcharge, never a free waiver)
                            ▼
        orderFactory.CreateAsync(input with { NowUtc, Waiver = reserved })
             └─ BookingPolicy.RequiresExpressSurcharge(cleaningUtc, nowUtc, waiverApplies: Waiver != null)
             └─ Order.Create(..., finalTotalPrice, ...)   ← price frozen here, forever
             └─ orderRepository.Add(order)                ← change-tracked; NOT yet in the DB
                            ▼
        AttachAsync(reserved, order.Id)    ← CHANGE-TRACKED UPDATE, rides the UoW commit
                                              (out-of-band here would raise 23503 — the Orders
                                               row does not exist until the pipeline commits)
```

**What changed vs. the draft's diagram:** the resolver lost `countryId` and gained an optional
`cleaningUtc`; `GetMyMembership` joined the read side; the consuming call site moved out of
`OrderFactory` into a single `IExpressWaiverConsumer` in the handler (so `OrderFactory` gains **zero**
collaborators and "exactly one consuming call site" is true by construction); `HAVING` is gone; the
attach is no longer out-of-band; and the `null`-after-waived-validation branch is new.

### 3.3 The entity, in one line each

`MembershipBenefitUsage : Auditable, ITenantEntity` — **an order-linked waiver ledger** —
`UserId` · `BenefitKind` (int enum, never reorder) · `PeriodKey` (string, 64) · `SlotOrdinal` (0-based,
**smallest free**, derived in SQL) · `OrderId?` · `UserMembershipId` · `ReservedAtUtc` · `IsActive`
(live = consumed; false = released).

**Indexes (three):**
1. `UNIQUE NULLS NOT DISTINCT (TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal) WHERE IsActive`
   — the filter is what lets a release free the ordinal; **nulls-not-distinct is what makes the index
   fire at all in single-tenant mode**. Precedent: `FiscalCounterEntityConfiguration.cs:23-29`.
2. `(TenantId, UserId, BenefitKind, PeriodKey)` — the remaining-count read.
3. `("ReservedAtUtc") WHERE "OrderId" IS NULL AND "IsActive"` — the orphan reclaim.

⚠️ **There is no global `IsActive` query filter in this codebase.** Every read must write
`.Where(u => u.IsActive)` by hand or it silently counts released rows.

**Plan column:** `MembershipPlan.ExpressUpgradesPerMonth` (int, default `0` = off), sibling to
`FreeCancellationWindowHours`. `AllowsExpressUpgrade == false` ⇒ never, regardless. **Unlimited is
deliberately not expressible** (D2.1). The `0` default is also the **holding position for E-1**: the
seeded plans stay at `0` until the owner rules on the free trial.

### 3.3b The orphan, and what reclaims it

`CleanupStalePendingOrders` **cannot** reclaim a usage row — it queries `Orders`, filters
`PaymentType == Card`, and an orphan is by definition a row whose order never committed. *"No new job"*
was false. The reclaim is a **new** `IMembershipBenefitUsageRepository.ReleaseOrphanedReservationsAsync
(cutoffUtc, ct)` (tenant-ignoring) + index (3) above + a small command hung off the **existing hourly
cleanup schedule**. New command, new method, new index; no new schedule.

### 3.4 The reversal table (AMENDED — keyed on ASSIGNMENT, not on status)

| What happened | Slot released? |
|---|---|
| Order `Cancelled` with **no assigned employee** (customer cancel, system sweep, any path) | **yes** |
| Customer cancels, **a cleaner was assigned** | **no** ← the accepted exploit |
| `CancelledBy.Admin` | **yes**, even with an assignment |
| `CancelledBy.Cleaner` | **no such path exists today** — open gap, not a live rule |
| Refund on a completed order | **no** |
| Orphan (`OrderId IS NULL` past the cutoff) | **yes**, by the **new** reclaim command (§3.3b) |

> ⚠️ **Do not key this on `hasBeenAccepted`.** `Confirmed` is written by four paths —
> `TakeOrder.cs:194` (a cleaner), `HandlePaymentNotification.cs:261` (the Stripe webhook),
> `ConfirmRecurringOrder.cs:111` (cash auto-confirm) and `AdminOverrideOrderStatus.cs:56-64` — **and**
> `TakeOrder` writes its track only `if (currentStatus is New or Pending)`, so a cleaner taking an
> already-Confirmed order leaves **no trace in the status history at all**. The flag is
> simultaneously false-positive and false-negative; `AssignedEmployees` is the only durable evidence
> a cleaner exists. `CancelOrder` already `.Include`s it (`:62-63`).
>
> ⚠️ **The cancellation *fee* still keys on `hasBeenAccepted` — deliberately not harmonized.** That is
> a pre-existing defect (a customer can be charged 50% for an order no cleaner ever took), ticketed
> separately. Do not "align" the release rule back onto it.

**Accepted exploit:** a member who cancels a real, **assigned** express booking for a legitimate reason
loses the credit — and **may lose it for 0 Kč**.

> ⚠️ **The bound is the QUOTA, not the fee.** The draft claimed the 50% last-minute fee bounds the
> farming loop. It does not: `BookingPolicy.cs:127-132` returns `0m` for any cancellation within **15
> minutes** of booking — *after* the acceptance short-circuit, so it fires **only** when acceptance is
> true — and the fee is a *recorded rate*, not collected money, for every **cash** order
> (`CancelOrder.cs:136-145`). The real bound is that each iteration **spends a credit**, and there are
> **two per calendar month** — unconditionally, cash or card, at any plan configuration.
>
> ✅ **The `FreeCancellationWindowHours > 4` seeding constraint is DELETED and the proposed validator
> is REJECTED.** The shipped seed is **exactly 4** (`insert_seed_data.sql:1669`, `:1683`) and it is
> **advertised** (*"Cancel up to 4 hours before your cleaning, no fees"*). A validator would reject the
> production seed and narrow a live perk to protect an argument that no longer exists.
>
> ➡️ **Because the forfeiture can cost 0 Kč, it MUST be disclosed.** The customer order read carries a
> server-computed `bool ExpressWaiverForfeitedOnCancel`, and the cancel confirmation says *"this uses
> up one of your free express bookings this month"* whenever it is true — **especially when the fee is
> 0**. The cancel `Response`'s `FeeRate` is **not** a disclosure: it is returned *after*
> `order.Cancel(...)` has already run (`CancelOrder.cs:122-127` vs `:171-176`), and there is no
> preview endpoint.

### 3.5 The client contract (AMENDED — five fields, three regen surfaces)

| Response | New field | For |
|---|---|---|
| `GetMyMembership.Response` | `int? ExpressUpgradesPerMonth` | the plan's quota |
| `GetMyMembership.Response` | `int? ExpressUpgradesRemaining` | "1 free express left this month" |
| `QuoteOrder.Response` | `bool ExpressSurchargeWaivedByMembership` | disambiguates "waived" from "not an express slot" — without it `ExpressSurchargeApplied: false` is ambiguous |
| `QuoteOrder.Response` | `int? ExpressUpgradesRemaining` | so the wizard needn't re-fetch the membership |
| **customer order read** | **`bool ExpressWaiverForfeitedOnCancel`** | **[NEW]** the cancel-confirmation warning — the client cannot compute it (`OrderDetailDto` has no waiver field and the assignment predicate is server-side) |

**`ExpressUpgradesRemaining` has ONE meaning: remaining BEFORE the booking under consideration.** A
client that wants "after" computes `remaining − (waived ? 1 : 0)`. **`PeriodKey` never crosses a DTO
boundary.**

**Render surfaces: the membership screen, the booking quote, AND the slot grid.** The slot grid is
where the choice is made and the draft omitted it — the express badge is computed client-side from
constants with no membership input (`order-wizard.models.ts:211-214`; Android
`values/strings.xml:560`), so a member with two unused credits would see every express slot badged
**+20%** and avoid the perk they pay for. The grid renders the server's count against the client's
existing lead-time computation — server-computed, client-rendered, never client-counted.

**No new endpoint.** **An exhausted-at-quote-time member is never blocked** — they are *told* and
charged; a quota loss is a price. **But there IS one new failure path:** if the slot is lost *between*
the validator and the reservation, the command fails with **`ExpressWaiverNoLongerAvailable`** and the
client re-quotes. The invariant is *no persisted `Order.TotalPrice` may exceed the `command.TotalPrice`
the validator approved.*

**When a membership ends:** `ExpressUpgradesRemaining` is **null**, not 0 — the client renders the
non-member state. Unused slots are not carried, refunded or restored.

---

## 4. Copy — the state at `master`, and who owns the tripwire

> **The earlier revision of this section was stale and is replaced.** It described five locales
> promising *"One free same-day booking per month"* on both mobile clients and an uncapped discount on
> web. **None of those strings exists.** The corrective wave shipped as T-0513.

**What is true at `master`:** the express perk is **absent** from all three clients' benefit lists.
Android has no `membership_perk_express*` key in any of the five locales (`values/strings.xml:844-847`
is a *comment* explaining the deliberate omission); iOS `MembershipPerks.swift:6-9` says the same and
`enum MembershipPerk` has three cases; web `en.json:1090-1095` lists three benefits. That is **honest
today** and becomes an **omission** (a paid perk we do not advertise) the day T-0493 lands.

**⚠️ Two committed regression guards pin that absence — and they will go RED when the copy returns.**

- `src/cleansia_android/customer-app/src/test/java/cz/cleansia/customer/features/membership/MembershipExpressClaimTest.kt`
  — no membership screen renders `membership_perk_express`; none reads `allowsExpressUpgrade`; **no
  `membership_*` string in any of the five locales contains `express` / `expres` / `експрес` /
  `экспресс`**.
- `src/cleansia_ios/CleansiaCustomer/Tests/MembershipExpressClaimTests.swift` — the same three.

**This is correct tripwire design, not an oversight**: the tests exist so the claim cannot come back
without the code that makes it true. **T-0493 owns retiring them**, in the same PR as the affirmative
copy, and **four artifacts move together** — the two test files (**inverted**, not deleted), the
Android comment at `values/strings.xml:844-847`, and the iOS doc comment at `MembershipPerks.swift:6-9`.
A PR that lands fewer than four is incomplete.

**The sequencing rule, narrowed:** the corrective half is **done**; the affirmative half ships **with
T-0493 and not before**. There is no longer an urgency argument and none should be re-derived — T-0513's
Context table carried the same stale citations under a *"PM-verified"* header and should be re-grounded
before further dispatch.

**Constraints on T-0513's canonical sentence:** name the 2–4h lead window (not "same-day"); say **"each
calendar month"**; state the cap as **2** from `ExpressUpgradesPerMonth`; do not promise that a
cancelled booking returns the credit; **say that the cancel confirmation warns when a waiver is about
to be forfeited**; **say that unused express bookings do not carry over and end with the membership**.

Anchor (analyst owns the final wording):
> *"Two free express bookings each calendar month — we waive the 20% surcharge on cleanings booked
> 2 to 4 hours ahead."*

---

## 5. Known gaps and escalations (post-adjudication)

**Owner decisions (the PM carries these into `questions/open.md`):**

1. **E-1 — does the 14-day free trial grant express waivers?** Both Plus plans ship
   `TrialPeriodDays = 14` and `"trialing"` collapses to `Active` (`UserMembership.cs:124`), so a
   subscriber who signs up on the **28th** can draw **four waivers for 0 Kč** across two `PeriodKey`s
   and cancel before conversion. Linked to `Q-PLUS-01` (an unlimited-trial loop makes the waiver loop
   unlimited too). **The platform cannot tell trialing from paying — there is no trial marker on
   `UserMembership`** — so "waivers begin at first payment" costs an additive column: cheap on
   T-0512's wave, expensive after launch. **Holding position: the seeded plans stay at
   `ExpressUpgradesPerMonth = 0`, so the perk does not turn on until this is answered.** The
   *paying*-member partial-first-month double grant is **accepted** (the owner ruled the calendar
   boundary).
2. **E-2 — do Plus benefits continue during Stripe dunning (`PastDue`)?** `MembershipStatus.cs:18-19`
   says *"Benefits still apply during the grace window"*; `UserMembership.cs:84-85` makes `PastDue`
   inactive. The design ships the **predicate's** answer (no benefits), consistent with every other
   perk. **Whichever way it goes, one of those two files changes in T-0512** — and
   `UserMembership.cs:46-51` must stop saying *"free express upgrade once per period"*.
3. **E-3 — mid-month plan swap.** `ApplyPlanSwap` (`:180-197`) changes the quota while the `PeriodKey`
   stays. Upgrade 2 → 4 is intuitive; downgrade 4 → 2 with 3 consumed is not. **Not decided**; the one
   substantive gap no challenger attacked.
4. **`Q-PLUS-03`** — favourite-cleaner gate; and the other four perks (T-0491 / T-0492 / T-0494 /
   T-0495 / T-0498).

**Engineering follow-ups filed out of this panel:**

5. **E-4 — the live promo path has the same FK-ordering hazard.** `OrderPromoApplier.ApplyAsync`
   (`CreateOrder.cs:315`) inserts a `PromoCodeRedemptions` row under a non-deferrable FK to `Orders`
   **before** the order row is committed (the handler returns before `UnitOfWorkPipelineBehavior`
   commits); its only tests mock the repository. **ADR-0035 does not bless it** — it routes the new
   attach through the UoW precisely to avoid it.
6. **E-5 — two entity configs assert a false invariant.**
   `UserMembershipEntityConfiguration.cs:100-109` and `LoyaltyTransactionEntityConfiguration.cs:84`
   state that this repo does not use `NULLS NOT DISTINCT`. **It does, twice** —
   `FiscalCounterEntityConfiguration.cs:23-29` (tenant-scoped, same sole-arbiter reasoning) and
   `LiveActivityTokenConfiguration.cs:26-28`. Correct both to the **backstop vs. sole-arbiter** rule.
7. **E-6 — the cancellation *fee* keys on `hasBeenAccepted`**, which the Stripe webhook writes, so a
   customer can be charged 50% for an order no cleaner ever took. Ticketed separately; the release rule
   deliberately does **not** copy it.

**Verification debt (preconditions on T-0512, not open questions):**

8. **The reservation statement is unverified against PostgreSQL.** The
   `generate_series` + `NOT EXISTS` + `ORDER BY … LIMIT 1` + `ON CONFLICT DO NOTHING` +
   `RETURNING … AS "Value"` form is *adapted* from a statement known to work, not the statement that
   works. Fallback if it does not compose: an advisory lock or `SELECT … FOR UPDATE` per
   `(user, period)`.
9. **The attach's batch ordering is reasoned, not observed.** That EF places the `Orders` INSERT before
   the dependent usage UPDATE in one `SaveChangesAsync` must be proven by integration test. Fallback:
   the ADR-0002 post-commit seam — **never** the out-of-band attach.
10. **DEV data is still an assumption.** Nobody queried whether `MembershipPlan` has live DEV rows. The
    `0` default is fail-closed, so the risk is bounded — but T-0512 must confirm rather than assume.
    (The *seeded* values are no longer assumptions: `FreeCancellationWindowHours = 4`,
    `TrialPeriodDays = 14`, both verified.)

---

## 6. The generality rule this decision establishes

Recorded here because T-0511 AC5 asked that this and T-0517 (payout details) not answer the same
question differently by accident. They **should** answer differently, and this is why:

> **One table + a discriminator** when the rows have the **same shape** and differ only in **meaning**
> (benefit-usage rows: user, period, ordinal — only *which benefit* varies).
> **A `CountryConfiguration`-driven per-country shape** when the **fields themselves differ**
> (payout details: IBAN vs. a domestic account/bank-code pair vs. a card token).

---

**Cross-links:** `../../backlog/adr/0035-metered-membership-benefit-usage.md` (**accepted** — read its
`## Verdict` for the AM-1…AM-16 index and the E-1…E-6 escalations) · the three challenge files under
`../../backlog/adr/challenges/` · `../../knowledge/roles/membership-benefit-usage.md` ·
`../../knowledge/roles/express-waiver-resolver.md` · `../../knowledge/patterns-backend.md`
(the "Per-user metered entitlements" section lands on acceptance) ·
`../../backlog/adr/0023-per-consumer-claim-ordering-email-claims-after-successful-send.md` (the
claim-ordering rule this reuses) ·
`../../backlog/adr/0009-refund-policy.md` (the frozen-`TotalPrice` principle D1 extends)
