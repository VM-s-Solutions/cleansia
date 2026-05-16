# LOY-003 — Loyalty Tier Restructure [SHIPPED — Option C with uniform 1000 CZK floor]

> **Outcome:** Option C chosen. Plus + tier additive, capped at 12%. Uniform 1000 CZK floor on the tier portion (Plus always applies). See [post-android-followups.md "LOY-003 (shipped)"](post-android-followups.md) for the line-by-line implementation summary. Doc kept for historical context of the decision.

---

# LOY-003 — Loyalty Tier Restructure: Decision Doc

> **Purpose:** four candidate shapes for how Cleansia's loyalty tiers + Plus
> membership should interact. Pick one, then a small implementation kicks off
> (~3 days code + SQL migration). Until a decision lands, no code work.
>
> **Audience:** product / founder. Engineering decisions are downstream and
> small.

---

## Today's state (the baseline)

### Loyalty tiers (per `LoyaltyTierConfigs` seed)

| Tier | Lifetime points to unlock | Discount % | Min order for discount | Other perks today |
|---|---:|---:|---:|---|
| Bronze | 0 | 0 % | — | "Welcome" badge |
| Silver | 500 | 5 % | **1000 CZK** | Discount above floor |
| Gold | 2000 | 10 % | — | Priority support badge |
| Platinum | 5000 | 15 % | — | Priority support + "dedicated pool" badge |

Points earned per order: roughly 1 point per 1 CZK spent (verify with current `PointsAwardedForOrder` logic if exact rate matters).

### Cleansia Plus membership (per `MembershipPlans` seed)

| | Monthly | Yearly |
|---|---|---|
| Price | 199 CZK/mo | 2030 CZK/yr (≈169/mo, –15%) |
| Discount % | **5 %** | **5 %** |
| Free-cancellation window | 4 h | 4 h |
| Free express upgrade | Yes (skips +20% surcharge) | Yes |
| Trial | 14 days | 14 days |

### Discount stacking — **today's rule is "best-of-three, no stacking"**

When an order is created, [`OrderFactory.ResolveBestDiscount`](src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs) picks the **largest single** of:
- Tier discount (whatever the user's tier qualifies for)
- Cleansia Plus discount (5 % flat if active)
- Promo discount (if user typed a valid code)

Ties favor: membership → promo → tier (in that order). Only ONE source ever applies to a given order.

### Bottom-line examples of today's behavior

| User profile | Order subtotal | What applies | What user pays before surcharge |
|---|---:|---|---:|
| Bronze (new) + No Plus | 1500 | nothing | 1500 |
| Silver + No Plus | 1500 | tier 5 % = 75 | 1425 |
| Silver + Plus | 1500 | tie 75 vs 75 → **Plus wins** (membership 5%) | 1425 |
| Gold + Plus | 1500 | tier 10 % = 150 **vs** Plus 75 → tier wins | 1350 |
| Platinum + Plus | 1500 | tier 15 % = 225 **vs** Plus 75 → tier wins | 1275 |
| Bronze + Plus | 800 | Plus 5 % = 40 (tier 0 below floor) | 760 |
| Silver + Plus | 900 (below floor) | Plus 5 % = 45 (tier blocked by floor) | 855 |

### Today's pain points

1. **"Plus loses its value at Gold+"** — once a user reaches Gold, the 10% tier wipes out the 5% Plus benefit. From the user's perspective, paying 199 CZK/mo for Plus stops feeling worth it. This is the main grievance behind LOY-003.
2. **Silver's 1000 CZK floor is invisible** — most one-room bookings sit under 1000 CZK. Users in Silver tier feel the tier "did nothing" for them. Surfaced now via the LOY-005 tier-floor hint, but the underlying floor is still there.
3. **Generous on the high end** — Platinum 15% is significant margin. The business case: high-LTV customers, retention focus. The risk: race-to-zero competitors.
4. **Plus pricing assumes the 5% discount** — at 199 CZK/mo, a Plus user needs to book ~3980 CZK/mo just to break even on the discount alone. The 4h cancel + free express upgrade are the real value props. Communicating that clearly is hard while the marketing leads with "5% off."

---

## Decision drivers

Before evaluating options, agree on which of these you optimize for:

| Driver | What it means |
|---|---|
| **Total discount given** | Lower = better margin. Today: 0/5/10/15 % depending on tier. |
| **Plus retention** | The Plus subscription is recurring revenue. The benefit has to feel valuable forever, not "until I hit Gold." |
| **Tier aspiration** | Tiers exist to make bookings feel like progress. Too small a gap between tiers → no progression feel. Too big → cliff effect at unlock. |
| **Operational simplicity** | Fewer rules = fewer support tickets, fewer mistakes, easier to communicate. |
| **Convertibility (free → Plus)** | Plus has to feel like a genuine upgrade over free, not a tax. |

My honest take: drivers #2 (Plus retention) and #5 (free→Plus conversion) are the strongest signals that the current shape is broken. Driver #1 (margin) is the constraint, not the goal.

---

## Option A — Status quo, tighten the floors

**Shape:** keep today's structure. Drop the Silver 1000 CZK floor (it's the source of the "tier didn't apply" complaints). Optionally drop Platinum from 15 % → 12 %.

| Tier | Discount | Floor |
|---|---:|---|
| Bronze | 0 % | — |
| Silver | 5 % | None |
| Gold | 10 % | None |
| Platinum | 12 % | None |

**Plus stays at 5 %, best-of-three rule unchanged.**

### Pros
- **Smallest change** — 1-day SQL migration + Plus marketing copy refresh. No code logic changes.
- Solves driver #2 only partially: Silver+Plus is now still a tie (5 vs 5), but at least Silver users see the discount.
- Fixes the LOY-005 hint surface (no more "needs orders above 1000 CZK" displayed).

### Cons
- **Doesn't solve Plus-at-Gold+** — the core grievance remains. Gold/Platinum users still feel Plus is wasted.
- Looks like "we adjusted the numbers a bit" — no narrative.

### Best fit if
You're not ready to redesign the tier system; you just want to stop bleeding on the floor complaint. Plan for a real restructure later.

### Effort
~1 day. Pure data migration. No code.

---

## Option B — Flat 3/5/7 % + non-discount perks per tier

**Shape:** tiers give smaller discounts but unlock real perks. Plus becomes the "premium experience" layer.

| Tier | Threshold | Discount | Perks |
|---|---:|---:|---|
| Bronze | 0 | 0 % | Basic notifications, welcome badge |
| Silver | 500 | 3 % | Priority support (response 24h → 4h) |
| Gold | 2000 | 5 % | Free reschedule once per booking, +50 bonus points/month |
| Platinum | 5000 | 7 % | Dedicated favorite-cleaner pool, free recurring booking templates |

**Plus stays at 5 % discount, AND it adds:**
- 4h free cancel (today)
- Free express upgrade (today)
- New: extended free reschedule window (Plus = 12h vs free = none)
- New: "Plus" badge on all Plus-user reviews (social signal)

**Best-of-three rule unchanged.** But the math now works in Plus's favor at Bronze/Silver (Plus 5 wins over tier 0/3), ties at Gold (5=5), and Plus loses at Platinum (5 vs 7).

### Pros
- **Solves Plus-at-Gold problem** — Plus wins or ties everywhere except Platinum. Even at Platinum the user wanted Plus for the *features* (cancel window, reschedule, express upgrade), not the discount.
- **Tiers feel meaningful** — Silver's priority support is a real benefit. Gold's free reschedule is a real benefit.
- **Lower total discount given** — Platinum capped at 7% vs today's 15%. Significant margin win.
- **Narrative is clean** — "tiers reward loyalty with service, Plus rewards subscription with features." Fits the existing `notes/loy-005-followups` direction.

### Cons
- **Perk infrastructure doesn't exist today** — "priority support" needs an oncall queue priority signal. "Free reschedule once per booking" needs a reschedule API + UI (doesn't exist). "Dedicated cleaner pool" needs cleaner-pool segmentation (doesn't exist).
- **Roll-out is gated by feature buildout** — you can ship the tier discount values immediately but the perks land over a series of follow-up PRs.
- Discount-only users feel the cut: a Platinum user going from 15% → 7% sees their per-order saving halve. Risk of churn for high-engagement existing users.

### Best fit if
You believe Plus should be a feature subscription, not a discount subscription, AND you have product runway to actually build the perks. If perks won't ship for 6 months, customers will see "Silver = 3% only" with no priority support visible, and it'll feel like a downgrade.

### Effort
- Day 1: SQL migration for tier % values. Per-tier `PerksJson` updates. Mobile + web copy/i18n for perk descriptions.
- Week 2-N: implement each perk as its own task (priority support routing, reschedule flow, cleaner pool segmentation, recurring template caps).
- The "ship the new numbers" part is ~1 day; the "make perks real" part is months.

---

## Option C — Additive Plus + tier with cap (12% combined)

**Shape:** Plus and tier *stack* additively up to a cap, instead of competing.

| Tier | Discount | + Plus discount | Combined (capped at 12%) |
|---|---:|---:|---:|
| Bronze | 0 % | +5 % | 5 % |
| Silver | 5 % | +5 % | 10 % |
| Gold | 10 % | +5 % | 12 % (capped) |
| Platinum | 12 % | +5 % | 12 % (capped) |

Promo replaces this combined value if larger; otherwise the combined value applies.

### Pros
- **Plus always adds value** — even Platinum users see +5% from Plus until they hit the cap. Conversion + retention both improve.
- **Margin protected by the cap** — never above 12% total. Platinum on its own drops from 15% to 12% (a 3-point margin gain even before Plus).
- **Easy to explain** — "you get your tier discount + an extra 5% with Plus, up to 12% off." Tagline-able.
- **No new perks needed** — the math itself is the value prop. Ship in ~3 days, no roadmap dependency.

### Cons
- **Total discount given is HIGHER than today on average** — a Silver+Plus user goes from 5% (today, best-of-three) to 10% (additive). A Bronze+Plus user goes from 5% (today, Plus wins) to 5% (same). The cap helps at the top but the middle gets cheaper for us.
- **Cap is invisible at booking time today** — needs UI work to communicate why a Plus+Platinum user only gets 12% not 17%. Could feel like a bait-and-switch unless surfaced clearly.
- **Best-of-three logic gets retired** — small but real code change in `OrderFactory.ResolveBestDiscount`. Discount snapshot fields on Order (tier + membership amounts) become "both can be populated simultaneously" rather than the current "at most one is non-null." Mapper + DTO + mobile chip logic all need to handle the both-populated case.

### Best fit if
You want a clear "Plus is always worth it" message AND can absorb a slight margin reduction on the Silver/Gold segments. The cap protects you from runaway combined discounts; the additive structure protects Plus's value at every tier.

### Effort
~3-4 days:
- Day 1-2: backend logic change (best-of-three → additive with cap). Migration of historical Order discount snapshots is NOT needed (existing orders keep their snapshot as-is).
- Day 2: customer mobile + web chip rendering for "Plus + Tier" (today the chips assume exclusivity; need to render both).
- Day 3: copy/i18n × 5 locales, marketing surface refresh.
- Day 4: testing.

---

## Option D — Plus is the only discount; tiers are pure perks

**Shape:** remove tier discounts entirely. Only Plus gives a discount (bumped from 5% → 8%). Tiers give non-monetary perks only.

| Tier | Threshold | Discount | Perks |
|---|---:|---:|---|
| Bronze | 0 | 0 % | Welcome badge |
| Silver | 500 | 0 % | Priority support (24h → 4h) |
| Gold | 2000 | 0 % | Free reschedule once per booking, +50 bonus pts/mo |
| Platinum | 5000 | 0 % | Dedicated cleaner pool, free recurring templates, all of Gold's |

**Plus alone:** 8% discount + 4h cancel + free express upgrade + reschedule extended window. Promo replaces if larger.

### Pros
- **Strongest "subscribe to Plus" signal** — Plus is THE way to get a price discount. No "earn it via tier" path.
- **Maximum margin protection** — discount only flows to paying Plus subscribers. Loyalty becomes pure retention play, not margin leak.
- **Tier complexity drops** — no tie-breaking math, no floor amounts, no per-tier discount %. Just "Plus or not."

### Cons
- **Existing customers feel robbed** — a Platinum non-Plus user goes from 15% discount to 0%. They will churn unless grandfathered.
- **Requires grandfathering policy** — either freeze existing Platinum/Gold users at their current discount until churn, OR offer free Plus to anyone above Silver as a goodwill credit. Either is operational complexity + cost.
- **Same perk-infrastructure dependency as Option B** — tiers without discounts are *just* perks. If the perks aren't real (no priority queue, no reschedule API), Silver/Gold/Platinum feel like cosmetic badges.
- **Plus pricing math** — 8% on average order of ~1500 CZK = 120 CZK saving. Plus monthly = 199 CZK. Customer needs to book 1.7x/month just to break even on discount. Tighter than today.

### Best fit if
You're confident the customer base hasn't ossified on the existing tier discounts (very early stage, low retention risk), AND you have a real Plus-features roadmap. High risk, high reward.

### Effort
- Day 1-2: backend changes (`OrderFactory` drops tier from best-of-three; `LoyaltyService.ResolveTierDiscountForOrderAsync` returns zero always until tier discounts are reintroduced).
- Day 2-3: grandfathering policy (separate stored field per user? feature flag?) — non-trivial design call.
- Week 2-N: perk infrastructure (same as Option B).
- Day N: customer communication about the change (email, in-app banner).

---

## Side-by-side summary

| | A: tighten floors | B: 3/5/7 + perks | C: additive capped 12% | D: Plus-only discount |
|---|---|---|---|---|
| **Pain point fix** | Floors only | Plus value + simplification | Plus value at every tier | Plus value max + simplification |
| **Margin impact** | Slightly worse (Silver floor drop) | Better (caps Platinum) | Worse (additive at mid tiers) | Best (only Plus pays out) |
| **Implementation cost** | ~1 day, SQL only | ~1 day numbers + months of perks | ~3-4 days code+UI | 2 weeks + grandfathering |
| **Risk of churn** | Lowest | Medium (Platinum cut) | Lowest | High without grandfathering |
| **Narrative** | "We tuned the numbers" | "Tiers = service, Plus = features" | "Stack your savings up to 12%" | "Plus = the discount, tiers = the experience" |
| **Plus retention** | Marginal improvement | Strong (Plus wins ~always) | Strongest (Plus always adds) | Strongest (Plus is the only way) |

---

## My recommendation

**Option C** — additive Plus + tier capped at 12%.

Rationale:
- It's the only option that fixes the Plus-at-Gold+ problem **without** requiring a perk-infrastructure roadmap.
- The 12% cap caps your downside while feeling generous from the customer side.
- Ships in ~3-4 days. No customer-facing churn risk from removing tier discounts.
- The narrative is the simplest to communicate ("stack your savings up to 12%").
- The "best-of-three becomes additive-with-cap" code change is small and well-scoped because of where the discount math now lives ([`OrderFactory.ResolveBestDiscount`](src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs) is one method; the chip rendering on customer mobile + web is the bulk of UI work).

**If you want bigger margin protection AND have product runway**, Option B is right. The 3/5/7% structure is generous to your margin without requiring grandfathering, but you signed up for a multi-quarter perk-buildout commitment.

**Avoid Option D** until you have either: a churn-risk-tolerant user base (new launch), OR a tested grandfathering mechanism. The customer-experience risk is too high otherwise.

**Avoid Option A** unless you genuinely have no time for a real change. It's the "ship Friday" version, but you'll need to redo this exercise in 3 months when Plus retention numbers don't move.

---

## Decision required

To unblock implementation, the following need to be locked:

1. **Pick A, B, C, or D** (or a variant I haven't covered — propose it).
2. **For Options C or D:** the exact discount %s (C: tier 0/5/10/12 + Plus 5 cap 12, OR variant. D: Plus % between 6-10).
3. **For Option B:** which perks ship in the v1 SQL migration vs land later. The migration can store `PerksJson` for advertised perks even if implementations come in follow-ups.
4. **Communication plan:** banner in-app? Email blast to existing tier holders? Phased rollout? Grandfathering for early users? Especially critical for D, less so for B/C, irrelevant for A.

Once 1-4 are decided, I'll write the implementation plan as TASK-LOY-003-A through TASK-LOY-003-N and we can run it as a normal wave (~3-4 days for Option C, ~1 day + ongoing for B, ~1 day for A, ~2 weeks for D).
