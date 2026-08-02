# Membership benefits — living decision doc

> **Status of this document: TRACKING A `proposed` ADR.** The metered-benefit design below is
> **ADR-0035**, which is `proposed` and **not yet adjudicated** (author only; challengers + lead have
> not run — `agents/process/deliberation.md`). **The "current shape" section is what ships TODAY, and
> today nothing is metered.** The "decided shape" section is what ADR-0035 proposes. When ADR-0035 is
> accepted, the two merge and this banner comes off.

**ADRs:** `../../backlog/adr/0035-metered-membership-benefit-usage.md` (proposed)
**Tickets:** T-0511 (this decision) → T-0512 (schema) → T-0493 (enforcement) → T-0514 (clients);
T-0513 (copy, independent)
**Open owner questions:** `Q-PLUS-02` (partially answered 2026-08-02 — see below), `Q-PLUS-03`

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
| — | **Which time zone is "the 1st"?** | **not asked.** ADR-0035 D2 defaults to the **order country's** `CountryConfiguration.TimeZoneId` (UTC fallback). Non-blocking — both answers use the same schema |
| — | `Q-PLUS-03` favourite cleaner: universal or Plus-only? | **open**, deliberately not defaulted |

---

## 3. The decided shape (ADR-0035, proposed)

### 3.1 The trade-off space, and where each axis landed

| Axis | Options | Landed on | The thing that decided it |
|---|---|---|---|
| **Storage** | derive from `Order` rows · counter column on `UserMembership` · a ledger table | **ledger table** (`MembershipBenefitUsage`) | Derivation has no DB arbiter (the S7 race the promo path already had to fix) **and re-computes history** when a `BookingPolicy` constant is tuned. A counter has no audit trail and no idempotent reversal. |
| **Counted unit** | express order created · express order completed · **the waiver granted** | **the waiver granted** | A booking that would never have carried a surcharge (the 9-hour "same-day" case) must cost nothing. Price and consume must happen in one transaction or they drift. |
| **Period** | `(Start,End)` pair · rolling 30 days · **stored key** | **stored `PeriodKey`** (`"C:2026-08"`) | Survives calendar **and** billing-anchored with no migration; a stored key cannot be retroactively moved by a zone/DST/config change. |
| **Time zone** | customer's (`X-Time-Zone`) · UTC · **country's** | **country's** `CountryConfiguration.TimeZoneId`, UTC fallback | The header is client-supplied and unauthenticated — a member could straddle the boundary and draw 4 credits from 2 months. Bare UTC makes "the 1st" wrong for the first 1–2 hours of every month in CET. |
| **Concurrency** | app-level check · optimistic version · **filtered partial unique index + one atomic statement** | **the index + one statement** | The `PromoCodeRedemption` archetype, already in production, already race-fixed. The DB is the arbiter; a full quota returns `null`, never an exception at the order's commit. |
| **Ordering** | reserve-then-price · price-then-reserve | **reserve-then-price** (ADR-0023 Mode A) | The repeatable-effect test: a duplicate grant is money-shaped. There is no path where a price is waived without a committed slot. |
| **Reversal** | always release · never release · **conditional** | **release iff the booking was never consumed by the customer** (`!hasBeenAccepted`, or `CancelledBy != Customer`, or orphan) | Always-release enables an unbounded supply attack (waive → cleaner accepts → free cancel → repeat, yanking cleaners onto 2-hour-notice jobs). Never-release charges the member's perk for **our** failures. |
| **Generality** | column per benefit · table per benefit · **one table + `BenefitKind`** | **one table + discriminator** | A second metered benefit costs an enum value + a plan column + a resolver; the index, the statement, the release path and the sweep are reused. |

### 3.2 The shape in one picture

```
                         PURE READ (safe everywhere)
QuoteOrder ───────┐
CreateOrder.Val ──┼──▶ IExpressWaiverResolver.ResolveForUserAsync(userId, countryId, cleaningUtc, now)
OrderPricingCalc ─┘         │  reads: UserMembership (the ONE predicate) + MembershipPlan
                            │         + live slot count for PeriodKey
                            ▼
                     ExpressWaiver(Waived, Remaining, Quota, PeriodKey)
                            │
                            │   CONSUMING — exactly one call site, in OrderFactory
                            ▼
        IMembershipBenefitUsageRepository.TryReserveBenefitSlotAsync(...)   →  row | null
                            │   (one atomic INSERT…SELECT…HAVING…ON CONFLICT DO NOTHING RETURNING)
                            ▼
        BookingPolicy.RequiresExpressSurcharge(cleaningUtc, now, waiverApplies: reserved != null)
                            ▼
                 Order.Create(..., finalTotalPrice, ...)   ← price is frozen here, forever
                            ▼
                 AttachOrderAsync(usageId, order.Id)       ← the row stops being an orphan
```

### 3.3 The entity, in one line each

`MembershipBenefitUsage : Auditable, ITenantEntity` —
`UserId` · `BenefitKind` (int enum, never reorder) · `PeriodKey` (string, 64) · `SlotOrdinal` (0-based,
derived in SQL) · `OrderId?` · `UserMembershipId` · `ReservedAtUtc` · `IsActive` (live = consumed;
false = released).

**Index:** `UNIQUE (TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal) WHERE IsActive` — a
**filtered partial** unique index, the shape `UserMembership` already uses (`UserMembership.cs:14-20`).
The filter is what lets a release free the ordinal.

**Plan column:** `MembershipPlan.ExpressUpgradesPerMonth` (int, default `0` = off), sibling to
`FreeCancellationWindowHours`. `AllowsExpressUpgrade == false` ⇒ never, regardless. **Unlimited is
deliberately not expressible** (D2.1).

### 3.4 The reversal table (the part that gets forgotten)

| What happened | Slot released? |
|---|---|
| Customer cancels, **no cleaner had accepted** | **yes** |
| Customer cancels, **a cleaner had accepted** | **no** ← the accepted exploit |
| `CancelledBy.Cleaner` / `.Admin` / `.System` | **yes** |
| Refund on a completed order | **no** |
| Orphan (`OrderId IS NULL` > 1h) | **yes**, by the existing stale-order sweep |

**Accepted exploit:** a member who cancels a real, accepted express booking for a legitimate reason
loses the credit. **Bounded** by the fee schedule — an express booking is 2–4h out, so a customer
cancel of an accepted one is inside `PartialCancellationHours` and pays
`LastMinuteCancellationFeeRate = 0.50`.

> ⚠️ **The bound has a data dependency.** `MembershipPlan.FreeCancellationWindowHours` **must** be
> seeded **> `BookingPolicy.StandardLeadTimeHours` (4)**. A *smaller* value is *more* generous
> (`BookingPolicy.cs:106-110`), so seeding it at 2 makes an accepted express booking free to cancel and
> the farming loop becomes free. ADR-0035 CH-8 flags "enforce this in a validator, not a comment" as a
> candidate blocking amendment.

### 3.5 The client contract

| Response | New field | For |
|---|---|---|
| `GetMyMembership.Response` | `int? ExpressUpgradesPerMonth` | the plan's quota |
| `GetMyMembership.Response` | `int? ExpressUpgradesRemaining` | "1 free express left this month" |
| `QuoteOrder.Response` | `bool ExpressSurchargeWaivedByMembership` | disambiguates "waived" from "not an express slot" — without it `ExpressSurchargeApplied: false` is ambiguous |
| `QuoteOrder.Response` | `int? ExpressUpgradesRemaining` | so the wizard needn't re-fetch the membership |

**No new endpoint.** **No blocked booking** — an exhausted member is *told* and charged the standard
surcharge; a quota loss is a price, never an error. **The client never counts** — the first release
(§3.4) would make it disagree with the server.

---

## 4. Copy — the state, and the sequencing rule

**Three clients advertise three different express perks, and none matches the mechanic.**

| Surface | Promise | Reality |
|---|---|---|
| Android `values/strings.xml:844` + 4 locales | *"One free same-day booking per month, no surcharge."* | metered 1/month — **wrong number** (owner said 2) **and wrong word** |
| iOS `Localizable.xcstrings:14121` + 4 locales | identical | same |
| Web `cleansia.app en.json:1095` | *"Pay less for last-minute bookings inside the express window."* | **uncapped discount** — a different product |
| `BookingPolicy.cs:18-30` | express = **2–4h lead**, +20% | **not "same-day" at all** |

**The sequencing rule (ADR-0035 §Copy):**

- **Corrective half ships NOW, ahead of the implementation** — delete *"same-day"*, delete the web
  client's uncapped claim. *"Same-day"* is false **against** the customer in the case that matters (a
  09:00-for-12:00 booking **is** express and **is** charged +20% after being told it was free). That is
  a live misrepresentation on a paid subscription and removing it needs no backend at all.
- **Affirmative half ships WITH T-0493** — *"Two free express bookings each calendar month"* must not
  appear before anything actually waives anything.
- **"one" → "two" is low urgency** — false in the customer's *favour*; nobody is wronged by receiving
  two when told one. Ride the same pass, but never let it hold up the corrective half.

**Constraints on T-0513's canonical sentence:** name the 2–4h lead window (not "same-day"); say **"each
calendar month"**; state the cap as **2** from `ExpressUpgradesPerMonth`; do not promise that a
cancelled booking returns the credit.

Anchor (analyst owns the final wording):
> *"Two free express bookings each calendar month — we waive the 20% surcharge on cleanings booked
> 2 to 4 hours ahead."*

---

## 5. Known gaps in this design (open, not decided)

1. **Mid-month plan swap.** `UserMembership.ApplyPlanSwap` (`:180-197`) changes `MembershipPlanId`
   mid-period while the `PeriodKey` stays the same — so the quota changes under already-consumed slots.
   **ADR-0035 does not decide this.** Upgrade (2 → 4) is intuitive (already-used slots still count);
   downgrade (4 → 2) with 3 already used is not. Needs a ruling.
2. **The reservation SQL is unverified against PostgreSQL.** It *adapts* a statement known to work
   (`PromoCodeRedemptionRepository.cs:60-74`, which uses `MAX(…)+1`) into a `COUNT(*) FILTER` form.
   T-0512 must prove it with an integration test; if it does not compose, the *guarantee* still holds
   and the fallback is an advisory lock or `SELECT … FOR UPDATE` on a per-(user, period) row.
3. **DEV data is an assumption.** Nobody queried whether `MembershipPlan` has live DEV rows or what
   `FreeCancellationWindowHours` they carry. The `0` default is fail-closed (perk stays off), so the
   risk is bounded — but T-0512 AC6 must confirm rather than assume.
4. **Cancel-time warning.** Should the cancel sheet say *"this will use up your free express
   booking"*? ADR-0035 CH-4 raises it; it is a T-0514 decision this ADR's rule creates the need for.
5. **The other four perks.** T-0491 / T-0492 / T-0494 / T-0495 / T-0498; `Q-PLUS-03` for the
   favourite-cleaner gate.

---

## 6. The generality rule this decision establishes

Recorded here because T-0511 AC5 asked that this and T-0517 (payout details) not answer the same
question differently by accident. They **should** answer differently, and this is why:

> **One table + a discriminator** when the rows have the **same shape** and differ only in **meaning**
> (benefit-usage rows: user, period, ordinal — only *which benefit* varies).
> **A `CountryConfiguration`-driven per-country shape** when the **fields themselves differ**
> (payout details: IBAN vs. a domestic account/bank-code pair vs. a card token).

---

**Cross-links:** `../../backlog/adr/0035-metered-membership-benefit-usage.md` ·
`../../knowledge/roles/membership-benefit-usage.md` ·
`../../knowledge/roles/express-waiver-resolver.md` · `../../knowledge/patterns-backend.md`
(the "Per-user metered entitlements" section lands on acceptance) ·
`../../backlog/adr/0023-per-consumer-claim-ordering-email-claims-after-successful-send.md` (the
claim-ordering rule this reuses) ·
`../../backlog/adr/0009-refund-policy.md` (the frozen-`TotalPrice` principle D1 extends)
