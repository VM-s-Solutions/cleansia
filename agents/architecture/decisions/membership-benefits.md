# Membership benefits — living decision doc

> **Status of this document: ADR-0035 is `accepted` (adjudicated 2026-08-02, with 16 binding
> amendments) and AMENDED BY OWNER INSTRUCTION 2026-08-03 (AM-17, AM-18, AM-19).** The panel is
> complete — author + three independent challengers + lead. **§3 below is the AMENDED shape**, which
> differs from the ADR's draft in seven mechanisms; several diagrams and tables in earlier revisions of
> this file described the draft and were wrong. **§1 remains what ships TODAY: nothing is metered yet**
> — T-0512/T-0493 have not landed. This banner comes off when T-0493 ships.
>
> **2026-08-03 — three of the six escalations are CLOSED by owner ruling.** **E-1** (the trial),
> **E-2** (`PastDue`) and **E-3** (the mid-month plan swap) are answered and binding. See **§2** for the
> rulings and **§5** for what remains. The ADR's §Verdict escalation table is discharged for those
> three; **E-4, E-5 and E-6 are engineering follow-ups and are still open.**

**ADRs:** `../../backlog/adr/0035-metered-membership-benefit-usage.md` (**accepted**, immutable;
carries a dated 2026-08-03 owner-instruction amendment at the end)
**Tickets:** T-0511 (this decision) → T-0512 (schema) → T-0493 (enforcement) → T-0514 (clients);
T-0513 (copy — **corrective half already shipped**); **P-1 / P-2 / P-3** to be filed (§5)
**Open owner questions:** `Q-PLUS-02` (partially answered 2026-08-02), `Q-PLUS-03`, `Q-PLUS-01`
(the trial loop — **narrowed but NOT closed** by the 2026-08-03 ruling, see §5), plus escalations
**E-4 … E-6** in the ADR's §Verdict. **E-1 / E-2 / E-3 are closed.**

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

Current consumers: `CancellationPolicyResolver.cs:33`, `GetMyMembership.cs:35`, `OrderFactory.cs:76-77`,
`QuoteOrder.cs:142`, `CreateRecurringBooking.cs:84-85`. **A `PastDue` or `Paused` membership is not
active** — benefits stop, by the predicate, with no per-feature rule.

> ✅ **SETTLED 2026-08-03 by owner ruling (E-2 / `Q-PLUS-05`): `PastDue` keeps NO benefits. Cut
> everything on the first payment failure. No grace window.** The contradiction is closed **against the
> enum**: `ActiveForUserQuery` and `UserMembership.IsActive` are unchanged, and
> `MembershipStatus.cs:18-19`'s comment has been corrected to say benefits stop immediately. **"PastDue
> gets nothing" is now a decided rule** and may be relied on.

> ⚠️ **"One predicate" is true of the BENEFIT paths and false of the codebase.**
> `SendMembershipLifecycleNotifications.cs:77` and `:116` write `m.Status == MembershipStatus.Active`
> **inline** — a second, hand-written expression of the same condition. It is the renewal /
> cancellation-reminder sweep, not a benefit resolver, and it happens to agree. **Do not add a third.**
> Its agreement has a consequence: a `PastDue` member is skipped by the reminder sweep too (§5, P-1).

**The four consequences of the `PastDue` ruling, recorded because the owner accepted them knowingly:**

| # | What happens to a `PastDue` member | Where | Disposition |
|---|---|---|---|
| C-1 | **The app says they have no membership at all.** `GetMyMembership` uses the same predicate ⇒ `HasMembership: false`, every field null; `Response.Status` can never carry `PastDue` on the wire. They see the *subscribe* upsell. **No surface anywhere says "your card failed".** | `GetMyMembership.cs:35-51` | **P-1** |
| C-2 | **A booking is hard-rejected** (`PreferredEmployeeMembershipRequired`, ADR-0036 D7) before they know anything is wrong. | ADR-0036 D7 | **Accepted** — the headline trade. Makes D7's "the error must name the tap" a requirement. |
| C-3 | **The renewal reminder is suppressed** at the moment it is most useful. | `SendMembershipLifecycleNotifications.cs:77` | **P-1** (same missing message) |
| C-4 | **They can start a SECOND subscription while the first is in dunning.** Both app guards use `GetActiveForUserAsync` (null) and the DB backstop is `HasFilter("\"Status\" = 1")` — neither sees the PastDue row. | `CreateMembershipSubscription.cs:86`; `UserMembershipEntityConfiguration.cs:112-114` | **P-2** — pre-existing in code, **made reachable** by the ruling |

### The trial — a second, per-benefit conjunct (2026-08-03)

**The trial is NOT in the shared predicate and must never be put there.** `"trialing"` maps to `Active`
(`UserMembership.cs:124`) and the owner ruled the trial **keeps** the discount and the cancellation
window. Only the **metered** benefit is withheld. So:

```
ActiveForUserQuery          → "is there a live membership?"      (shared, unchanged)
  + IExpressWaiverResolver  → "&& !membership.IsInTrial"         (ONE resolver, ONE benefit)
```

Harmonizing `IsInTrial` into `ActiveForUserQuery` would silently strip the two benefits the owner
preserved. This is the one sanctioned per-benefit narrowing; a second one needs an ADR.

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
| **2026-08-03** | **E-1 — does the free trial grant waivers?** | **NO.** *"No express waivers during the 14-day trial."* The trial **keeps** the discount and the cancellation window; **metered waivers begin when they pay.** ⇒ needs a new field (§3.6) |
| **2026-08-03** | **E-2 / `Q-PLUS-05` — do benefits continue during `PastDue`?** | **NO.** *"PastDue keeps NO benefits. Cut everything on first payment failure."* **No grace window.** ⇒ predicate unchanged; the enum comment corrected |
| **2026-08-03** | **E-3 — mid-month plan switch and the counter** | **The counter CARRIES.** *"1 used on monthly, switch to yearly, 1 remaining."* **The quota belongs to the calendar month, not the plan.** ⇒ §3.7 |
| **2026-08-03** | **`Q-PLUS-04` — does a lapse stop a recurring schedule?** | **NO.** Occurrences keep being generated, at **full non-member price**, and the **customer is notified of the price change.** ⇒ `preferred-cleaner-dispatch.md`; ticket **P-3** |
| — | `Q-PLUS-03` favourite cleaner: universal or Plus-only? | **ANSWERED 2026-08-02: Plus-only** (ADR-0036 D7) |

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
deliberately not expressible** (D2.1). ~~The `0` default is also the holding position for E-1.~~
**E-1 is answered (2026-08-03), so the holding position is discharged and the seed value may be set —
but only in a wave that also ships `UserMembership.TrialEndsAtUtc` (§3.6). Setting the seed without the
field re-opens the four-waivers-for-0-Kč trial loop AM-14 named.**

### 3.6 The trial marker — `UserMembership.TrialEndsAtUtc` (NEW, owner ruling 2026-08-03)

The ruling *"no express waivers during the trial"* was **not expressible**: `"trialing"` collapses to
`Active` at `UserMembership.cs:124` and the entity carries no trial marker.

| | |
|---|---|
| **Field** | `DateTime? TrialEndsAtUtc` on `UserMembership` — mirrored from Stripe's `trial_end` |
| **Derived** | `bool IsInTrial => TrialEndsAtUtc != null && DateTime.UtcNow < TrialEndsAtUtc` |
| **Why an instant, not a `bool`** | a boolean needs a **writer** to flip it on conversion and there is no sweep — it goes stale and grants waivers forever. A stored deadline **expires by clock, no actor**. Same argument as `Order.PreferredHoldUntilUtc` (ADR-0036 D2). |
| **Additive?** | **yes** — nullable, no backfill, no index. Existing rows read `null` ⇒ not trialing ⇒ **fail-open**, which is safe **only** because the DB is being dropped and `Initial` regenerated. On a live DB this default would be wrong. |
| **Migration** | ⚠️ **owner-only `ef-migration`. BATCH it into the regenerated `Initial`** — do not stack (see §3.8) |
| **Fed by** | (1) `SubscriptionResult` (`IStripeClient.cs:208-211`) gains a 4th nullable field → `UserMembership.Create`; (2) `StripeSubscriptionWebhookHandler.ExtractSubscriptionShape` (`:75-101`) gains a 5th tuple element |
| ⚠️ **The trap** | the `invoice.payment_failed` branch (`:78-89`) returns `default` bounds and the handler **passes existing values through** (`:63-64`). `trial_end` must get the same treatment. Writing `null` there clears the marker for a trialing member whose first invoice failed — **re-enabling waivers for exactly the customer the ruling is about.** |
| **Enforced in** | `IExpressWaiverResolver` only — **one extra conjunct**, never in the shared predicate (§1) |
| **Read contract** | during the trial `ExpressUpgradesRemaining = **0**` (they *do* have a membership — `null` would call them a non-member), plus a new `GetMyMembership.Response.TrialEndsAtUtc` so the client says *"your free express bookings start on {date}"*. ⚠️ `nswag-regen` → **T-0514** |

### 3.7 The quota key — calendar month, never the membership row (owner ruling 2026-08-03, E-3)

**What a plan switch actually does, established by reading:** `SwapMembershipPlan.Handler` loads the
live row (`:39`) and calls `ApplyPlanSwap` (`:78-81`), which **mutates it in place** —
`UserMembership.Id` and `StripeSubscriptionId` untouched, **no new row**
(`UserMembership.cs:180-197`). The challenger's "re-subscribing creates a new row" finding is correct
but describes a **different** path (`UserMembership.Create` at
`StripeSubscriptionWebhookHandler.cs:174` / `CreateMembershipSubscription.cs:168`, legal because the
unique index is filtered `WHERE "Status" = 1`).

> **BINDING — the counting key is `(TenantId, UserId, BenefitKind, PeriodKey)` + `IsActive`.**
> **`UserMembershipId` MUST NOT appear in any `WHERE`, `GROUP BY`, `HAVING` or join on a counting
> path.** It is written once at reservation and read only by a human. The index and the reservation
> statement already comply; **the risk is entirely in the read path** — the resolver has the membership
> row in hand and scoping the count to it looks *more* correct than it is. That is the quiet violation.
> A reviewer greps for `UserMembershipId`: it may appear **only** in the reservation `INSERT` column
> list.

**Consequence the owner did not name, and it is the right one:** the same key governs **churn** —
cancel-and-resubscribe mid-month does **not** grant a fresh quota either. This closes the loop that
killed the counter-column option in §3.1 ("churn resets the quota").

**Upgrade / downgrade falls out with no new rule.** `Remaining = max(0, currentPlan.ExpressUpgrades
PerMonth − liveSlotsInPeriodKey)`. Upgrade 2→4 with 2 used ⇒ 2 left. Downgrade 4→2 with 3 used ⇒ 0 left,
and the 3 granted waivers are **not clawed back** (the ADR-0009 D2 freeze).

> ⚠️ **The ruling REINSTATES a guard AM-5 deleted.** AM-5 struck the `HAVING` guard as redundant
> because *"a full quota yields zero candidate rows"* — true **only while the quota is invariant across
> a `PeriodKey`**, which is exactly what a mid-month swap breaks. Downgrade 4→2 with live `{0,1,2}`,
> then release ordinal 0 ⇒ `generate_series(0,1)` finds ordinal 0 free ⇒ **a 4th waiver is granted on a
> quota-2 plan**, while the read path says 0 remaining. **Fix:** an independent **cardinality bound**
> (`live count in period < @maxPerPeriod`) inside the same statement. This is *not* the deleted
> `HAVING` — the smallest-free-ordinal derivation is untouched. Pinning test: **`TC-BENEFIT-DOWNGRADE-0`**.
> `Remaining` counts live rows **in the period**, not live rows with ordinal `< quota`, or the two sides
> disagree from the other direction.

### 3.8 Pending schema wave — batch, do not stack

⚠️ **All owner-only `ef-migration`.** The owner is dropping the DB and regenerating the single `Initial`
migration; every row below folds into that regeneration.

| Change | Source | Ticket |
|---|---|---|
| `MembershipBenefitUsage` + its three indexes | ADR-0035 D3 | T-0512 |
| `MembershipPlan.ExpressUpgradesPerMonth` (int, default 0) | ADR-0035 D2.1 | T-0512 |
| **`UserMembership.TrialEndsAtUtc` (`DateTime?`)** | **AM-18 (2026-08-03)** | T-0512 |
| `Order.PreferredHoldUntilUtc` (`DateTime?`) | ADR-0036 D2 | T-0515 |
| `RecurringBookingTemplate.PreferredEmployeeId` (`string?`, 26) | ADR-0036 D8 | C3 |

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

**Owner decisions — E-1, E-2 and E-3 are CLOSED (2026-08-03). Recorded here for the trail:**

1. ~~**E-1 — does the 14-day free trial grant express waivers?**~~ ✅ **ANSWERED 2026-08-03: NO.**
   *The original escalation, preserved:* both Plus plans ship `TrialPeriodDays = 14` and `"trialing"`
   collapses to `Active` (`UserMembership.cs:124`), so a subscriber who signs up on the **28th** could
   draw **four waivers for 0 Kč** across two `PeriodKey`s and cancel before conversion; and the platform
   **could not tell trialing from paying**. **Ruling:** the trial keeps the discount and the
   cancellation window; **metered waivers begin at first payment.** Costs the additive
   `UserMembership.TrialEndsAtUtc` column (§3.6) — **batched into the `Initial` regeneration** (§3.8).
   The holding position (`ExpressUpgradesPerMonth = 0`) is discharged, *conditional on the field
   shipping in the same wave*. The *paying*-member partial-first-month double grant remains **accepted**.
   ⚠️ **`Q-PLUS-01` is NARROWED, not closed** — this removes the express-waiver leg of the unlimited-trial
   loop; a repeatable trial still yields repeatable **discount** and **cancellation-window** benefits.
2. ~~**E-2 — do Plus benefits continue during Stripe dunning (`PastDue`)?**~~ ✅ **ANSWERED 2026-08-03:
   NO — cut everything on the first payment failure, no grace window.** *The original escalation,
   preserved:* `MembershipStatus.cs:18-19` said *"Benefits still apply during the grace window"* while
   `UserMembership.cs:84-85` made `PastDue` inactive. **The contradiction is closed against the enum** —
   the predicate is unchanged and the comment has been corrected. `UserMembership.cs:46-51` **still**
   needs its correction (the "grace window" phrase **and** *"free express upgrade once per period"* — the
   owner ruled **two per calendar month**); that stays on T-0512. See §1 for the four accepted
   consequences (C-1…C-4) and P-1/P-2 below.
3. ~~**E-3 — mid-month plan swap.**~~ ✅ **ANSWERED 2026-08-03: the counter carries; the quota belongs to
   the calendar month, not the plan.** *The original escalation, preserved:* `ApplyPlanSwap` (`:180-197`)
   changes the quota while the `PeriodKey` stays — upgrade 2→4 intuitive, downgrade 4→2 with 3 consumed
   not. **Resolved in §3.7**, which also establishes that a swap **mutates the row in place** (no new
   row) and that the ruling **reinstates a cardinality guard AM-5 deleted**.
4. **`Q-PLUS-03`** — ✅ **answered 2026-08-02: Plus-only** (ADR-0036 D7). The other four perks
   (T-0491 / T-0492 / T-0494 / T-0495 / T-0498) are unaffected.

**Tickets this ruling wave creates (for the PM to file — the architect does not edit `tickets/**`):**

- **P-1 — nothing tells a `PastDue` customer their card failed.** `GetMyMembership` returns
  `HasMembership: false` (so `Response.Status` can never carry `PastDue`), **and** the renewal-reminder
  sweep skips them (`SendMembershipLifecycleNotifications.cs:77`). A paying subscriber mid-dunning sees
  the *subscribe* upsell while losing four benefits. Needs a read surface + a message. **Highest-value
  item in this wave**; it is what makes the accepted trade survivable.
- **P-2 — a `PastDue` member can start a second subscription.** Both app guards
  (`CreateMembershipSubscription.cs:86`, `CreateMembershipCheckoutSession.cs:55`) and the DB backstop
  (`HasFilter("\"Status\" = 1")`) key on `Active`. Two live Stripe subscriptions, one customer.
  Pre-existing in code; **made reachable** by the ruling.
- **P-3 — the recurring price-change notification** (owned by `preferred-cleaner-dispatch.md`;
  `Q-PLUS-04`). Does not exist today. **One per price transition, both directions.**

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
