# Loyalty, credit, referrals and memberships: end-to-end cases

Points, tiers, credit, referrals, Cleansia Plus and the public promo-code request, from `docs/flows/loyalty-and-memberships.md`. Ids `E2E-LOYAL-NNN`; format and DEV facts in `e2e/README.md`.

Reading notes for every case below:

- Most cases start from a completed order or a booking made by the booking, offer, execution and payment cases. They are written as "a completed order of total T" and reuse the order of those cases; they do not create their own cleaner path. Where a case needs money to have moved (a refund), it uses Stripe test mode: 4242 4242 4242 4242 pays; 4000 0000 0000 5126 pays and then fails any refund.
- The CZ market is the default and its currency CZK has `LoyaltyPointsDivisor` 10 (1 point per 10 CZK). Read the live divisor, tier thresholds, discount percentages and plan figures from the API (`Loyalty/GetTiers`, `Membership/GetPlans`, the admin currency form) and compute expectations from them; the figures below are the seeded ones and the DEV database may differ.
- The Customer role reads points on the web at `/rewards` and `/rewards/activity`, on iOS in the Rewards tab and on Android in the Rewards screen. The Admin role reads and changes them on the admin web (customer loyalty detail, tier configurations, referrals, promo codes, membership plans) and on the admin API.
- Tier names are Bronze Cleaner, Silver Mopper, Gold Polisher, Platinum Sparkler. The tier-upgrade notice (`loyalty.tier_upgrade`) lands in the customer's notification feed, which exists on the customer mobile host and the apps only; the customer web host has no notification feed. Push delivery is not checkable.
- Steps that "poll" wait for a webhook or a timer; no case waits longer than two minutes except where the DEV line says so.

## Points

### E2E-LOYAL-001 — A completed order earns floor(total / divisor) points
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#points — a completed order earns `floor(total / Currency.LoyaltyPointsDivisor)` in the order's currency.
- **Given** a new customer (0 points, Bronze) and a paid card order of total T (for example 1 249 CZK) taken and completed by a cleaner. **When** the order completes and the customer opens Rewards. **Then** the points balance is floor(T / 10) (124 for 1 249 CZK); the activity list has one row labelled "the cleaning completed" for that amount; the Admin's customer loyalty detail shows the same balance and the same ledger row; the loyalty account was created by this first grant. A cash order that completed with the cash recorded earns the same.
- **DEV:** follows the execution case that completes the order. Poll `Loyalty/GetMy` for up to two minutes after completion.

### E2E-LOYAL-002 — Points are floored, not rounded
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#points — `floor(total / divisor)`.
- **Given** two completed orders with totals ending in 9 CZK (for example 1 009 and 1 099) and 0 CZK (1 000). **When** each completes. **Then** the earn is 100, 109 and 100: the units digit never rounds up.
- **DEV:** shares completed orders with E2E-LOYAL-001 where the totals fit; otherwise it needs one more completed order.

### E2E-LOYAL-003 — The activity list labels each row by its source
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#points — a clawback row reads as a refund; labels come from the source alone.
- **Given** a customer with a completion earn, a partial refund clawback and an Admin manual grant. **When** the customer opens the points activity. **Then** the rows read, by source: the cleaning completed, "Refunded" (Android and iOS: "Refunded #N"), and a manual adjustment; no refund or Admin revoke reads as a cancelled booking. The web "How it works" line says a cancelled or refunded booking takes its points back, in all five languages.
- **DEV:** run after E2E-LOYAL-001, E2E-LOYAL-004 and E2E-LOYAL-008.

### E2E-LOYAL-004 — A partial refund takes back the same share of the points
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#money-constants — every refund takes back the share `floor(earned x returned so far / TotalPrice)` less what was already taken.
- **Given** a completed card order of total 1 000 CZK that earned 100 points. **When** the Admin refunds 250 CZK of it (partial refund) and the Customer reopens the activity. **Then** 25 points are taken back (balance 75); the new row is a revoke with the source of a partial refund, labelled "Refunded"; the Admin ledger shows the same row under the refund's own key.
- **DEV:** the refund goes to Stripe test mode on a 4242 card and succeeds. The cancellation-and-refund flow owns the refund rules themselves; this case checks only the points.

### E2E-LOYAL-005 — Split refunds floor once, like one refund of their sum
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#points — each refund works on the order's running total, so split refunds floor once.
- **Given** the order of E2E-LOYAL-004 after the first refund (returned 250, 25 taken). **When** the Admin refunds another 250. **Then** returned so far is 500, target floor(100 x 500 / 1 000) = 50, so 25 more are taken (balance 50, never 49 from two floorings). A third refund of 1 CZK takes 0 (target floor(100 x 501 / 1 000) = 50, nothing left to take) and writes no negative row.
- **DEV:** continues E2E-LOYAL-004 on the same order.

### E2E-LOYAL-006 — A full refund takes everything the earn still holds
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#money-constants — a full refund hands in the whole price and takes everything left.
- **Given** a second completed order of 1 000 CZK that earned 100 points. **When** the Admin refunds it in full (the full-refund action). **Then** the 100 points are taken back in one row keyed to the full refund; the customer's balance falls by 100 and, if that crosses a threshold downwards, the tier falls with it (E2E-LOYAL-014).
- **DEV:** the full refund of a 4242 order succeeds in Stripe test mode.

### E2E-LOYAL-007 — Money returned before the order completes takes its share at completion
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#points — money given back before completion takes its share when the order completes.
- **Given** a paid card order of 1 000 CZK in progress, and the Admin partly refunds 250 CZK before the cleaner completes it. **When** the cleaner completes it. **Then** the earn of 100 is written and, beside it, a clawback row of 25; the balance after completion is 75.
- **DEV:** timing-sensitive: the refund must land between the start and the completion; skip if the partial refund is refused for an order in progress and record the refusal key.

### E2E-LOYAL-008 — The Admin grants and revokes points with a reason
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#tiers — an Admin grant raises and a revoke lowers the total and the tier follows both ways.
- **Given** a customer at 0 points. **When** the Admin grants 600 points with a reason, then revokes 200 with a reason. **Then** the customer's balance is 600 then 400; the ledger rows read "manual adjustment" in the customer's activity; with a Silver threshold at or below 500 the tier moves to Silver on the grant (see E2E-LOYAL-012) and back as the revoke dictates.
- **DEV:** uses Admin grants on the run's own customer only.

### E2E-LOYAL-009 — A grant needs a reason and a sane number
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#points — grants carry an idempotency key and are validated before they land.
- **Given** an Admin session. **When** the Admin grants points with an empty reason, then with a number above the sanity cap. **Then** `loyalty.reason_required` and `loyalty.points_exceed_sanity_cap` respectively; no ledger row is written.
- **DEV:** creates nothing. The exact cap is read from the refusal; the case does not hard-code it.

### E2E-LOYAL-010 — A cancelled booking takes its points back
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#points — the points activity labels "the booking cancelled" for a cancelled order's revoke.
- **Given** a customer who earned points on an order that is then cancelled by the Admin. **When** the customer opens the activity. **Then** a revoke row labelled "the booking cancelled" exists if the platform wrote points for that order; if nothing was earned (a cancelled order never completed) there is no row. Unsure under which exact condition the platform writes this source; record the result and report the condition to the triager.
- **DEV:** depends on an Admin cancel of a completed order, which the order lifecycle may refuse; record the refusal key.

### E2E-LOYAL-011 — A currency cannot open without a points divisor
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#points — a market cannot open without a divisor, so activation and clearing the divisor on an active currency are refused `currency.loyalty_divisor_missing`.
- **Given** the active CZK currency and an inactive currency row with no divisor. **When** the Admin clears CZK's divisor, then activates the inactive currency. **Then** both are refused `currency.loyalty_divisor_missing` and CZK still earns points on the next completion.
- **DEV:** both calls are refused so nothing changes; do not send a variation that would succeed. If DEV has no inactive currency without a divisor, run only the first half.

## Tiers and the tier discount

### E2E-LOYAL-012 — Crossing a threshold raises the tier and tells the customer
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#tiers — a tier follows the points total and a grant can raise it.
- **Given** the ladder from `Loyalty/GetTiers` (thresholds and perks per tier) and a customer 50 points short of the Silver threshold. **When** the Admin grants 100 points. **Then** `Loyalty/GetMy` shows Silver with its achieved date; the Rewards page on the web, iOS and Android shows Silver and the ladder with the thresholds; the mobile notification feed holds `loyalty.tier_upgrade` naming Silver.
- **DEV:** feed check on the mobile host only. The push itself is not checkable.

### E2E-LOYAL-013 — The tier ladder lists only perks the platform provides
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#tiers — a tier's perks are what the rewards page can name; no tier promises priority support or a dedicated cleaner pool.
- **Given** the rewards page in each of the five languages. **When** the customer opens the tier ladder. **Then** every tier shows the welcome badge, tiers above the first show their discount, and no tier mentions priority support or a dedicated pool; the ladder says the tier follows the current points total and can drop.
- **DEV:** read-only; run in cs, en, sk, uk, ru.

### E2E-LOYAL-014 — A revoke can lower the tier
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#tiers — nothing ratchets; a revoke takes the total below the threshold and the tier drops.
- **Given** the customer of E2E-LOYAL-012 at Silver. **When** the Admin revokes enough points to fall below the Silver threshold. **Then** the tier returns to Bronze and its achieved date moves with it; the customer's rewards page shows Bronze. No upgrade notice is sent for a downgrade.
- **DEV:** continues E2E-LOYAL-012.

### E2E-LOYAL-015 — The tier discount applies above the floor and not below it
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#money-constants — the tier floor (`MinimumOrderAmountForDiscount`, seeded 1000 in the platform default currency) only keeps the discount off small orders.
- **Given** a customer at Silver or higher whose tier carries a discount percentage D, and the quote's `tierDiscountMinOrderAmount` (1 000 CZK). **When** the customer quotes a selection with subtotal above the floor and one below it, in CZK. **Then** the first quote shows a tier discount of D percent of the raw subtotal and `totalPrice` reduced by it; the second shows no tier discount; the quote reports the floor it judged; the web, iOS and Android summaries show the discount line and the floor rule. A Bronze customer sees no tier discount at any amount.
- **DEV:** needs a customer in a discounted tier (Admin grant). The tier floor line shows only in the platform default currency.

### E2E-LOYAL-016 — Tier and Plus discounts share a 12 % cap
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#discount-cap — tier and Plus add, then are capped at 12 % of the raw subtotal and pro-rated.
- **Given** a Platinum customer whose tier discount is 12 % and an active Plus membership (5 %). **When** the customer quotes a selection above the floor. **Then** the combined discount is 12 % of the raw subtotal, not 17 %; the tier and Plus lines both show reduced amounts that sum to the cap; the member copy on the web, iOS and Android says a top-tier subscriber gets no extra from the membership.
- **DEV:** needs a Platinum account (Admin grant) and Plus (E2E-LOYAL-034).

### E2E-LOYAL-017 — Editing the tier thresholds re-tiers every account
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#tiers — `UpdateTierConfig` re-tiers every loyalty account in the same save and sends no upgrade push.
- **Given** a customer with 400 points at Bronze and the Silver threshold at 500. **When** the Admin previews the impact of lowering Silver to 300 (`preview-threshold-impact`), applies it, then restores 500. **Then** the preview lists the account as moving to Silver; after the save the customer is Silver without a new grant, no `loyalty.tier_upgrade` is queued for it; after the restore the customer is Bronze again.
- **DEV:** edits a platform-wide setting: restore the original threshold in the same case, even if an assertion fails. Run it alone, not in parallel with tier cases.

## Credit

### E2E-LOYAL-018 — Points and credit are different balances
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#points-vs-credit — points move the tier and are never spent; credit is money off bookings.
- **Given** a customer with points and a credit balance. **When** the customer opens the credit explainer sheet (iOS and Android: Rewards or Profile) and the rewards page on the web. **Then** the text says points move the tier and its discount and are never spent, and credit is money off bookings; the credit shows per currency with an expiry date; nothing on any client offers to spend points.
- **DEV:** read-only; needs the Admin credit from E2E-LOYAL-019.

### E2E-LOYAL-019 — The Admin issues credit and the customer sees it
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#credit — credit is money the platform owes the customer, one account per currency, with an expiry 12 months after the last movement.
- **Given** a customer with no credit. **When** the Admin issues 500 CZK of goodwill credit with a reason. **Then** `Credit/GetMy` for the customer lists a CZK balance of 500 with an expiry date about 12 months out and `maxShareOfOrder` 0.7; the Admin's credit view for the user shows the grant; the customer's apps and the web show it.
- **DEV:** use goodwill on the run's own customer only.

### E2E-LOYAL-020 — An Admin credit grant is validated
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#money-constants — the credit sanity cap, whole minor units and issuable reasons.
- **Given** an Admin session. **When** the Admin issues 10 001 CZK, then 0.001 CZK, then credit with the dispute-settlement reason. **Then** `credit.amount_exceeds_sanity_cap`, `credit.amount_not_whole_minor_units` and `credit.reason_not_issuable` (or `credit.dispute_settlement_not_issuable`; record which) respectively; no balance changes.
- **DEV:** creates nothing. The cap is 10 000 per the business rules.

### E2E-LOYAL-021 — Credit pays up to 70 % of a card booking, and the card pays the rest
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#credit — credit is spent automatically on the next card booking in the same currency, at most 70 % of the total, floored to cents.
- **Given** a customer with 500 CZK credit and a card booking whose total is 400 CZK. **When** the customer books it by card. **Then** `creditAppliedAmount` is 280.00, `amountDueOnCard` 120.00 and `totalPrice` stays 400.00; the Stripe session or intent is for 12 000 minor units; the balance falls to 220. With a total of 399.98 the credit is 279.98 (floored to the cent) and the card is asked for 120.00; rounding would give 279.99 and 119.99. A balance smaller than 70 % is spent in full. The web payment step shows "To pay by card" before Stripe does.
- **DEV:** creates a card order and a Stripe test session for the reduced amount; use the 4242 card to pay if the case continues into the payment flow.

### E2E-LOYAL-022 — A cash booking spends no credit
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#credit — credit is a tender for card bookings only; a cash booking spends none.
- **Given** a customer with credit, a usable saved card and a one-cleaner selection. **When** the customer books it as cash. **Then** `creditAppliedAmount` is 0, the balance is unchanged, and the confirm step (web, iOS, Android) says credit applies to card payments only.
- **DEV:** creates one cash order; cancel it.

### E2E-LOYAL-023 — A cancelled or refunded booking gives its credit back once
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#points-vs-credit — a refunded or cancelled booking gives back what it spent.
- **Given** the order of E2E-LOYAL-021 (credit 280 applied) cancelled by the customer in its oops window. **When** the cancellation completes and, separately, the Admin cancels a second such order. **Then** the credit balance is restored by exactly 280 per order, once; asking again (repeated cancel call) restores nothing more; the card leg is refunded by the cancellation policy.
- **DEV:** the cancellation policy and refund rules belong to the cancellation flow; this case checks the credit leg only.

### E2E-LOYAL-024 — Credit of one currency never pays a booking in another
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#credit — one account per currency; a CZK balance never pays a EUR booking.
- **Given** a customer with CZK credit and a second serviced market in EUR. **When** the customer quotes and books a EUR card booking. **Then** `creditBalance` in the EUR quote is 0, `creditAppliedAmount` 0, and the CZK balance is untouched.
- **DEV:** runs only if `Market/GetOverview` lists a EUR market on the day; otherwise not run.

### E2E-LOYAL-025 — The Admin can expire a customer's credit
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#credit — an administrator's Expire credit discharges the balance.
- **Given** a customer with credit. **When** the Admin expires it with a reason. **Then** the balance is 0 in that currency; the ledger has an expiry row; a later card booking applies no credit.
- **DEV:** run on the credit left after E2E-LOYAL-021 to E2E-LOYAL-023 so nothing is wasted.

## Referrals

### E2E-LOYAL-026 — A customer has a code and a share link that lands on a page
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#referrals — the code is generated randomly the first time the customer opens it.
- **Given** a registered customer. **When** the customer opens the referral card on the Rewards page (`Referral/GetMy`). **Then** a code exists, is not derived from the name or e-mail, and is the same on a second read; the web share link `/r/{code}` opens the public referral landing for a signed-out visitor and `Referral/Validate` (anonymous) answers valid for it and invalid for a made-up code.
- **DEV:** creates one referral code.

### E2E-LOYAL-027 — A qualified referral pays both sides credit
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#referrals — both sides are paid `Currency.ReferralCredit` (CZK 150 seeded) in their own currency when the friend's first order completes.
- **Given** customer A with a code and a new customer B who registers with it (`Auth/Register` with `referralCode`, or the sign-up screen's referral field; the web `/r/{code}` link prefills it). **When** B's first order (a card order, paid) is completed by a cleaner. **Then** the referral goes Accepted then Qualified; both A and B hold 150 CZK in `Credit/GetMy` under the keys `referral:{id}:referrer` and `referral:{id}:referred`; A's `GetMyReferrals` shows B as qualified and the code's qualified counter rose by one; the Admin's referrals list shows it Qualified with the amounts and currencies. Referral pays credit, not points: neither side's points changed beyond the order's own earn.
- **DEV:** B must not share a phone, address or e-mail inbox with A, or the referral is held (E2E-LOYAL-029). Poll for the credit for up to two minutes after completion.

### E2E-LOYAL-028 — Own code, unknown code and a second referral are refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#referrals — you cannot redeem your own code and you cannot be referred twice.
- **Given** customer A and customer B already referred by A. **When** A enters their own code on a booking, a new customer registers with a code nobody owns, and B tries a different customer's code on a later booking. **Then** the own code and the second referral are refused or ignored without a second `Referral` row (record the exact key the server returns; the flow page does not name it); the unknown code leaves the registration working without a referral.
- **DEV:** unsure of the keys and whether an unknown code refuses the registration or is dropped; the case records what the server does and the triager checks it against the docs.

### E2E-LOYAL-029 — A same-household referral is held for an Admin
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#referral-hold — accounts that share a home, a phone or an e-mail inbox are held; nothing is paid; the administrators are told.
- **Given** customer A and customer B who registers with A's code and the same phone number, and completes a first order. **When** the order completes. **Then** the referral stays Accepted with its hold reasons and the recorded order, no credit is paid to either side, and the company's administrators receive `admin.referral.held` in the admin notifications; the referrals list shows it under the Held filter.
- **DEV:** two DEV accounts with the same phone are easy to make; the hold never expires on its own.

### E2E-LOYAL-030 — The Admin can release, reject, force-qualify and reverse
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#referrals — release pays the friend in the held order's currency, reject takes nothing, reverse takes back only what each balance still holds.
- **Given** (a) the held referral of E2E-LOYAL-029, (b) a second held referral, (c) the qualified referral of E2E-LOYAL-027 after one side spent some credit. **When** the Admin releases (a) with `force-qualify` and `expectHeld` true, rejects (b) with `reverse` and `expectHeld` true, and reverses (c). **Then** (a) pays the friend (and the referrer by their own latest order's currency) and turns Qualified; (b) pays and takes nothing and ends Reversed; (c) takes back only the balance each side still holds, never below zero. Sending an action with the wrong `expectHeld` (a release sent with `expectHeld` false on a held referral) is refused `referral.hold_changed` and the web list reloads. An empty reason is `referral.reason_required`, force-qualify on a non-Accepted referral `referral.not_accepted`, reverse on a non-Qualified one `referral.not_qualified`, an unknown id `referral.not_found`.
- **DEV:** run after E2E-LOYAL-027 and E2E-LOYAL-029.

### E2E-LOYAL-031 — Only the friend's first order, paid at the door, qualifies the referral
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#referrals — an order reported unpaid at the door never qualifies; a later completed order leaves it Accepted until it expires.
- **Given** a referred customer B whose first order was a cash booking the cleaner reported unpaid at the door. **When** the order completes and B's second order completes paid. **Then** the referral stays Accepted after the first (and after the second), nothing is paid, and the Admin's referrals list shows it Accepted.
- **DEV:** needs the cash-not-paid path of the execution flow, which creates a receivable on the account; clear it afterwards. Not run if that path is not part of the run.

## Cleansia Plus

### E2E-LOYAL-032 — The plans are listed in the chosen market's currency
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#plus-is-priced-per-market-end-to-end — only plans with a price row in the market's currency are listed, labelled with it.
- **Given** the default market. **When** a guest calls `Membership/GetPlans` (anonymous, on both hosts) with the default market's `countryId`, with none, and with an unknown `countryId`. **Then** the first two list `PLUS_MONTHLY` and `PLUS_YEARLY` in CZK with prices (199 and 2 030 seeded), `discountPercentage` 5, `freeCancellationWindowHours` 4, `expressUpgradesPerMonth`, `trialPeriodDays` (14 once the DEV seed is current) and `savingsPercentVsMonthly`; an unknown or unserviced `countryId` answers an empty list, never an error. The web `/plus` page shows the plans to a signed-out visitor and offers the trial days.
- **DEV:** if `trialPeriodDays` is 0, the DEV seed predates the ruling and `sql-scripts/fix-plus-trial-14-days.sql` sets 14; record it and do not assert 14. A second market without prices shows "Plus is not available in your market yet", no price and no button; run that half only if DEV lists one.

### E2E-LOYAL-033 — A market with no priced plan refuses a subscription
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#plus-is-priced-per-market-end-to-end — no price row means not listed and `membership.plan.not_priced_in_currency` on subscribe.
- **Given** a second serviced market in EUR with no Plus rows. **When** the customer posts `CreateCheckoutSession` and `Subscribe` with that `countryId`. **Then** both are refused `membership.plan.not_priced_in_currency` and nothing reaches Stripe; an unserviced `countryId` is refused `country.not_serviced`.
- **DEV:** runs only if a second priced market exists; the `country.not_serviced` half runs regardless with a made-up country.

### E2E-LOYAL-034 — A new member starts the 14-day trial through Stripe Checkout on the web
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#plus-trial — a trialing member is held as Active with the trial's end in `TrialEndsAtUtc` and gets every benefit from day one.
- **Given** a confirmed customer who has never had a trial (`GetMine` `trialEligible` true) and a plan with trial days above 0. **When** the customer subscribes to `PLUS_MONTHLY` on the web (`/plus`, sign in, `CreateCheckoutSession`, the Stripe hosted page with 4242) and returns. **Then** after the webhook (poll up to two minutes) `GetMine` shows `hasMembership` true, status Active, `trialEndsAtUtc` about 14 days ahead, `currencyCode` CZK, `trialEligible` false; the web membership page shows "Free trial until" that date; no charge was taken (Stripe test mode shows a trialing subscription).
- **DEV:** Stripe test mode and the webhook to DEV must work; the first charge falls 14 days later and is not checked.

### E2E-LOYAL-035 — The mobile apps subscribe through the payment sheet
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#cleansia-plus — the apps subscribe through `Membership/Subscribe` with the plan code and the market's `countryId`.
- **Given** a customer with no membership, signed in on the app. **When** the customer opens Membership, taps Subscribe on the monthly plan, completes the Stripe sheet with 4242 and returns. **Then** the same state as E2E-LOYAL-034 appears in the app (Active, trial end, benefits) and the success screen shows; the app offers the trial only because the server said `trialEligible`.
- **DEV:** unsure how the Stripe payment sheet is driven under AXe and adb; if it cannot be automated the agent drives it by sight. Use a second customer if the first already holds Plus.

### E2E-LOYAL-036 — A trialing member has every benefit from day one
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cleansia-plus — seven benefits resolve through one entitlement predicate; a trialing enrolment is served like a paying one.
- **Given** the member of E2E-LOYAL-034. **When** the member quotes a selection above the tier floor, creates a recurring schedule, books naming a cleaner who served them, and cancels a fresh booking after 20 minutes. **Then** the quote carries the Plus discount (5 %) on the raw subtotal; the schedule is accepted (not `recurring_booking.membership_required`); the preferred cleaner is not refused for the membership (`order.preferred_employee.membership_required` absent); the cancellation preview shows an oops window of 60 minutes (a non-member is 15 after the first booking) and a free-cancellation window of 4 hours instead of 24.
- **DEV:** run the oops-window part on a booking that is not the customer's first (a first booking is 60 for everyone).

### E2E-LOYAL-037 — A second subscription is refused while one is live
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#cleansia-plus — both subscribe paths refuse a live enrolment with `membership.already_active`.
- **Given** the member of E2E-LOYAL-034. **When** the member posts `CreateCheckoutSession` and `Subscribe` again for either plan, and switches the market selector. **Then** both are refused `membership.already_active`; the web and apps offer no second subscription, even in another market; no second Stripe subscription exists.
- **DEV:** creates nothing.

### E2E-LOYAL-038 — An account gets one trial
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#plus-trial — any earlier trial means none.
- **Given** the member of E2E-LOYAL-034 who cancels (E2E-LOYAL-039) and later re-subscribes after the membership has ended. **When** the customer subscribes again. **Then** `trialEligible` is false in `GetMine` and Stripe is asked for no trial (the new subscription starts without a trial period); the web and apps show the paid wording.
- **DEV:** the membership must end before a re-subscribe is accepted, and a cancel inside a trial ends it only at the trial's end 14 days later; this case cannot complete on DEV in one run. Run the second half only on an account whose membership was ended in Stripe test mode by the Admin; otherwise record "not run".

### E2E-LOYAL-039 — Cancelling inside the trial keeps the benefits to its end
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#plus-trial — cancelling inside the trial charges nothing and the benefits run to the trial's end.
- **Given** the member of E2E-LOYAL-034. **When** the member cancels (`Membership/Cancel`). **Then** `GetMine` shows `cancelRequested` true, status still Active, the same `trialEndsAtUtc`; the discount and windows still apply; no invoice exists in Stripe test mode; the web and apps show the cancellation scheduled and no Cancel button.
- **DEV:** the membership ends 14 days later, outside the run.

### E2E-LOYAL-040 — A member swaps from monthly to yearly
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#plus-is-priced-per-market-end-to-end — a swap looks the target plan up in the membership's own currency.
- **Given** an Active member on `PLUS_MONTHLY` in CZK. **When** the member posts `SwapPlan` with `PLUS_YEARLY`. **Then** `GetMine` shows the yearly plan with its CZK price (2 030 seeded) and the new billing interval; the apps and the web offered the annual switch only because the yearly plan is priced in the membership's currency.
- **DEV:** Stripe test mode swaps a trialing subscription; unsure whether the platform allows a swap inside the trial: record the result (success or key).

### E2E-LOYAL-041 — A swap that cannot work is refused with a key
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#cleansia-plus — a plan swap requires an Active membership and a priced, different plan.
- **Given** (a) a customer with no membership, (b) an Active member on `PLUS_YEARLY`, (c) an Active member. **When** (a) swaps to `PLUS_MONTHLY`, (b) swaps to `PLUS_YEARLY`, (c) swaps to a made-up code. **Then** (a) `membership.not_found`, (b) `membership.swap_same_plan`, (c) `membership.plan.not_found`. A swap to a plan with no row in the membership's currency is `membership.plan.not_priced_in_currency` and the clients do not offer it (needs a plan priced in only one currency; not run when none exists).
- **DEV:** (b) and (c) need an Active member; reuse the member of E2E-LOYAL-034.

### E2E-LOYAL-042 — The express waiver is free for a member and metered per calendar month
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#the-express-waiver-is-metered-per-calendar-month — the surcharge is waived N times per calendar month, then charged.
- **Given** an entitled member with `expressUpgradesRemaining` N (read from `GetMine`) and a start 3 h away. **When** the member quotes and books N express bookings, then one more. **Then** each of the first N shows no +20 % line (the quote marks the waiver) and `expressUpgradesRemaining` drops by one per booking; the (N+1)th quote and booking carry the +20 % surcharge. A guest quoting the same slot sees the surcharge charged, not waived.
- **DEV:** run between 05:00 and 16:45 Prague (the slot must be inside 08:00 to 19:45). Creates N+1 orders; cancel them. Cancelling a waived booking may return the quota: record whether `expressUpgradesRemaining` rises (the flow says the smallest free ordinal is reused).

### E2E-LOYAL-043 — A waiver lost between the quote and the booking is its own refusal
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/booking-and-pricing#edge-cases — the quote promised a waiver and the quota ran out before the booking; refused with its own key, never silently charged 20 % more.
- **Given** a member with exactly one waiver remaining and two sessions. **When** session 1 quotes a 3 h slot (waived), session 2 books another express slot using the last waiver, then session 1 books the original total. **Then** session 1 is refused `membership.express_waiver.no_longer_available` and no order is created; a re-quote shows the surcharge.
- **DEV:** needs a member with one waiver left (book N-1 first). Same clock window as E2E-LOYAL-042.

### E2E-LOYAL-044 — The waiver quota survives a re-subscribe and a downgrade
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#edge-cases — the quota key has no membership id in it, so re-subscribing does not reset it and a downgrade carries the live count.
- **Given** a member who used one waiver this month. **When** the member swaps to a plan with a smaller quota, and (if the membership ends) re-subscribes in the same month. **Then** `expressUpgradesRemaining` reflects the used one against the new quota and never grants more than that plan allows; the re-subscribe does not restore the used waiver.
- **DEV:** the re-subscribe half cannot complete inside a run (E2E-LOYAL-038); the swap half is runnable when the two plans differ in quota.

### E2E-LOYAL-045 — A member without a market price sees "not available" and nothing to buy
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/loyalty-and-memberships#edge-cases — a Plus page opened in a market with no priced plan shows the empty state with no price and no button.
- **Given** a second market without Plus prices. **When** the customer switches the market and opens `/plus`, the wizard's Plus step and the apps' Subscribe screen. **Then** each shows "Plus is not available in your market yet", with no price and no button. For an existing member, the membership keeps its own currency and labels after the switch.
- **DEV:** runs only if DEV lists such a market; otherwise not run.

### E2E-LOYAL-046 — The Admin manages the plans
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cleansia-plus — a plan has a discount, an express quota, a trial and per-currency prices set by an administrator.
- **Given** an Admin session. **When** the Admin creates a throwaway plan with a duplicate code, with a discount above the range, with a Stripe price already used by another plan, and then a valid one; lists plans; deactivates it. **Then** the refusals are `membership.plan.code_already_exists`, `membership.plan.discount_out_of_range` and `membership.plan.stripe_price_already_used`; a negative trial is `validation.must_be_positive`; the valid plan appears in the paged list and the detail, and disappears from `GetPlans` once deactivated.
- **DEV:** creates a plan row on DEV; deactivate it at the end. A valid plan needs a real Stripe Price id from test mode.

### E2E-LOYAL-047 — A subscribed plan's offer is fixed
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/loyalty-and-memberships#plus-is-priced-per-market-end-to-end — once anyone has subscribed, discount and express quota cannot change (`membership.plan.benefits_locked`).
- **Given** the plan the E2E-LOYAL-034 member subscribed to. **When** the Admin changes its discount, then its express quota, then switches the express waiver off, then renames it and changes its trial days. **Then** the first three are refused `membership.plan.benefits_locked`; the rename and the trial change save; the plan detail says `benefitsLocked` true and the admin form shows both fields disabled.
- **DEV:** use the seeded plan only for the refused calls; restore the name and the trial if the saving calls were made.

## Public promo-code request

### E2E-LOYAL-048 — One promo code is sent per e-mail address
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#public-promo-code-requests — the first request creates a code and queues the e-mail; a repeat is HTTP 400 `promo.already_sent`.
- **Given** a fresh `@example.com` address that has never requested. **When** the guest posts `PromoCode/Request` with it, then again with the same address, then with ` UPPER@Example.com ` spelled in other case and with surrounding spaces. **Then** the first is 200 and the response does not contain the code; the second and the third are 400 `promo.already_sent`; the Admin's promo list shows exactly one code for that request.
- **DEV:** the e-mail goes nowhere (`@example.com`); only the code row is checked. Unsure which page hosts the public form (it is not in the customer web app libraries): run on the API.

### E2E-LOYAL-049 — Concurrent promo requests produce one code
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/loyalty-and-memberships#public-promo-code-requests — the database's unique constraints choose one successful request.
- **Given** a fresh address. **When** the guest sends five identical requests at the same moment. **Then** exactly one is 200 and four are 400 `promo.already_sent`; one code exists.
- **DEV:** use the API client with parallel calls.

## Not testable on DEV

- E-mail and push content: the promo code itself (sent by e-mail only), referral and tier notices in e-mail form, `membership.payment_failed` and `membership.expiring_soon` copy, the tier-upgrade push text. `@example.com` never receives mail; only feed rows on the mobile host and stored state are checked.
- A past-due or paused membership (a failed renewal or a failed first charge), its "payment failed, benefits paused" state, the immediate cancel with the voided invoice, and the `membership.payment_failed` notice per attempt: they need a Stripe test clock or a card that fails on renewal, which the Phase 1 suite does not wire.
- The end of a trial (14 days), the first charge, a trial that ends unpaid pausing a recurring schedule (`recurring.paused`), the 90-day referral expiry and its daily sweep, the 12-month credit expiry and its 03:30 UTC sweep, and the weekly retention sweep of tokens: they need the calendar to pass.
- Races: two loyalty writes for one customer at once (the `xmin` replay), two refunds at once taking one share, two first grants for a customer with no account, two checkouts racing for one credit balance, two bookings racing for the last waiver, a release and a reject at once, two grants to the same referral pair. They are covered by the backend integration suite on a real Postgres.
- Re-subscribing in another currency (one Stripe customer per currency), a Stripe customer locked to a currency, and a webhook naming a currency the platform does not know: need a second priced market and a deliberate Stripe fault; the DEV sandbox did not enforce the currency lock on 2026-09-13.
- Points for an order in a currency with no divisor (unreachable through the admin surface), and one side of a referral having been erased (belongs to the GDPR cases).
- A second operating company: loyalty and credit following the account across operators, and the credit of a deactivated company being lost.
- Apple and Google sign-in as a way to sign up with a referral code, real e-mail delivery, and load.
