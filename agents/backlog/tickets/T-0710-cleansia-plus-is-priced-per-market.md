---
id: T-0710
title: Cleansia Plus is priced per market
status: done
size: L
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: []
blocks: [T-0713, T-0714, T-0716, T-0718, T-0720]
stories: []
adrs: [ADR-0059]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**A membership plan carried one CZK price and one Stripe Price id, so Plus could be sold in exactly one
currency, and a subscription recorded no currency at all.** The owner ruled (2026-09-12) that Plus is
per currency and per region — CZ in CZK, SK in EUR. ADR-0059 moves the price and the Stripe id onto a
row per (plan, currency), creates the subscription in the chosen market's currency and keeps it there
for life, and lets a customer surface ask for the price in its market.

## Doing

- `MembershipPlanPrice` (plan × currency: price + Stripe Price id; unique on the pair and on the Stripe
  id; plan FK Cascade, currency FK Restrict), `IMembershipPlanPriceRepository`; `MembershipPlan` loses
  `MonthlyPriceCzk`, `MonthlyEquivalentPriceCzk`, `StripePriceId`, `UpdatePricing` and the two `Create`
  parameters; `MonthlyEquivalentOf(price)` replaces the computed property.
- `UserMembership.CurrencyId` (required, FK Restrict, `Currency` nav, written once at creation);
  `CurrencyRepository.IsInUseAsync` names `MembershipPlanPrices` and `UserMemberships`.
- Customer: `GET api/Membership/GetPlans?countryId=` answers in the market's currency, lists only the
  plans priced in it, `CurrencyCode` on every row; `GetMine` renames `MonthlyPriceCzk → Price`,
  `MonthlyEquivalentPriceCzk → MonthlyEquivalentPrice`, adds `CurrencyCode` (the membership's own);
  `CreateCheckoutSession` / `Subscribe` take `CountryId` (serviced or `country.not_serviced`), resolve
  the currency, hand Stripe that row's Stripe Price, refuse `membership.plan.not_priced_in_currency`
  before any Stripe call; `Swap` picks the target plan's row in the membership's currency; the webhook
  reads `Subscription.Currency` off the Stripe object and provisions nothing for a code the platform
  does not know; the attempt-id fallback carries the currency code.
- `StripeRefusals.IsCustomerCurrencyLocked` classifies Stripe's "cannot combine currencies on a single
  customer" refusal into `membership.stripe_customer_currency_locked` on both subscribe paths.
- Admin: `Prices` dictionary keyed by currency code on create/update (empty or partial is legal, no
  `MustCoverAllActiveCurrencies`; unknown key `currency.not_found`; each entry `Price ≥ 0`, Stripe id
  required ≤ 64 and not on another plan `membership.plan.stripe_price_already_used`); detail carries
  `Prices` keyed by code; list carries the platform-default row as `decimal? Price` /
  `MonthlyEquivalentPrice` + `CurrencyCode`, null when absent.
- Seeds (`MembershipPlanPrices` × CZK for both plans, plan columns dropped), host seed Stripe id derived
  from the plan code, `Initial` regenerated.

## NOT

No Stripe Price objects created; no EUR rows; no migration of existing subscriptions; no currency
change on a live subscription; `IsOfferableAsync` untouched; `ActivateCurrency` gains no plan gate;
no local "ever subscribed in another currency" pre-check (classify Stripe's refusal only); no
`IStripeClient` signature or subscription-metadata change; no web/mobile code; NSwag / mobile-spec
regens (orchestrator's); **no joined price sort on the admin plan list** — `MembershipPlanSort` drops
the price key and falls through to the default order, as `PackageSort` does and for the same reason
(a price is per currency); the shipped admin list sends no sort, and the list is two rows.

## Acceptance criteria

See the programme ticket plan (AC1–AC14): admin detail keyed by code with no absent key, partial prices
saveable, unknown key, Stripe id unique, per-market listing, the market's row handed to Stripe, unpriced
market refused, the swap in the membership's currency, the webhook's currency source, the Stripe
Customer-currency refusal, currency-free benefits, `GetMine` in the membership's currency, the currency
deletion guard, and the shared host-test database.

## Review

- Stripe sandbox probe (DEV test key, 2026-09-13): a Customer with a cancelled CZK subscription was
  ALLOWED both a EUR Checkout Session (open) and a EUR subscription (created, incomplete) — Stripe did
  not enforce the per-Customer currency lock for that shape; the classification is kept for the
  documented rule and keyed on its documented wording, since Stripe returns no dedicated error code.
- Tests: `GetMembershipPlansHandlerTests`, `SwapMembershipPlanCurrencyTests`,
  `GetMyMembershipCurrencyTests`, `StripeRefusalsTests`, `CreateMembershipCheckoutSessionContractLockTests`
  (+2), `CreateMembershipSubscriptionIdempotencyTests` (+1), `MembershipCommandsStripeFailureTests` (+2),
  `WebhookProvisionActiveMembershipIdempotencyTests` (+2), `MembershipPlanPlatformConfigStructuralTests`
  (+1), `Create/UpdateMembershipPlanValidatorTests`, `Create/UpdateMembershipPlanHandlerTests`,
  `GetPagedMembershipPlansHandlerTests`, `QuoteOrderMembershipDiscountIsCurrencyFreeTests`;
  `MembershipPlanPerMarketTests` + `SubscriptionWebhookIntegrationTests` (+1) on real Postgres;
  `MembershipPlanPerMarketRouteTests` on the Admin and Customer hosts.
- Sabotage: ten rules broken once each, the named test red, file restored byte-exact (sha256), green.
- Catalog-edit routing: no `patterns-backend.md` / `consistency.md` entry added — every shape mirrors an
  existing one (`PackagePrice` + `CreatePackage`'s upsert for the row and the admin write,
  `CataloguePriceLookup` for the per-currency read, `QuoteOrder` for the `CountryId` rule).
