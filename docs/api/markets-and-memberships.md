# Markets and memberships

The market directory a customer browses in, the Cleansia Plus endpoints that read it, and the admin
writes that author a market's prices and figures. The rules behind every endpoint here are
[ADR-0058](/decisions/adr-0058) (the market), [ADR-0059](/decisions/adr-0059) (Plus per market) and
[ADR-0060](/decisions/adr-0060) (copy figures per market).

::: info Source Files
- Market read: `src/Cleansia.Core.AppServices/Features/Markets/`, `Cleansia.Web.Customer/Controllers/MarketController.cs` (and the Mobile.Customer twin)
- Membership: `src/Cleansia.Core.AppServices/Features/Memberships/`, `Cleansia.Web.Customer/Controllers/MembershipController.cs` (and the Mobile.Customer twin)
- Admin plans: `src/Cleansia.Core.AppServices/Features/Memberships/Admin/`, `Cleansia.Web.Admin/Controllers/AdminMembershipController.cs`
- Admin country / currency: `Features/Countries/`, `Features/Currencies/`, `AdminCountryController.cs`, `AdminCurrencyController.cs`
:::

## The market, in one paragraph

A **market** is a serviced country whose configuration names an **active** currency **and an operating
company** (`CountryConfiguration.OperatorTenantId`, [ADR-0061](/decisions/adr-0061)). A customer
chooses one (remembered per device, defaulting to the flagged default market) and every surface that
has no service address yet reads it: the catalogue overviews, the quote, the Plus plans, the subscribe
commands and the copy figures. The address, once there is one, wins. The market reaches the API only as
`countryId` — there is no header and no session field, and **there is no `tenantId` on any wire**: an
anonymous request that writes names its market and the server maps market → company. Which anonymous
requests carry `countryId` for that reason, and what they refuse, is in
[Authentication](/api/authentication#the-market-on-anonymous-requests) and
[Orders](/api/orders#createorder).
→ [Business rules — the market](/product/business-rules#market)

---

## Market/GetOverview <Badge type="info" text="Customer + Customer Mobile" />

```
GET /api/Market/GetOverview
```

**Auth:** Anonymous — this is the read behind the landing page, before anyone signs in.

**Response:** one row per market. The row whose country configuration carries `IsDefaultMarket` is
`isDefault` (at most one, by the database; CZE seeded; moved by the admin PUT below). When nothing is
flagged, or the flagged country is not listed, an error is logged and the fallback rule decides:
exactly one row is `isDefault` when one market is on the platform default currency; with several,
the lowest `isoCode` is flagged and an error is logged; with none, no row is flagged and an error is
logged. The read **never throws** on a configuration state: a serviced country with no configuration,
or whose configured currency is unknown or inactive, is omitted and logged as a warning; a serviced,
configured, currency-active country that **no operating company serves** (`OperatorTenantId` null) is
omitted and logged as an **error** — it is a seed defect, since every anonymous write naming it would
fail `tenant.not_found`, and the picker must not offer it. This — not `Country/GetServiced`, which
still lists such a country — is the read any registration picker must use, because a cleaner registers
with a market and is held to its company at approval.

```json
[
  {
    "countryId": "country-id",
    "isoCode": "CZE",
    "isoAlpha2": "CZ",
    "name": "Czech Republic",
    "translations": { "cs": { "name": "Česko" } },
    "currencyId": "currency-id",
    "currencyCode": "CZK",
    "currencySymbol": "Kč",
    "isDefault": true,
    "noShowCredit": 250.00,
    "insuranceCoverageAmount": 1000000.00
  }
]
```

| Field | Meaning |
|---|---|
| `isoCode` | ISO 3166-1 alpha-3, `Country.IsoCode`. **What a client persists** (cookie `preferred_market` on the web, `market` / `settings.market` on mobile) — stable across the DEV reseeds that re-mint ids |
| `isoAlpha2` | The two letters the market chip prints beside the currency code ("CZ · CZK"). Display only; nothing keys on it |
| `currencyCode` / `currencySymbol` | The market's currency — `CountryConfiguration.DefaultCurrencyCode` resolved to its `Currency` row |
| `isDefault` | The pre-selection for a visitor who has chosen nothing — the flagged `CountryConfiguration.IsDefaultMarket`, else the fallback rule above. A **pre-selection, not a pricing invariant** — a client always sends the `countryId` it resolved |
| `noShowCredit` | `Currency.NoShowCredit` — the apology credit paid in this currency when a slot arrives with no cleaner; `null` = none is paid and the copy renders its refund-only variant. CZK 250; EUR 10, PLN 40, GBP 9, USD 10 are DEV placeholders the owner replaces before activation |
| `insuranceCoverageAmount` | `CountryConfiguration.InsuranceCoverageAmount` — the insurance ceiling the trust badge and FAQ state, a number in the market's currency; `null` = the no-figure copy renders. CZE seeded at 1 000 000; every other configuration `null` until the owner authors it |

A client resolves its market as: stored code **if listed** → the `isDefault` row → the first row →
persist. A stored value is only ever compared against the list, never rendered or sent. When this
call fails the client renders no chip and no selector, sends no `countryId` anywhere, persists
nothing, and retries on the next navigation / refresh.

---

## Membership/GetPlans <Badge type="info" text="Customer + Customer Mobile" />

```
GET /api/Membership/GetPlans?countryId=country-id
```

**Auth:** Anonymous

`countryId` — optional; the chosen market. The plans are priced in that market's currency and **only
plans that have a price row in it are listed**, so an empty list means Plus is not on sale in that
market (the clients render *"Plus is not available in your market yet"*, no price, no button). An
unserviced or unknown `countryId` answers an empty list — never the default market's prices and never
an error. Without `countryId` the list is in the platform default currency.

```json
[
  {
    "code": "PLUS_YEARLY",
    "name": "Cleansia Plus (yearly)",
    "price": 2030.00,
    "monthlyEquivalentPrice": 169.17,
    "billingInterval": 2,
    "discountPercentage": 5,
    "freeCancellationWindowHours": 4,
    "allowsExpressUpgrade": true,
    "expressUpgradesPerMonth": 1,
    "trialPeriodDays": 0,
    "savingsPercentVsMonthly": 15,
    "currencyCode": "CZK"
  }
]
```

`price` is one billing period (the full annual charge for a yearly plan); `monthlyEquivalentPrice`
is `price / 12` for a yearly plan, else `price`; `savingsPercentVsMonthly` compares the yearly row
against the cheapest **monthly** row **in the same currency**, so it may differ between markets. Every
figure on the row is in `currencyCode` — a client labels from it, never from a currency it assumed.

---

## Membership/GetMine <Badge type="info" text="Customer + Customer Mobile" />

```
GET /api/Membership/GetMine
```

**Auth:** `CanManageMembership`

The customer's own membership, priced in **the membership's currency** — the market it was created
in, kept for life — not the market the customer is browsing now. `price` and
`monthlyEquivalentPrice` are the plan's row in that currency; `currencyCode` says which. All three
are `null` with no membership, and `price` / `monthlyEquivalentPrice` are `null` (with a logged
warning) if the plan's row in that currency has been removed. The other fields are unchanged
(`hasMembership`, `planCode`, `planName`, `discountPercentage`, `freeCancellationWindowHours`,
`allowsExpressUpgrade`, `status`, `currentPeriodEnd`, `cancelRequested`, `billingInterval`,
`expressUpgradesPerMonth`, `expressUpgradesRemaining`, `trialEndsAtUtc`, `trialEligible`).

---

## Membership/CreateCheckoutSession <Badge type="info" text="Customer" /> and Membership/Subscribe <Badge type="info" text="Customer + Customer Mobile" />

```
POST /api/Membership/CreateCheckoutSession      { "planCode": "PLUS_MONTHLY", "countryId": "country-id" }
POST /api/Membership/Subscribe                  { "planCode": "PLUS_MONTHLY", "paymentMethodConfirmed": false, "countryId": "country-id", "idempotencyToken": "…" }
```

**Auth:** `CanManageMembership`

`countryId` — optional; the chosen market (the wizard's Plus step sends the **market's**, not the
address's). A named country must be serviced (`country.not_serviced`). The server resolves the
market's currency with the same resolver the quote uses, loads the plan's price row in it, and hands
**that row's** Stripe Price id to Stripe. The subscription is therefore created in the market's
currency and `UserMembership.CurrencyId` records it; the webhook confirms the currency off the Stripe
`Subscription` object itself and refuses to provision a code the platform does not know.

**Which Stripe Customer is billed:** the user's Customer **for that currency** (`UserStripeCustomers`,
one per user per currency — Stripe locks a Customer to the currency of its first invoice). Both
commands resolve it after the plan and price checks: an existing row for the currency; else the
legacy `User.StripeCustomerId`, adopted when it has never billed a membership in another currency
and no row claims it; else a new Customer (`payment.gateway_unavailable` if Stripe fails there). So a
customer whose CZK Plus was cancelled subscribes in EUR on a second Customer, and nothing is asked
of support. → [Loyalty and memberships](/flows/loyalty-and-memberships#plus-is-priced-per-market-end-to-end)

| Refusal | When |
|---|---|
| `membership.plan.not_priced_in_currency` | The plan has no price row in the resolved currency — refused before any Stripe object exists |
| `membership.stripe_customer_currency_locked` | Stripe refused the call with its "cannot combine currencies on a single customer" rule on a Customer the resolver could not see was locked. The **backstop**, since the per-currency Customer makes the ordinary re-subscribe never hit it. Classified, never a 500. The DEV sandbox did not enforce the rule on 2026-09-13 |
| `membership.already_active` | The customer already has a live membership — switching market never offers a second one |
| `country.not_serviced` | `countryId` names a country the platform does not service |

---

## Membership/SwapPlan <Badge type="info" text="Customer + Customer Mobile" />

```
POST /api/Membership/SwapPlan      { "newPlanCode": "PLUS_YEARLY" }
```

**Auth:** `CanManageMembership`

No `countryId`: the target plan's price row is looked up in **the membership's own currency**, because
Stripe refuses a currency change on a live subscription and the platform refuses first. A target with
no row in that currency answers `membership.plan.not_priced_in_currency`. The clients only offer a
switch the server will accept — a yearly plan whose `currencyCode` equals the membership's.

---

## Admin — membership plans <Badge type="info" text="Admin" />

```
GET  /api/AdminMembership/get-paged
GET  /api/AdminMembership/details/{membershipPlanId}
POST /api/AdminMembership/create
PUT  /api/AdminMembership/update/{membershipPlanId}
POST /api/AdminMembership/deactivate/{membershipPlanId}
```

**Auth:** `CanViewMembershipPlans` / `CanCreateMembershipPlan` / `CanUpdateMembershipPlan` /
`CanDeactivateMembershipPlan`

A plan's price is **a row per currency**, keyed by currency code on the wire. Create and update take
`prices`; an entry is a price for one billing period and the Stripe Price id that charges it. The
Stripe Price objects are created out of band — the admin enters ids.

```json
{
  "code": "PLUS_MONTHLY",
  "name": "Cleansia Plus",
  "billingInterval": 1,
  "prices": {
    "CZK": { "price": 199.00, "stripePriceId": "price_…" }
  },
  "discountPercentage": 5,
  "freeCancellationWindowHours": 4,
  "trialPeriodDays": 0,
  "allowsExpressUpgrade": true,
  "expressUpgradesPerMonth": 1
}
```

- `prices` may be **null, empty or partial** — a plan with no price in a currency is "Plus is not on
  sale in that market", a valid state. There is deliberately no "every active currency" rule here,
  unlike the catalogue forms: it would block a benefit edit until the owner had minted a Stripe Price
  for every active currency.
- On update, currencies **not sent keep their rows**; a row is never deleted (deactivate the plan
  instead).
- Refusals: `currency.not_found` (a key naming no currency), `membership.plan.stripe_price_already_used`
  (a Stripe Price id already charging another row, in this payload or in the database — a Stripe Price
  is single-currency), `Required` / `MaxLength` on a blank or over-long id, `MustBePositive` on a
  negative price, `membership.plan.trial_not_permitted` on any `trialPeriodDays` but 0.

**Detail** returns `prices` as `{ "CZK": { "price", "monthlyEquivalentPrice", "stripePriceId" } }` —
an absent key is a currency the plan is not priced in, never a zero. **The paged list** carries the
**platform-default-currency** row's `price` and `monthlyEquivalentPrice` plus `currencyCode`; both
figures are `null` when the plan has no row in that currency (the admin app prints "—"), because "0"
would read as free. The list has no price sort.

---

## Admin — country market content <Badge type="info" text="Admin" />

```
PUT /api/AdminCountry/{countryId}/market-content      { "countryId": "country-id", "insuranceCoverageAmount": 1000000 }
```

**Auth:** `CanUpdateCountry` (rate-limited, `auth` policy; the route id must match the body's)

Authors the one per-country copy figure: the insurance ceiling per booking, a number in the country's
default currency, `null` to state no figure. The country must already have a configuration row —
`country.configuration_missing` otherwise (creating one needs a currency, a language and a VAT rate,
which is not this command's business). A negative amount is `MustBePositive`.

`GET /api/AdminCountry/details/{countryId}` returns `insuranceCoverageAmount`, `hasConfiguration` and
`isDefaultMarket` alongside `isoCode`, `isoAlpha2`, `name` and `isServiced`; the admin country form
disables the Market section with a hint while `hasConfiguration` is false. The paged and overview
`CountryListItem` rows carry `isDefaultMarket` too (last field on both DTOs).

---

## Admin — the default market <Badge type="info" text="Admin" />

```
PUT /api/AdminCountry/{countryId}/default-market
```

**Auth:** `CanUpdateCountry` (rate-limited, `auth` policy; audited)

Flags this country's configuration as **the default market** — what a customer surface pre-selects
before any choice is made (owner ruling 2026-09-13; [ADR-0058](/decisions/adr-0058) amendment). At
most one configuration carries the flag, held by the partial unique index
`IX_CountryConfigurations_IsDefaultMarket_Unique`; promoting a second country **moves** the flag in
one transaction (clear, flush, promote, flush — the `SetDefaultCurrency` shape), and promoting the
current default is a no-op that still answers `200 { "countryId" }`. `Market/GetOverview` marks the
flagged market `isDefault` as soon as it is listed. No body: the route id is the command.

| Refusal | When |
|---|---|
| `country.not_found` | No such country |
| `country.not_serviced` | The country is not switched on as serviced — a default the directory would not list is a pre-selection of nothing |
| `country.market_not_ready` | No configuration row, a configuration naming no currency, one whose currency is inactive — the same gate `…/serviced` applies — or one with no operating company (`OperatorTenantId` null): a default nobody operates would refuse every registration that names no market ([ADR-0061](/decisions/adr-0061) D2) |
| `country.default_market_changed_concurrently` | Two admins promoted at once and this call lost the race on the index; retry or read the detail |

**Related, changed by the same programme:**

- `POST /api/AdminCountry/create` requires `isoAlpha2` (two upper-case letters,
  `country.iso_alpha2_invalid`); `PUT …/update/{countryId}` accepts it optionally and leaves the column
  alone when it is omitted.
- `PUT /api/AdminCountry/{countryId}/serviced` with `isServiced: true` is **gated**: the country must
  have a configuration whose default currency is **active**, else `country.market_not_ready`.
  Switching a country off is never gated. → [Platform expandability — the expansion path](/architecture/platform-expandability#expansion-path)

---

## Admin — currency no-show credit <Badge type="info" text="Admin" />

```
POST /api/AdminCurrency/create               { "code": "EUR", "symbol": "€", "name": "Euro", "loyaltyPointsDivisor": null, "noShowCredit": null }
PUT  /api/AdminCurrency/update/{currencyId}  { "currencyId": "…", "code": "CZK", "symbol": "Kč", "name": "Czech koruna", "loyaltyPointsDivisor": 10, "noShowCredit": 250 }
```

**Auth:** `CanCreateCurrency` / `CanUpdateCurrency`

`noShowCredit` is the apology credit `CancelUnfilledOrders` pays on an order **in this currency** when
its slot arrives with no cleaner; `null` means none is paid in this currency (the sweep still refunds
in full and sends the plain cancellation push). It must be positive when set (`MustBePositive`).
Unlike the loyalty divisor it is **not** an activation gate — a market may open without an apology
credit. `AdminCurrencyDetailDto` and `AdminCurrencyListItem` carry it back; the `CurrencyListItem` /
`CurrencyDetailDto` that travel on order rows do not.

---

## Error keys, and which app must translate them

| Key | Raised by | Reaches |
|---|---|---|
| `membership.plan.not_priced_in_currency` | checkout, subscribe, swap | customer web, Android customer, iOS customer |
| `membership.stripe_customer_currency_locked` | checkout, subscribe | customer web, Android customer, iOS customer |
| `membership.plan.stripe_price_already_used` | admin plan create/update | admin web |
| `country.configuration_missing` | market-content PUT | admin web |
| `country.market_not_ready` | serviced PUT, default-market PUT | admin web |
| `country.not_serviced` | default-market PUT (and the customer quote / subscribe paths, already catalogued there); **since ADR-0061** also `Auth/Register`, `Auth/RegisterEmployee`, `Auth/GoogleAuth`, `Auth/AppleAuth`, `PromoCode/Request`, `Referral/Validate` and guest `Order/CreateOrder` when the named market is not one | admin web; customer web, Android customer, iOS customer; partner web, Android partner, iOS partner (register / social) |
| `country.default_market_changed_concurrently` | default-market PUT | admin web |
| `country.iso_alpha2_invalid` | country create/update | admin web |
| `tenant.not_found` | the same seven anonymous requests, when the named (or default) market has **no operating company** — a configuration defect the directory never lists, so a client that resolved its market from `Market/GetOverview` cannot produce it | customer web, Android customer, iOS customer; partner web, Android partner, iOS partner |
| `order.country_operator_mismatch` | `Order/CreateOrder` — the address's country is served by another operating company than the one the request is scoped to | customer web, Android customer, iOS customer |
| `employee.work_country_operator_mismatch` | `Employee/Approve` — the work country's operating company is not the approving admin's | admin web |

Every key is under `api.*` on the web apps (the interceptor resolves `api.${key}`), `error_*` on
Android and `error.*` on iOS, in all five locales; the parity specs pin the rosters.
