# Platform Expandability & Current-State Doctrine — Tenancy · Currency · Region/Country

> Status: **doctrine (ratified by lead-architect panel, 2026-06-02)** — author + YAGNI + corner-painting
> challengers reconciled; index/reversal facts file-verified. Built on the three analyst current-state
> maps and verified against real code. Documentation/decision only — no code.
> Owner ask: "run analysts + architecture discussion one more time to fully define the platform
> expandability and current situation overall."
>
> This is the canonical reference for the question **"is this entity platform-config, tenant-scoped, or
> country-scoped?"** Where it changes a decision it cross-references **ADR-0001 Addendum A1**.
>
> ### ⚠️ Two of the three decisions it grounded have SHIPPED — verified 2026-08-14
>
> **T-0113 (MembershipPlan tenancy) and the four sibling anonymous catalogs are done.** All six —
> `MembershipPlan`, `Service`, `Package`, `Extra`, `ServiceCity` and `ServiceCategory` — are
> `: Auditable` today, with no `ITenantEntity`, and `MembershipPlanEntityConfiguration` records the
> `(TenantId, Code)` composite being dropped for a platform-wide unique `(Code)`. §7a and §7b below
> still describe the *pre-fix* state in the present tense and still issue marching orders to perform it.
> **Read them as the record of a decision that was carried out, not as work to schedule** — including
> §7b's instruction to the PM to create a ticket "before scheduling", which is the failure this project
> has already paid for once: four lanes dispatched at 24 already-shipped tickets.
>
> **Currency-display (§7c) shipped too, on 2026-09-13** — as ADR-0059, with the customer-market
> programme (ADR-0058, ADR-0060). §2 "Customer surfaces", §5, §7c and §8 describe that state.
>
> **Architect verdict (this pass):** the wider three-axis picture **CONFIRMS** ADR-0001 Addendum A1's
> Option-A ruling for T-0113 and broadens it into a general entity-classification rule (§6). It does
> **not revise** A1. Only one ground-truth correction was needed: there are **40** `ITenantEntity`
> entities, not 41 (§1).
>
> ### The currency axis was rewritten 2026-09-12
>
> §2, and the currency clauses of §0, §3, §4, §5, §6, §7c, §8, §9 and §10, now describe what the
> multicurrency programme shipped: prices authored per currency, `Currency.IsActive` as the market
> switch, the order currency derived from the service address's country, per-currency pay coverage, a
> cleaner's board scoped to the currency they are paid in, and one payout invoice per currency. The
> `Currency.ExchangeRate` column and the conversion path those sections used to describe no longer
> exist, and neither does the interim "the caller names the currency" contract that stood for three days.
> The tenancy material, and the country material except where it names a currency, is unchanged.

---

## 0. TL;DR

Cleansia has **three independent expansion axes**, each at a different maturity level:

| Axis | Mechanism in code | Actually used today? | Verdict |
|---|---|---|---|
| **Tenancy** | `ITenantEntity` on 40 entities + EF global query filter + JWT `tenant_id` | **No** — runs effectively single-tenant (`TenantId = null` everywhere) | **Forward-compat scaffolding** |
| **Currency** | `Currency` platform entity (Code/Symbol/Name/IsDefault/IsActive/LoyaltyPointsDivisor) + per-currency price rows (`ServicePrices`/`PackagePrices`/`ExtraPrices`) + per-record `CurrencyId` on every money-carrying row; **nothing converts** | **Partially** — the whole path is live (order currency from the service address's country, cleaner currency from the work country, per-currency pay coverage, a board scoped to the cleaner's currency, one payout invoice per currency), but CZK is the only active currency; EUR is seeded switched off with no prices | **Real mechanism, single-currency operation — adding a market is data, not code** |
| **Region/Country** | `Country` + `CountryConfiguration` + `CountryInvoiceConfig` platform entities, keyed by `CountryId` | **Partially** — config seeded for ~10 countries; consumed by VAT/tax-id/fiscal/invoice code; but only CZE is `IsServiced` | **Real mechanism, single-country operation** |

The three axes are **separate, not coupled** — but currency is **downstream of country**. Every
currency the platform decides is derived from a country through one chain
(`CountryConfiguration.DefaultCurrencyCode` → `Currency`): an order's from its service address's
country, a cleaner's from their work country. Nothing else decides one — not the customer, not the
caller (a `currencyId` on the wire is checked against the address, never trusted), not the tenant.
Country is independent of tenant; tenant is independent of both. There is no
place in the code where currency is derived from tenant, or where country is derived from tenant.

**The classification rule (the doctrine):** an entity is **platform config** if it is shared catalog/
reference data read on `[AllowAnonymous]` paths (or otherwise global); **tenant-scoped** only if it is
private per-operator data behind authenticated, `tenant_id`-bearing routes; **country-scoped** if it
varies by legal/fiscal jurisdiction. These are orthogonal — an entity can be platform-wide AND
country-keyed (e.g. `CountryConfiguration`), but a single entity should not be both `[AllowAnonymous]`
**and** `ITenantEntity` (the bug class behind T-0113 and the sibling catalogs).

---

## 1. Axis 1 — Multi-TENANCY (forward-compat scaffolding, not operational)

**Mechanism (real):**
- **40 domain entity classes** implement `ITenantEntity` — file-verified by exact-string count of
  `: Auditable, ITenantEntity` across `Cleansia.Core.Domain` (verified sample: `Order`, `Employee`,
  `Service`, `Package`, `Extra`, `ServiceCity`, `ServiceCategory`, `MembershipPlan`, `LoyaltyTierConfig`,
  `PromoCode`, `EmployeePayConfig`, `EmployeeInvoice`, plus the per-tenant key/value store
  `TenantConfiguration : Auditable, ITenantEntity`).
  - **Count correction (was "41"):** the orchestrator's ground truth said 41; the real number is **40**.
    The 41st `ITenantEntity` text occurrence is `Common/ITenantEntity.cs` — the **interface declaration
    itself**, not an entity. Separately, `ProcessedStripeEvent : BaseEntity` deliberately does **not**
    implement `ITenantEntity` (`ProcessedStripeEventRepository.cs:13` notes the global filter does not
    apply to it) — the Stripe idempotency ledger is correctly platform-global, not tenant-scoped. So the
    canonical number is **40 tenant-scoped entity classes**.
- EF global query filter auto-scopes reads: `CleansiaDbContext.ApplyTenantQueryFilters`
  (`CleansiaDbContext.cs:111-179`). The filter is:
  `tenantProvider == null  ||  (currentTenantId == null && e.TenantId == null)  ||  e.TenantId == currentTenantId`.
  The **middle clause** is what makes single-tenant mode work — without it `null == null` is SQL `NULL`
  (not true) and every row would be filtered out.
- `TenantProvider` resolves the tenant **only** from the JWT `tenant_id` claim or an explicit
  `_override`. `SetTenantOverride` is used **only by background services** iterating tenants
  (recurring/cleanup/payments/fiscal/pay-period). **There is no inbound host/subdomain
  tenant-resolution middleware for web requests.**

**Operational reality:** the app runs **effectively single-tenant**. Seed data writes `TenantId = NULL`
on every tenant-scoped row (e.g. `ServiceCities … TenantId … NULL`, `MembershipPlans … TenantId … NULL`).
With no live second tenant, the null-slice **is** the only tenant, so the filter is correct today by
coincidence of single-tenancy, not by design intent for anonymous routes. CLAUDE.md states the contract:
"Backward compatible: null TenantId = single-tenant mode."

**Consequence (the bug class):** any `[AllowAnonymous]` route reading an `ITenantEntity` is correct
**only** while single-tenant. With no JWT, `GetCurrentTenantId()` is null → filter collapses to
`TenantId == null`. The day a second tenant exists: (1) the anonymous read returns only the null-tenant
slice (wrong/empty), and (2) any `TenantId == null` "shared" row leaks to every tenant's anonymous page.
This is exactly T-0113 (`MembershipPlan`) and its four siblings.

**The 40 entities sorted into three buckets (the classification this doctrine acts on):**

| Bucket | Count | Members | Verdict |
|---|---|---|---|
| **1 — Genuinely tenant-owned** (private per-operator operational data; correct as `ITenantEntity`) | 33 | Order, OrderNote, OrderIssue, OrderReview, OrderStatusTrack, OrderPhoto, OrderReceipt, OrderEmployeePay, User, Employee, EmployeeDocument, Address, SavedAddress, Cart, RefreshToken, UserConsent, GdprRequest, UserNotificationPreferences, Device, Dispute, RecurringBookingTemplate, UserMembership, LoyaltyAccount, LoyaltyTransaction, PromoCode, PromoCodeRedemption, ReferralCode, Referral, PayPeriod, EmployeePayConfig, EmployeeInvoice, CompanyInfo, LoyaltyTierConfig | **Keep `ITenantEntity`.** (CompanyInfo + LoyaltyTierConfig are tenant-*level config* but genuinely vary per operator and are reached only behind `tenant_id`-bearing JWTs — they stay.) |
| **2 — Catalog/config that is tenant-scoped-but-shouldn't-be** (the T-0113 + sibling-catalog class) | 6 | **Service, ServiceCategory, Package, Extra, ServiceCity, MembershipPlan** | **Drop `ITenantEntity` → platform config** (Option A). `[AllowAnonymous]` + `ITenantEntity` is the bug; correct today only by single-tenant coincidence. |
| **3 — Infra** (tenancy is the entity's whole purpose) | 1 | **TenantConfiguration** (per-tenant key/value store) | **Keep `ITenantEntity`.** Never anonymous; exists to hold per-tenant overrides. |

(`ProcessedStripeEvent` is the deliberate **platform-global ledger** outside all three buckets — correctly
`: BaseEntity`, never tenant-scoped, with `IgnoreQueryFilters()` as belt-and-braces. It is precedent, not
a defect.)

---

## 2. Axis 2 — Multi-CURRENCY (real mechanism, single-currency operation)

**The model in one sentence: a price is authored per currency, and nothing converts.** `Service`,
`Package` and `Extra` carry no price columns. Prices live in `ServicePrices` (`BasePrice`,
`PerRoomPrice`), `PackagePrices` (`Price`) and `ExtraPrices` (`Price`) — one row per (entry, currency),
unique on `(EntryId, CurrencyId)`. `Currency` has no `ExchangeRate` column; there is no rate anywhere in
the platform and no code path that multiplies one currency into another. The previous design multiplied
a CZK-authored basket by a hand-typed rate with no feed, no history and no per-order snapshot, so editing
the rate silently restated every historical order that referenced it. The owner ruled on 2026-09-08 that
a price is authored per currency, never converted; the fourteen multiplication sites went on 2026-09-09
and the column went with the per-currency price tables. `OrderPricingCalculatorNoConversionTests` is
the guard that nothing scales.

### The currency entity and the market switch {#market-switch}

**Mechanism (real):**
- `Currency : Auditable` (NOT `ITenantEntity`) — **platform config**. Fields: `Code` (citext,
  canonicalised to upper case because it travels onto receipts and fiscal requests), `Symbol`, `Name`,
  `IsDefault`, `IsActive`, `LoyaltyPointsDivisor` (nullable — how much of the currency earns one loyalty
  point; CZK is seeded at 10, a currency with no divisor earns nothing and logs). The admin form
  (`AdminCurrencyDetailDto`) authors the divisor and shows `IsActive`; the `CurrencyDetailDto` that rides
  orders and disputes on every host is `Id`/`Code`/`Name`/`Symbol`/`IsDefault` and nothing else.
- Seeded with **five currencies**: CZK (`IsDefault`, active, divisor 10) and EUR, PLN, GBP, USD (all
  **inactive**, no catalogue prices, no divisor) — the four are there so that every code a seeded
  `CountryConfiguration` names is a real row, which the resolver now insists on. The ten rate-carrying
  rows (USD, GBP, PLN, CHF, SEK, NOK, DKK, HUF, RON, BGN) were removed on 2026-09-09: each was seeded
  active with a hand-typed rate nobody had reviewed, and until the same change any authenticated caller
  could name one on the quote and create paths; the three that came back on 2026-09-13 carry no rate
  and stay off until an admin activates them.
- **A currency is switched on deliberately, and not before it can earn.** `Currency.Create` makes a
  row inactive; `ActivateCurrency` / `DeactivateCurrency` (Admin → Currencies, `CanUpdateCurrency`,
  audited; `POST api/AdminCurrency/activate/{id}` and `deactivate/{id}`) flip it, and the default cannot
  be switched off (`currency.cannot_deactivate_default`). Activation refuses a currency whose
  `LoyaltyPointsDivisor` is unset or not positive (`currency.loyalty_divisor_missing`), and
  `UpdateCurrency` refuses to clear the divisor on an active one with the same key: an order completed
  while the divisor is null earns nothing, permanently, and nothing re-fires the grant when the divisor
  is set later — so the platform refuses to open a market in that state rather than log about it after
  customers have lost points. Switching a currency on makes every catalogue save require a price in it
  (`MustCoverAllActiveCurrencies` on the service, package and extra create/update validators —
  `service.missing_price_for_currency`). `SetDefaultCurrency` refuses a currency that is inactive
  (`currency.invalid`) or that has no price row in any of the three price tables (`currency.not_priced`)
  — "offerable" is both, and the booking path checks the same pair.
- **Offerable is one predicate**, `ICurrencyRepository.IsOfferableAsync`: the currency exists, `IsActive`
  is true, and at least one row in `ServicePrices`, `PackagePrices` or `ExtraPrices` is in it. The
  `QuoteOrder`, `CreateOrder` and `QuotePlusSavings` validators ask it of the currency the address
  country resolved to, and `SetDefaultCurrency`'s promotion gate asks it of the candidate default, so
  the star and the quote cannot disagree about what "offerable" means. On a Currency,
  `IsActive` is the **market switch**, not the soft-delete flag it is on services and packages —
  `DeleteCurrency` hard-deletes, and an in-use currency may be switched off (everything already
  denominated in it is untouched; only new pricing, booking and promotion stop). The admin list carries
  activate/deactivate row actions, and the star (set default) is hidden on a row that is not operated.
- `CurrencyId` is **per-record** on every money-carrying row: `Order` (required;
  `FK_Orders_Currencies_CurrencyId` is ON DELETE RESTRICT), `OrderEmployeePay`, `EmployeePayConfig`,
  `EmployeeInvoice`, `CreditAccount` (one account per customer per currency), `PromoCode` (nullable — a
  fixed-amount code always names one; a percent code with no minimum is global), `EmployeePayoutDetails`
  (nullable — the currency the cleaner's account holds) and the three price tables. **Not per-tenant.**
  `DeleteCurrency` answers `currency.in_use` when any of those tables references the row
  (`CurrencyRepository.IsInUseAsync`), so the RESTRICT foreign keys are never reached as a raw 23503.
- `CurrencyRepository.GetDefaultAsync` returns the `IsDefault` row and throws when there is none. The
  partial unique index `IX_Currencies_IsDefault_Unique` holds exactly-one-default in the database, and
  `SetDefaultCurrency` clears and promotes inside one transaction so the window is unobservable.

### The order currency follows the service address's country {#order-currency}

**The market of an order is the address's country; the market a customer browses in is chosen,
defaulting to the default market** (owner rulings 2026-09-12; ADR-0058). A customer has no currency
of their own: the address they are booking for has a country, and the country has a currency — that
is the whole rule for an order, and it is the cleaner-side rule mirrored: both ends of an order
resolve through `CountryConfiguration.DefaultCurrencyCode`, so a Slovak address is priced in EUR and
taken by a cleaner who is paid in EUR. Before there is an address, a customer surface reads the
**chosen market** instead (§"Customer surfaces" below) and sends its `countryId`; the address
overrides it the moment there is one. → /product/business-rules#market

- **Quote.** `QuoteOrder` and `QuotePlusSavings` take an optional `CountryId` — the service address's
  country once the wizard has one — and resolve the currency from it
  (`ICurrencyResolutionService.ResolveCurrencyForCountryAsync`); with no country the quote is in the
  platform default, which is what the wizard's first step and the home page's quick quote get. A
  country the platform does not service is `country.not_serviced`, judged before the currency is
  resolved; a serviced country with no configured currency is not a case the quote handles — the
  resolver throws (see [currency resolution](#currency-resolution)). An explicit `CurrencyId` on the
  quote wins over the country, because the create path echoes the quote's currency back and the two
  must resolve identically. Whichever way it resolves, the currency must be offerable
  (`currency.invalid`), and that rule runs before pricing so the calculator is never asked to price in a
  currency it cannot. → /api/orders#quote
- **Create.** `CreateOrder` resolves the address (`IOrderAddressResolver.ResolveCountryIdAsync` — the
  saved row's country or the inline one, else the single serviced country) and derives the currency
  from it. A `CurrencyId` the client sends is **checked, not trusted**: one that is not the address
  country's is refused as `currency.invalid` before anything is priced, which is what makes the ruling a
  rule rather than a default. The server then re-prices in the address currency and compares against
  the client's `totalPrice`, so a total quoted in another market fails as
  `order.total_price.not_match`. The resolved row's id is stamped on `Order.CurrencyId`, so the price
  and the stamp cannot disagree.
- **Catalogue.** `GET api/Service|Package|Extra/GetOverview?countryId=` (Customer and Mobile.Customer
  hosts) prices the overview in that country's currency and withholds any entry without a price row or
  a pay config in it; the list items carry `currencyCode`, so a surface labels what it was sent. With
  no `countryId` the overview is in the platform default; a named country the platform does not serve
  (unknown, or not `IsServiced`) has no catalogue and the overview answers an empty list before the
  resolver is asked — a client-supplied id can never turn the anonymous read into an error, and a
  serviced country the seed has not configured throws by the resolver's rule. The Partner host's
  overviews are untouched and stay in the default.
- **Item level.** Prices are authored per currency and nothing converts, so a selected service or
  package with no price row in the resolved currency is refused on quote and create as
  `order.selected_services.invalid` / `order.selected_package.invalid` — a 400, never the calculator's
  throw. An extra without a row is dropped from the line items by the calculator and the factory
  alike, because no extras-level error key exists on the wire.
- **Recurring.** `MaterializeRecurringBookingTemplate` resolves the currency from the template's saved
  address's country, so a standing booking on a Slovak flat materialises EUR orders. The template
  carries no currency of its own and needs none.
- `OrderPricingCalculator` resolves the currency **first** — it is an input to the prices, not a label
  applied afterwards — then reads the `ServicePrices` / `PackagePrices` / `ExtraPrices` rows in it. A
  service line is `BasePrice + PerRoomPrice × (rooms + bathrooms)` from its row; a package or an extra
  is its row's `Price`. Every figure the calculator returns is in that currency; the quote carries
  `currencyId` and `currencyCode` and nothing else about currency.
- **Pay coverage is per currency, and it is a second offerability gate.** `EmployeePayConfig` rows carry
  a `CurrencyId` (the unique index is `(EmployeeId, ServiceId, PackageId, CurrencyId)`, NULLS NOT
  DISTINCT). One predicate, `PayCoverage.Applies(config, employeeId, currencyId)`, serves both the
  catalogue gate and the pay writer: an entry is offered in a currency only when every line has a
  platform-wide pay config **in that currency**, and `CalculateOrderPay` reads only rows in the order's
  currency. A rate in another currency counts for nothing — without the currency term every gate
  admitted a EUR order on the strength of a CZK rate, and the writer then found nothing: an order on
  every board with no pay on any of them. `ApproveEmployee` creates no pay config: it resolves the
  cleaner's currency from the work country (`ResolveCurrencyForCountryAsync`) and refuses approval
  (`employee.pay_config_missing`) while any active catalogue entry lacks a platform-wide rate in it. The
  bulk grade apply is what creates per-employee configs, multiplying the `ServicePrices` /
  `PackagePrices` row in the admin-chosen currency and skipping an entry with no row in it.
  → /product/business-rules#cleaner-pay
- **The promo preview asks in the same currency.** `ValidatePromoCode` takes the quote's `currencyId`
  (null resolves to the platform default) so a code bound to another currency answers
  `CurrencyMismatch` at checkout, and `CreateOrder`'s last rule previews the code again in the address
  currency and refuses the booking (`promo.currency_mismatch`, or whichever reason the preview gives)
  rather than charging a full price the customer did not consent to.

### The cleaner's currency is resolved from country — never from tenant {#currency-resolution}

- `ICurrencyResolutionService.ResolveCurrencyForEmployeeAsync` returns the `Currency` **entity**, never
  null. Chain: **`Employee.WorkCountryId` → `CountryConfiguration.DefaultCurrencyCode` → the `Currency`
  row it names; a null country → platform default.** The same body serves the order side as
  `ResolveCurrencyForCountryAsync(countryId)`. **A named country has no fallback** (owner ruling
  2026-09-12, "throw instead, 100 %"): a country with no `CountryConfiguration` row, a blank
  `DefaultCurrencyCode`, or a code naming no `Currency` row throws `InvalidOperationException` naming
  the country and the code. The column is free text with no foreign key — three characters the seed
  authors — so a typo, or a currency deleted after the country was configured, used to fall through to
  the platform default with an error log; it now fails on every partner money screen, board read and
  invoice approval for that country, because paying a cleaner in the platform default is the outcome
  the ruling forbids and a loud failure is the one that gets the seed fixed. The platform default is
  reached only through a **null** country: an unapproved cleaner with no `WorkCountryId`, or a customer
  quote before an address is known. The lookup is deliberately **not** filtered on `IsActive`: a
  country configured for EUR resolves to EUR as soon as the EUR row exists, switched on or not, rather
  than reading as broken until the market opens.
- **The cleaner's board is scoped to that currency** (owner ruling 2026-09-12: a cleaner is paid in the
  currency of the country they work in). `OrderVisibility.PayableTo(employeeId, currencyId)` — the
  order is in the cleaner's currency, or the cleaner is already on it — is conjoined with the
  preferred-cleaner hold into `OpenTo`, which the available-jobs preview and count, the paged order list
  for a non-admin caller, the browse gate and `TakeOrder` all read; the pending-offer list conjoins
  `PayableTo` alone. A foreign-currency order therefore does not exist from that cleaner's side (a take
  is `order.not_found`, like a held order), so every pay row a cleaner earns is in their currency and a
  period closes into one invoice. `AdminReassignOrder` is deliberately **not** gated — it is the admin
  override — and an order the cleaner is already on stays visible to them whatever its currency.
- Every partner-facing money aggregate is scoped to that currency and labelled with its code: dashboard
  stats, the earnings chart, personal bests, order-distribution money columns, the available-jobs
  headline, pending earnings and My Pay. Counts stay over all orders. My Pay (`GetPeriodPays`) also
  takes an explicit `currencyId` view, so a client that opened it from an invoice shows that invoice's
  currency exactly; each pay row on it carries `currencyCode`.
- **The preferred-cleaner request is gated on the same currency.** A customer may name a cleaner
  only if they have completed an order together **and** the cleaner is paid in the order's currency —
  `CreateOrder` and `ChoosePreferredCleaner` compare `ResolveCurrencyForEmployeeAsync` against the
  order's currency, `CreateRecurringBooking` and `UpdateRecurringBooking` against the saved address's
  country's currency, all as one rule with one key (`order.preferred_employee.not_eligible`, judged after
  the completed-order term). Without it the hold could only lapse: the push would go out, the seat be
  withheld for the whole hold, and the cleaner unable to take a job their board does not show.
- **No money is rendered with a guessed unit.** Every path reads the record's own currency row. The
  order e-mails (`EmailService.cs`) and the customer receipt PDF (`ReceiptService.cs`) render
  `Order.Currency.Symbol`, and when the navigation was not loaded they print the **bare number with no
  unit** (`order.Currency?.Symbol ?? string.Empty`) — an unloaded navigation is a loader omission, not a
  CZK order. The two money-of-record paths **refuse** instead: an invoice PDF with no resolved
  `EmployeeInvoice.Currency` records `PdfGenerationError` (`FileExtensions.CreatePdfData`), and a fiscal
  request for an order with no resolved currency throws before it is built and lands as a recorded
  failed attempt on the receipt row. The `?? "Kč"` fallbacks are gone from every path (§5).
- **The fiscal regime is never guessed either.** The receipt's country comes from
  `Order.CustomerAddress.CountryId`; an order whose country cannot be resolved is refused on the same
  landing as a missing currency — `FiscalCountryCodeOf` throws inside `HandleFiscalAsync`'s try, the
  receipt is marked `FiscalRegistrationFailed`, and the retry job sees it — never registered under the
  Czech authority by default. The receipt-number counter follows the same rule: with no ISO code there
  is no provider key, and `FiscalSequenceScope.Resolve` maps the empty key to the `DEFAULT` issuer scope
  (no annual reset), not to the Czech provider's.

There is **no per-tenant currency setting anywhere**. The only tenant-shaped currency surface is the
per-record `CurrencyId`, and it is populated from the address country (orders), from the pay rows
(invoices) or from the work country (pay configs, dashboards, the board) — never from a tenant config.

### Payroll: one invoice per (employee, period, currency) {#payroll-per-currency}

- The unique index is `IX_EmployeeInvoices_EmployeeId_PayPeriodId_CurrencyId`. `GenerateInvoice` groups
  the cleaner's unassigned pay by currency, allocates every payout reference first, writes one invoice
  per group and flushes them together (`Response.InvoiceIds`); its already-exists guard is per currency.
  The auto-close batch (`PayPeriodBackgroundService`) does the same per cleaner and sends one
  period-closed e-mail per invoice document, because the template carries one attachment. The
  reconciliation sweep's anti-join is per currency, so a half-invoiced pair is still a candidate. An
  invoice's currency is derived from the pay rows it invoices (`EmployeeInvoice.CreateFromOrderPays`),
  never supplied; the old refusal `InvoiceSpansMultipleCurrencies` is gone.
- **The cleaner declares the currency their payout account holds**: `EmployeePayoutDetails.CurrencyId`
  (nullable, FK Restrict), written only by `UpdateBankDetails` (`currency.invalid` when it names no
  currency; absent on the wire means unchanged, because two shipped mobile clients cannot send it yet),
  shown on `MyPayoutDetails`, `MaskedPayoutDetails` and the GDPR export. `ApproveInvoice` runs two
  rules on the record, in this order: **presence** — the record exists, `Scheme` is set and `Status` is
  `Provided`, else `payroll.invoice.payout_details_missing` — and then **currency** — the declared
  currency, or the work country's currency when undeclared (the normal case), equals the invoice's,
  else `payroll.invoice.payout_currency_mismatch`. Presence is judged first so a cleaner with no record
  is told that, not told their currency is wrong. This is ADR-0034 D7's issuance block, relocated from
  invoice generation to approval (the ADR carries a dated correction banner): generation would withhold
  a numbered tax document the cleaner needs, approval only withholds the admin's commitment to
  transfer, and the document is issued on time either way. Approval is the last point the platform can
  refuse: the transfer is keyed by hand in a bank and `MarkInvoicePaid` only records it. There is **no
  payout execution path** (no bank file, no Stripe Connect, no SEPA). → /flows/pay-and-payouts#the-path

### Customer surfaces: every figure is labelled from its payload {#customer-default-currency}

- A customer surface never assumes a currency. The catalogue overviews carry `currencyCode` (the
  named `countryId`'s currency, else the platform default), and the web app has one shared
  `formatMoney(value, currencyCode, locale)`; every quote, order, dispute, credit and membership
  figure is labelled from its payload's `currencyCode`. The Android and iOS customer apps mirror this.
- **Before there is an address, the surface reads the customer's chosen market** (ADR-0058). The
  Customer and Mobile.Customer hosts expose an anonymous `GET api/Market/GetOverview`
  (`MarketListItem`: the serviced country joined to its active configured currency, `isDefault`, the
  alpha-2 the chip prints, and the two copy figures) — one call that replaces the older pair of
  `Country/GetServiced` + `Currency/GetOverview` for every pre-address surface (the currency overview
  is still served for admin-style readers). Each client resolves stored-if-listed → `isDefault` →
  first, persists the ISO code per device (the web in one cookie so SSR and the browser agree), and
  sends the market's `countryId` to the catalogue overviews, the quote, `Membership/GetPlans` and the
  subscribe commands. Address wins from the wizard's address step on. → /product/business-rules#market
- There is **no *currency* picker** on any customer surface — there is a **market selector**, whose
  currency follows: the customer picks a country (navbar/footer pill and quick-quote chip on the web,
  Profile → Preferences → Market and a home-tab chip on mobile), never a unit. SK and DE will share
  EUR but not copy, presets or legal text, which is why the choice is the country. Known residual:
  the order payment sheets on both mobile apps still pin the merchant `countryCode` to CZ (correct —
  it is the merchant's, not the customer's; the Android Plus sheet reads the market's alpha-2, a
  reported deviation).
- **Money figures in customer copy come from the market, not from the translation** (ADR-0060): the
  no-show credit and the insurance ceiling ride `MarketListItem`, every locale string carries a
  placeholder, and `check-booking-policy-parity.mjs` fails a locale that types a figure back in.

**Admin surfaces** follow the same rule. Revenue and payroll reports answer in **one** currency
(`ReportFilter.CurrencyId`, null = platform default, `currency.not_found` for an unknown one) and carry
`currencyCode`; there is no "all currencies" report, because a sum across two currencies is not a
number. The order and invoice lists take a `currencyId` filter, and a sort on a money column with no
currency filter runs *within* currency (led by `CurrencyId`) rather than filing 150 EUR below 3 000
CZK. `IssueCustomerCredit` and `ExpireCustomerCredit` each name their currency (`currency.not_found`
when unknown; issue additionally requires it active): a customer holds one credit account per currency,
the customer detail shows one balance block per account, and a discharge takes one account's whole
balance and leaves the others alone. The catalogue lists are priced in the platform default and carry
its code. → /admin-app/reporting

### What is still bound to the platform default {#default-bound-numbers}

Two numbers are authored in one currency and enforced only on an order in it: the tier floor
`LoyaltyTierConfig.MinimumOrderAmountForDiscount` (1000; no floor applies elsewhere) and the minimum on
a promo code that names no currency (on an order in any other currency the validate preview answers the
`CurrencyMismatch` error code and the create path refuses the booking with `promo.currency_mismatch`).
Promoting a different default with `SetDefaultCurrency` re-denominates both **and moves the default
market** (the pre-selection is the market on the default currency, ADR-0058 D2) — an owner-level event,
not an admin click. The no-show credit left this list in 2026-09: it is `Currency.NoShowCredit`,
authored per currency (CZK 250, others null = none paid), paid by `CancelUnfilledOrders` in the order's
own currency. Loyalty earning is per currency through `Currency.LoyaltyPointsDivisor`; Plus is priced
per currency through `MembershipPlanPrice` (ADR-0059); `IssueCustomerCredit.SanityCap = 10 000` is a
unit-free typo guard in whatever currency the grant names; `CountryConfiguration.RefundStripeFixedFee`
and `CountryConfiguration.InsuranceCoverageAmount` are numbers in the country's own currency, the first
deducted only from a refund in it, the second only ever printed. The figures and the reasoning live on
the business-rules page. → /product/business-rules#money-constants

**Verdict:** the **per-record currency mechanism is real and wired end-to-end** — entity, per-currency
price rows, order currency from the address country, cleaner currency from the work country and a
board scoped to it, per-currency pay coverage, one invoice per currency, declared payout currency,
labelled DTOs on every host. Adding a market is data, not code (§8). There is no conversion half, and
it is not coming back.

**Operation is single-currency and the platform enforces it** rather than merely happening to be it:
CZE is the only serviced country, so every address resolves to CZK; CZK is the only active currency and
EUR is seeded switched off and unpriced, so a booking could not be priced in anything else even if a
second country were switched on — and the default cannot be moved to a currency the catalogue is not
priced in. No runtime "Kč" literal remains (§5): a record with no currency row renders a bare number
on the display paths and is refused on the money-of-record paths, and nothing assumes the platform is
CZK-only — `MembershipPlan`, the last structurally CZK-bound entity, lost its `…Czk` columns to
`MembershipPlanPrice` in 2026-09 (ADR-0059).

---

## 3. Axis 3 — Multi-REGION / COUNTRY (real mechanism, single-country operation)

**Mechanism (real):**
- `Country : Auditable` (NOT `ITenantEntity`) — **platform config**. Has `IsoCode`, `IsServiced`
  (operate-here flag, distinct from `IsActive` admin-catalog flag), translations
  (`Country.cs`). Seeded with ~45 countries; **only CZE has `IsServiced = true`**
  (`insert_seed_data.sql:82`; all others `false`).
- `Language : BaseEntity` (NOT `ITenantEntity`) — platform config.
- `CountryConfiguration : Auditable` (NOT `ITenantEntity`), keyed by `CountryId`
  (`CountryConfiguration.cs`). Repo `GetByCountryIdAsync` (`CountryConfigurationRepository.cs`).
- `CountryInvoiceConfig : BaseEntity` (NOT `ITenantEntity`), keyed by `CountryId`
  (`CountryInvoiceConfig.cs`). Repo `CountryInvoiceConfigRepository.cs`.
- `Employee.WorkCountryId` — the jurisdiction a cleaner is approved to work in; set at admin approval
  (`ApproveEmployee.cs:54-60, 114`, required + must be `IsServiced`). Distinct from `NationalityId`
  (passport) and `Address.CountryId` (residency) — see the Employee XML doc (`Employee.cs`).
  Employee also still carries a `PreferredCurrencyCode` column, but it overrides nothing: its setter
  (`UpdatePreferredCurrency`) has no caller, nothing money-related reads it — `GenerateInvoice` and
  `PayPeriodBackgroundService` say so explicitly, the invoice currency comes from the pay rows — and
  its only reader is the GDPR export. Whether the column survives is an open owner question.

**What per-country config actually DRIVES today (all verified consumers):**
1. **The market's currency** — `CountryConfiguration.DefaultCurrencyCode` is step 2 of the one
   currency-resolution chain (§2), on both sides of an order: it is the currency a booking at an address
   in this country is priced, charged and stamped in, the currency the country's catalogue overview is
   shown in, and the currency a cleaner working in this country is paid in, sees their board in and is
   approved against. It has to name a real `Currency` row — a serviced country with no row, a blank
   code or a code naming no currency makes the resolver **throw** rather than fall through (owner
   ruling 2026-09-12), so an unconfigured serviced country is a deploy-blocking defect; nothing in the
   platform writes it — the seed authors it (CZE→CZK, SVK→EUR, POL→PLN) and must for every serviced
   country. Its other reader is the refund fee rule (item 7).
2. **VAT calculation** — `VatCalculator.Calculate` reads `CountryConfiguration.StandardVatRate` (gross-
   inclusive formula); returns `NotApplicable` if `countryConfig == null` or company isn't a VAT payer
   (`VatCalculator.cs:14-34`).
3. **Tax-ID / registration-number validation** — `TaxIdValidator` reads
   `RegistrationNumberRequired/Format` and `VatNumberRequired/Format` regexes from
   `CountryConfiguration` (`TaxIdValidator.cs`). Seed has real per-country regexes (CZ IČO `^\d{8}$`,
   PL NIP `^\d{10}$`, etc.).
4. **Fiscal enforcement policy** — `FiscalRetryService.ResolveEnforcementModeAsync` reads
   `CountryConfiguration.FiscalEnforcementMode` (`FiscalRetryService.cs:96-107`); `None` for CZ today,
   `BlockingOnline` for DE/AT/ES (`FiscalEnforcementMode.cs`).
5. **Invoice formatting** — `CountryInvoiceConfig` drives `VatRequired`, `VatRate`,
   `DigitalSignatureRequired`, `EInvoiceFormat` (PDF / PDF+XML for IT), `LegalDisclaimerTemplate`,
   `AdditionalFieldsJson` on the payroll PDF (`RegenerateInvoicePdf.cs:105-123`,
   `FileExtensions.CreatePdfData`). Seeded for ~10 countries with localized disclaimers + VAT rates.
6. **Date format** — `CountryConfiguration.DateFormat` on the invoice PDF (`RegenerateInvoicePdf.cs:85-90`).
   Also carries `TimeZoneId`, `PhonePrefix`, `DefaultPaymentGateway`, `DefaultLanguageCode`.
7. **Stripe refund fee** — `IssuePartialRefund` deducts `RefundStripeFeeRate` (unit-free) plus
   `RefundStripeFixedFee`, a number in the country's `DefaultCurrencyCode`. The fixed part is deducted
   only when the order's currency code equals the country's — which it does by construction now that
   the order's currency comes from the same column; the guard covers an order stamped before the
   country's code was re-pointed at another currency (a code naming no row no longer falls through —
   it throws), and there the fixed part is absorbed. Dormant today: no production writer sets either
   figure.
8. **The market directory and the customer copy** — `Market/GetOverview` (ADR-0058) lists a serviced
   country only when `DefaultCurrencyCode` names an active currency, prints `Country.IsoAlpha2` on the
   market chip, and carries `CountryConfiguration.InsuranceCoverageAmount` (ADR-0060 — the insurance
   ceiling the mobile trust badge and FAQ state, a number in the country's currency; null today, the
   admin country form's Market section writes it — the **only** column of `CountryConfiguration` with
   an admin writer). `SetCountryServiced(true)` refuses `country.market_not_ready` without that active
   currency.

**Verdict:** the **per-country mechanism is real and consumed by live VAT/tax/fiscal/invoice code**, and
seed data exists for ~10 countries. But **operation is single-country**: only CZE is `IsServiced`, so
order creation rejects any non-serviced country (`OrderAddressResolver`, defaults to the single
serviced country when exactly one exists) and the quote refuses one as `country.not_serviced`.
Multi-country is "flip `IsServiced` + ensure `CountryConfiguration`/`CountryInvoiceConfig` rows exist,"
not a code change — and because the country's `DefaultCurrencyCode` is now what prices every booking
at its addresses, flipping a country whose currency is not switched on, priced and covered by pay
configs makes every booking there refuse as `currency.invalid`. The §8 checklist is the order to do it
in.

---

## 3b. The complete anonymous-read surface (verification pass, 2026-06-02) — table + two panel-missed reads

A second analyst pass enumerated **every** `[AllowAnonymous]` read on the customer
(`Cleansia.Web.Customer`) and mobile-customer (`Cleansia.Web.Mobile.Customer`) hosts and traced each to
its domain entity. The base controller is `[Authorize]` (`CleansiaApiController.cs:13`), so only
explicitly-marked actions are anonymous. **Host asymmetry confirmed:** the web host marks ServiceCity
`[AllowAnonymous]` (`Web.Customer/ServiceCityController.cs:19`); the **mobile host does NOT**
(`Mobile.Customer/ServiceCityController.cs:13`, plus Country/Language/PromoCode/Loyalty are not anonymous
on mobile). So the anonymous catalog set is **web = {Service, Package, Extra, ServiceCity, MembershipPlan,
Country, Language}**, **mobile = {Service, Package, Extra, MembershipPlan}**.

| Endpoint (host) | Entity | `ITenantEntity`? | Filter-reliant? | Collapse failure mode | Verdict |
|---|---|---|---|---|---|
| `Service/GetOverview` (web+mobile) | Service (+Category) | Yes (`Service.cs:9`) | Yes (`GetServiceOverview.cs:20`) | null-slice only; wrong/empty multi-tenant; null row leaks | **BUG — catalog batch** |
| `Package/GetOverview` (web+mobile) | Package | Yes (`Package.cs:8`) | Yes | same | **BUG — catalog batch** |
| `Extra/GetOverview` (web+mobile) | Extra | Yes (`Extra.cs:20`) | Yes (`GetExtraOverview.cs:25`) | same | **BUG — catalog batch** |
| `ServiceCity` GET (**web only**) | ServiceCity | Yes (`ServiceCity.cs:18`) | Yes (`GetServiceCities.cs:21-23`) | same (mobile requires auth) | **BUG — catalog batch** |
| `Membership/GetPlans` (web+mobile) | MembershipPlan | Yes (`MembershipPlan.cs:24`) | Yes (`GetMembershipPlans.cs:42`) | plans-missing + leak + write-side webhook mismatch | **BUG — T-0113 (Option A)** |
| `Country/GetOverview`+`GetServiced` (**web only**) | Country | **No** (`Country.cs:7`) | No | none | **CORRECT (platform config)** |
| `Market/GetOverview` (web+mobile, added 2026-09-13, ADR-0058) | Country + CountryConfiguration + Currency | **No** — all three platform config | No | none; the read omits and logs an unready country, never throws | **CORRECT (platform config)** |
| `Language/GetOverview` (**web only**) | Language | **No** (`Language.cs:6`) | No | none | **CORRECT (platform config)** |
| `Order/Lookup`+`LookupBatch` (web+mobile) | Order (+Service/Package) | Yes | Yes but **credentialed** by `DisplayOrderNumber`+`CustomerEmail` (`LookupOrder.cs:51-53`) | collapse fails it **shut** (hides), cannot enumerate | **CREDENTIALED — different risk class (this is the backlog's "BSP-9" / T-0123 LookupBatch item)** |
| `Order/Quote` (web+mobile) | Service/Package/Extra (pricing) | Yes | Yes — loads catalog by id (`OrderPricingCalculator.cs:41-48`) | resolves only null-slice catalog when pricing anonymously | **BUG — catalog batch (pricing path), PANEL-MISSED** |
| `Referral/Validate` (web+mobile) | ReferralCode + User | Yes (`ReferralCode.cs:14`, `User.cs:12`) | Yes (`ValidateReferral.cs:43-52`) | validates only null-tenant codes/users at sign-up | **BUG — anon tenant-scoped read, PANEL-MISSED** |

**Two reads the catalog batch must NOT forget (both are anonymous reads of `ITenantEntity` data the
original prose did not enumerate):**
- **`Order/Quote`** is a catalog-batch sibling on the **pricing path** — it loads Service/Package/Extra
  under the collapse via `OrderPricingCalculator` (`:41-48`). Fixing the four `GetOverview` reads but not
  Quote leaves catalog tenancy half-fixed (listing correct, pricing still null-slice). Quote's read is
  resolved by making those entities platform config (Option A) — no extra work once the entities flip,
  but the test plan must include the anonymous-Quote path.
- **`Referral/Validate`** is an anonymous read (modelled as POST; `ValidateReferral.cs:10-13` says
  "Read-only validation … Modelled as IQuery") of `ReferralCode` + `User`, both `ITenantEntity`, at
  sign-up before any tenant is known. **ACCEPTED AS-IS (panel ruling)** — known member of the
  anonymous-`ITenantEntity` class, but it **fails shut** on the null-tenant collapse (invalid code →
  `IsValid:false`), is rate-limited (`auth` bucket), has no zero-row customer page, and no leak of
  consequence (a referral-code probe). **NOT in the catalog batch, no ticket opened.** Documented here so
  it is named, not silent — re-adjudicate only if it ever gains a customer-facing zero-row surface.

**Confirmed NOT in the class (auth-gated):** `PromoCode/Validate` requires
`[Permission(Policy.CanRedeemPromoCode)]` on **both** hosts (`Web.Customer/PromoCodeController.cs:16`,
`Mobile.Customer/PromoCodeController.cs:16`). `PromoCode` is `ITenantEntity` but is read under a JWT, so
the filter works — it is **not** an anonymous-tenant bug despite carrying a `CurrencyId`.

---

## 4. The relationship between the three axes (SEPARATE, not coupled)

```
TENANT  ──(JWT tenant_id)──►  EF global filter on ITenantEntity        [private per-operator data]
COUNTRY ──(CountryId)──────►  CountryConfiguration / CountryInvoiceConfig   [legal/fiscal jurisdiction]
            │
            ├──(Address.CountryId → DefaultCurrencyCode, order side)───┐
            └──(Employee.WorkCountryId → DefaultCurrencyCode, cleaner side)──┴──► CURRENCY ──(price rows per currency + per-record CurrencyId)──► price / label / board
                                                                     nothing converts; no country known (a first-step quote) means the platform default;
                                                                     a named country with no configured currency throws — it never defaults
```

- **Currency is always derived from COUNTRY**, never from tenant and never from a person
  (`CountryConfiguration.DefaultCurrencyCode → Currency`, in `CurrencyResolutionService`). The order
  side reads the service address's country; the cleaner side reads the work country (§2). A customer
  has no currency of their own, and a `currencyId` a client sends is checked against the address, not
  trusted.
- **Country is independent of tenant.** `Country`/`CountryConfiguration`/`CountryInvoiceConfig` are all
  platform config (no `ITenantEntity`). A tenant does not own a country; a country's VAT/fiscal rules are
  the jurisdiction's, shared by all operators in it.
- **Tenant is independent of both.** Tenancy partitions *who owns the row*; currency/country describe
  *what jurisdiction & denomination the row is in*. A single tenant can (in the forward-compat design)
  operate in multiple countries/currencies; a single country/currency can be shared by multiple tenants.

This separation is the load-bearing fact for the pending decisions: **fixing the anonymous-tenant bug
class does NOT touch currency or country**, because currency display was never per-tenant to begin with.

---

## 5. Where CZK / "Kč" is hardcoded vs configurable

| Location | Hardcoded? | Nature |
|---|---|---|
| `Constants.Currency.Czk = "CZK"` | declared, **no reader** — no render or register path falls back to it | dead constant |
| `CurrencyResolutionService` — a named country's currency | **no fallback** — a serviced country with no `CountryConfiguration`, a blank `DefaultCurrencyCode` or a code naming no `Currency` row throws `InvalidOperationException` (owner ruling 2026-09-12); only a null country reads the platform default | fails closed |
| `ReceiptService.cs` — the fiscal request's currency | **no fallback** — a receipt whose order has no resolved currency is recorded as a failed fiscal attempt, never registered as CZK | fails closed |
| `ReceiptService.cs` — the fiscal regime and the receipt-number counter scope | **no fallback** — an order whose country cannot be resolved is refused on the same landing (recorded failed attempt, retried by the job), never declared to the Czech authority; the counter resolves an empty provider key to the `DEFAULT` issuer scope, not `cz-eet2` | fails closed |
| `ReceiptService.cs` — the receipt PDF symbol | `order.Currency?.Symbol ?? string.Empty` — the bare number, no unit | configurable, no hardcoded fallback |
| `EmailService.cs` — order e-mail amounts | `order.Currency?.Symbol ?? string.Empty` — the bare number, no unit | configurable, no hardcoded fallback |
| `FileExtensions.CreatePdfData` | **no fallback** — an invoice with no resolved currency refuses to render and records `PdfGenerationError` | fails closed |
| Plan prices (`MembershipPlanPrices`) | **not hardcoded** — one row per (plan, currency) carrying the price and the Stripe Price id; `MembershipPlan` has no price column (ADR-0059) | authored per currency, never converted; a plan with no row is not on sale in that market |
| Catalogue prices (`ServicePrices`, `PackagePrices`, `ExtraPrices`) | **not hardcoded** — one row per (entry, currency), each row carries `CurrencyId` | authored per currency, never converted |
| `Currency.NoShowCredit`, `CountryConfiguration.InsuranceCoverageAmount` | **not hardcoded** — authored per currency / per country, null = none; customer copy carries a placeholder and formats the market's figure (ADR-0060) | authored, never converted |
| `LoyaltyTierConfig.MinimumOrderAmountForDiscount`, a promo minimum on a code with no `CurrencyId` | **bound to the platform default** — enforced only on an order in it | §2 "What is still bound to the platform default" |
| Customer clients — the order payment sheets' merchant `countryCode` | `CZ` as the **merchant's** country per Stripe, not the customer's | correct; the Android Plus sheet reads the market's alpha-2 instead (reported deviation, ADR-0058) |
| Customer locale copy | **no money figure and no currency word** in any placeholder key — pinned by `check-booking-policy-parity.mjs` across five locales × three clients | data, not content |

**Net:** "Kč" does not appear at runtime at all (`grep '"Kč"'` over `Cleansia.Core.AppServices`
finds only a comment). A record with no `Currency` row renders a bare number on the two display paths
(order e-mails, receipt PDF) and is refused on the two money-of-record paths (invoice PDF, fiscal
request); the fiscal regime is likewise never defaulted. The only path is the configurable one —
record `CurrencyId` → `Currency.Symbol/Code`, or the price row in the order's currency. No entity is
structurally CZK-bound any more; the two default-bound numbers are CZK today because CZK is the
default, not because they name it.

---

## 6. Classification rule for entities (use this for every new entity)

Decide along the orthogonal axes:

1. **Platform config vs tenant-scoped:**
   - **Platform config** (NOT `ITenantEntity`) if it is *shared catalog / reference data*, especially if
     read on **any `[AllowAnonymous]` path**. Precedent: `Currency`, `Language`, `Country`.
   - **Tenant-scoped** (`ITenantEntity`) only if it is *private per-operator data behind authenticated,
     `tenant_id`-bearing routes*. Precedent: `Order`, `Employee`, `EmployeeInvoice`, `PromoCode`.
   - **Hard rule (ADR-0001 Addendum A1, D-A1.1):** an entity must **never** be both `[AllowAnonymous]`
     **and** `ITenantEntity` with no spoof-resistant inbound tenant-resolution. Today no such resolution
     exists (the `Host` header is client-controlled → S3), so anonymous catalogs **must** be platform
     config.
2. **Country-keyed?** Add a `CountryId` (platform-config, keyed by country) when the data varies by
   legal/fiscal jurisdiction (VAT, tax-id format, fiscal mode, invoice template). Precedent:
   `CountryConfiguration`, `CountryInvoiceConfig`. This is **independent** of axis 1 — country-keyed
   config is still platform config, not tenant-scoped.
3. **Currency-bearing?** Add a per-record `CurrencyId` on every *money-carrying record* — orders, pay
   rows, pay configs, invoices, credit accounts, payout details, fixed-amount promos — stamped at write
   time from the address country (orders), from the rows being aggregated (invoices) or from the work
   country (pay configs, dashboards), never from tenant and never from a customer preference, because
   no such preference exists. A **catalogue entity carries no price**: its price
   lives in a sibling row table keyed `(EntryId, CurrencyId)`, one row per currency the platform
   operates in, and nothing converts between rows. Precedent: `ServicePrice`, `PackagePrice`,
   `ExtraPrice`. A scalar that cannot be per-currency (the no-show credit, the tier floor) is bound to
   the platform default and enforced only on orders in it — say so where it is declared.

---

## 7. The pending decisions, resolved under this doctrine

### 7a. T-0113 (MembershipPlan tenancy) — Option A is CONFIRMED, and broadened

> **SHIPPED.** The paragraph below describes the state *before* the fix. `MembershipPlan` is
> `: Auditable` today and the index swap is in `MembershipPlanEntityConfiguration`.

`MembershipPlan` was `: Auditable, ITenantEntity` served on
`[AllowAnonymous] GetPlans` (`MembershipController.cs:58`, customer + mobile.customer hosts), with a
unique index `(TenantId, Code)` and seed rows at `TenantId = NULL`. It is the textbook instance of the
§1 bug class.

**This doctrine CONFIRMS ADR-0001 Addendum A1's Option A** (drop `ITenantEntity` → platform config),
now with the full three-axis picture behind it:
- It matches the established **platform-config precedent** (`Currency`/`Language`/`Country` are already
  not `ITenantEntity` — §2/§3). Making MembershipPlan platform config puts it in the same bucket as the
  other anonymous-readable reference data.
- The owner's specific worry — *currency display for plans* — is **moot**: plans are CZK-only by design
  (canonical price + charge currency live in Stripe via `StripePriceId`; the `*Czk` fields are a display
  mirror, `MembershipPlan.cs:36-71`). There is **no `CurrencyId` on MembershipPlan and none is needed**
  (§7c). Currency display has never depended on the tenant provider, so dropping the tenant dimension
  changes nothing about currency.
- Option B (host/subdomain tenant resolution) would require **building spoof-resistant resolution
  middleware that does not exist**, for one anonymous read of a **zero-row-at-launch** table, and would
  trust the client `Host` header on an unauthenticated route (S3). Strictly worse.

**Marching order:** proceed with Addendum A1's implementation contract verbatim
(`MembershipPlan.cs:24` drop `ITenantEntity`; `MembershipPlanEntityConfiguration.cs:55-56`
`(TenantId,Code)` → `(Code)`; no DbContext/handler/repo change; ef-migration folds into the owner's
regenerated Initial; ACs = read parity, no-footgun, write-side parity AC6, host-boot test, structural
"not ITenantEntity" test; `LoyaltyTierConfig` untouched — no anonymous read path). **No revision to the
A1 ruling is warranted.**

### 7b. The four sibling anonymous catalogs (Service / Package / Extra / ServiceCity) — marching orders

> **SHIPPED.** All five entities named below — the four siblings plus `ServiceCategory` — are
> `: Auditable` today. The marching order was carried out; it is not outstanding work.

All four were `: Auditable, ITenantEntity` **and** served `[AllowAnonymous]` on the customer
host (`ServiceController`, `PackageController`, `ExtraController`, `ServiceCityController`, all line ~14).
`ServiceCategory` is **also** `ITenantEntity` — confirm whether it is reachable from any anonymous read
path; if yes it joins the batch, if no it stays untouched (parity with the `LoyaltyTierConfig` carve-out).

**Marching order (per ADR-0001 Addendum A1 D-A1.4):** apply the **same Option-A treatment** — drop
`ITenantEntity`, make them platform config, swap any `(TenantId, …)` unique indexes to drop `TenantId`.
They are the same bug class as MembershipPlan and correct today only by single-tenant coincidence. Keep
them in **their own batch** (do NOT fold into T-0113 — scope discipline avoids the double-fix collision
the T-0113 ticket itself warns of). One doctrine (this doc + A1 D-A1.1) governs both; cross-reference it.

> **RESOLVED — the label "BSP-9" was overloaded, and both bodies of work have since shipped.**
> It meant two things at once: the anonymous `Order/LookupBatch` secret-pair and cap hardening (in
> `LookupOrderBatch.cs`), and — per ADR-0001 Addendum A1 D-A1.4 — the four sibling anonymous catalogs.
> The worry recorded here was that the catalog fix would fall through the naming gap for want of a
> dedicated ticket. It did not: all six catalog entities are `: Auditable` today, and the batch lookup
> is capped at 10 items and keyed on the internal GUID rather than the human-typed number. Kept as the
> record of a real risk that was handled, not as an action.

### 7c. Multi-currency PLANS — SHIPPED 2026-09-13 (ADR-0059)

> This section used to be a deferred option sketch. The owner ruled on 2026-09-12 (*"Cleansia Plus
> should be per currency and per region"*) and it shipped as ADR-0059; what follows is the current
> state, and the sketch's option (a) is the one that was built.

- **`MembershipPlanPrices`** — one row per (plan, currency), unique on `(MembershipPlanId, CurrencyId)`
  and on `StripePriceId`, carrying the charge for one billing period and the **Stripe Price id in that
  currency** (Stripe Prices are single-currency; the admin enters one id per row, Stripe objects are
  created out of band). `MembershipPlan` has no price column; the `*Czk` fields are gone from the
  entity and from every wire.
- **The display currency is resolved from a country, exactly as the catalogue's is** — the customer's
  chosen market (ADR-0058), which the wizard's Plus step follows even inside a booking priced in the
  address's currency, because a subscription belongs to the customer, not to the booking. Never from
  tenant.
- **`UserMembership.CurrencyId`** records the currency the subscription was created in and never
  changes; a swap picks the target plan's row in it. The benefits are currency-free, so one
  subscription serves orders in every currency.
- **A plan with no row in a currency is not on sale in that market** — the admin form does not require
  every active currency (unlike the catalogue forms), `ActivateCurrency` does not check plans, and the
  clients render "Plus is not available in your market yet".
- **Still decoupled from tenancy.** `MembershipPlan` and `MembershipPlanPrice` are platform config
  (structural test); `UserMembership` stays tenant-scoped.

---

## 8. The expansion path (single-tenant → multi-tenant → multi-region/currency, WITHOUT a rewrite) {#expansion-path}

The scaffolding is deliberately built so each axis flips on independently:

1. **Today — single everything.** One implicit tenant (`TenantId = null`), CZK the default and the only
   active currency, CZE serviced. All mechanisms present and exercised on the single-value path.
2. **Multi-COUNTRY and multi-CURRENCY are one step, and the order inside it matters.** A market is a
   country plus its currency, and switching one on is data, not code. What opening SK (or PL) actually
   takes, in the order that keeps every intermediate state refusing cleanly rather than half-working:
   1. **The currency row exists and is switched off.** EUR is seeded that way; a new one is created
      through Admin → Currencies and is born inactive.
   2. **`CountryConfiguration.DefaultCurrencyCode` names it** (SVK → `EUR` is seeded). Nothing in the
      platform writes this column; the seed or a SQL script does (the admin country form writes only
      the market-content field, step 8). Until it names a real row every resolution for that country —
      a booking at an address there, a cleaner approved for it, their board and every money screen —
      throws rather than defaulting (owner ruling 2026-09-12), which is why this step precedes
      servicing the country and not the other way round. The country's **`IsoAlpha2`** must be set —
      it is seeded for every row and required on the admin create form; the market chip prints it.
   3. **Author `LoyaltyPointsDivisor`** on the currency form. `ActivateCurrency` refuses without it
      (`currency.loyalty_divisor_missing`) — **gate 1**.
   4. **Price the catalogue in it** — every service, package and extra that should be sold there needs
      a row in `ServicePrices` / `PackagePrices` / `ExtraPrices`. An entry without one is withheld
      from that country's overview and refused on quote and create; once the currency is active every
      later catalogue save requires the row (`service.missing_price_for_currency`).
   5. **Add a platform-wide `EmployeePayConfig` per entry in it**, or the entry is withheld from that
      currency's catalogue by the pay-coverage gate, and no cleaner in that country can be approved
      (`employee.pay_config_missing`).
   6. **Switch the currency on** (`ActivateCurrency`). It is now offerable.
   7. **Optional — price Plus in it:** a `MembershipPlanPrices` row per plan (the price and the Stripe
      Price id the owner minted in that currency) on the admin plan form. Without them Plus is simply
      not on sale in that market (ADR-0059); nothing gates on it.
   8. **Optional — the market content:** `NoShowCredit` on the currency form (null = no apology credit
      is paid in that currency) and `InsuranceCoverageAmount` on the country form's Market section
      (null = the copy names no figure) (ADR-0060). Nothing gates on either.
   9. **Flip `Country.IsServiced`**, with `CountryInvoiceConfig` in place — **gate 2**:
      `SetCountryServiced(true)` refuses `country.market_not_ready` unless the configuration from
      step 2 names a currency that step 6 switched on, because `Country/GetServiced` feeds the
      wizard's address step and an unpriceable serviced country is a customer-visible dead end
      (ADR-0058 D7). Switching off is never gated. On success the country appears in
      `Market/GetOverview`, the market selector lists it and the chip can show it; VAT, tax-id
      validation, fiscal mode, invoice template and date format activate through the existing
      consumers (§3).

   Gate 2 makes flipping `IsServiced` before step 6 impossible rather than merely pointless. From
   step 9 on, a booking at a Slovak address is quoted and charged in EUR with no client change, a
   customer who chooses the SK market browses the EUR-priced catalogue before entering any address,
   cleaners approved for SK are paid in EUR, see only EUR orders on their board, declare EUR (or
   nothing) on their payout account, and their periods close into EUR invoices. **What stays bound to
   the platform default** until someone decides otherwise: the tier floor and any promo minimum on a
   code without a currency (§2, "What is still bound to the platform default"); promoting the default
   also moves the default market.
3. **Multi-TENANT (the one axis that needs new infrastructure).** The `ITenantEntity` filter + JWT claim
   already scope authenticated reads. The **single missing piece** is **spoof-resistant inbound tenant
   resolution for anonymous routes** (vetted-proxy header / allow-listed host registry / SNI pinning —
   never the raw `Host` header). This is *unavoidable the day ANY anonymous catalog goes per-tenant*, so
   building it later loses nothing — which is exactly why T-0113/the catalogs drop `ITenantEntity` now
   rather than half-build resolution for a zero-row table.

**The doctrine that keeps this rewrite-free:** keep **anonymous-readable catalogs as platform config**
(not tenant-scoped) until the day the product genuinely needs per-tenant catalogs — and on that day,
build the spoof-resistant resolver once and re-tenant. Currency and country need no such gate because
they were never tenant-coupled.

> **Reversal cost is NOT uniform across the six catalogs (corrected per architect panel, file-verified).**
> "Symmetric and cheap" is true **only for `MembershipPlan`** — zero rows at launch, `(TenantId, Code)` →
> `(Code)` is a clean inverse. For the **populated** catalogs the reverse (re-tenant) is a **constrained
> `TenantId` backfill**, not an index swap: `ServiceCategory`/`Extra` have `(TenantId, Slug)` unique;
> **`Service` and `Package` have no unique index at all** (nothing to swap forward; reverse would *add*
> one that never existed); `ServiceCity` has only a non-unique `(CountryId, Name)` and no tenant index.
> The forward flip is safe (platform-config parent / tenant-scoped `OrderService` child = the
> Currency-on-Order pattern), but the reverse is **forward-safe, reverse-constrained**: re-tenanting must
> preserve Order↔Service tenant agreement (`OnDelete.Restrict` + the `PackageService` M2M block a naive
> re-slice). The catalog-batch ticket MUST carry this per-entity index table + the FK note.

---

## 9. Cross-references

- **ADR-0001 Addendum A1** (`docs/decisions/adr-0001.md:1134-1184`) — the binding
  ruling this doctrine confirms and broadens. D-A1.1 is the hard rule; D-A1.4 routes the sibling
  catalogs (see the §7b naming discrepancy).
- **T-0113** (`agents/archive/2026-08/backlog/tickets/T-0113-lg-sec-05.md`) — MembershipPlan build ticket (awaiting owner
  approval; this doctrine recommends proceeding unchanged).
- **T-0123** (`agents/archive/2026-08/backlog/tickets/T-0123-prod-config.md`) — note its "BSP-9" is the `LookupBatch`
  hardening, a *different* finding from the four-catalog fix.
- **Security law S3** (`docs/architecture/security-rules.md`) — anonymous routes must not serve
  tenant-scoped data without deliberate, spoof-resistant resolution.
- **Real-code anchors:** `Currency.cs`, `ServicePrice.cs` / `PackagePrice.cs` / `ExtraPrice.cs`,
  `CurrencyRepository.cs` (`IsOfferableAsync`, `IsInUseAsync`), `PayCoverage.cs`,
  `CurrencyResolutionService.cs`, `OrderPricingCalculator.cs`, `Country.cs`, `Language.cs`,
  `CountryConfiguration.cs`, `CountryInvoiceConfig.cs`, `CleansiaDbContext.cs:111-179`,
  `TenantProvider.cs`, `MembershipPlan.cs:24`, `Employee.cs`, `ProcessedStripeEvent.cs:11-20`,
  `ProcessedStripeEventRepository.cs:12-19`, `insert_seed_data.sql` (countries :76+, currencies
  :517-541, price rows :748-810, invoice configs :940+, country configs :1030+, plans :1819+).

---

## 10. Architect ratification (this pass — the six asks, answered)

Recorded by the authoring architect on top of ADR-0001 Addendum A1, having verified every load-bearing
premise against real code. This is the single scannable sign-off.

1. **Current-state map (3 axes):** done — §0 table + §1/§2/§3. **Tenancy = forward-compat scaffolding**
   (40 `ITenantEntity` entities, EF filter, JWT claim — but no inbound resolution, every row null-tenant,
   one implicit tenant). **Currency = real per-currency-price mechanism, single-currency operation**
   (platform `Currency` with `IsActive` as the market switch, prices authored per currency in sibling
   row tables, per-record `CurrencyId` on every money-carrying row; the order currency resolved from
   the service address's **country** and the cleaner's from the work **country**, never from a person
   and never from tenant; nothing converts — as rewritten 2026-09-12). **Country = real mechanism,
   single-country operation** (platform
   `Country`/`CountryConfiguration`/`CountryInvoiceConfig` drive live VAT/tax-id/fiscal/invoice; only CZE
   `IsServiced`). The three axes are **orthogonal** (§4).
2. **Entity classification rule:** done — §6. Platform-config (incl. country-keyed) vs tenant-scoped vs
   currency-bearing, with the hard rule: **never `[AllowAnonymous]` + `ITenantEntity` without
   spoof-resistant resolution** (which does not exist today). The 40 are bucketed 33/6/1 in §1.
3. **Expansion path (no rewrite):** done — §8. single → a new market (one data step: currency row,
   country's `DefaultCurrencyCode` + alpha-2, divisor, prices, pay configs, switch the currency on,
   optionally Plus prices and market content, flip `IsServiced` behind the readiness gate — no code;
   the customer picks the market from the selector, ADR-0058) → multi-tenant
   (**the one new piece of infra = spoof-resistant inbound tenant resolution** — vetted-proxy header /
   host-allow-list / SNI pinning, never the raw `Host`). Everything else (filter, claim, write-stamping,
   per-currency price rows, per-record currency, country config) is reused as-is.
4. **T-0113 Option A — CONFIRMED, not revised.** The broader view *strengthens* A1: dropping
   `MembershipPlan.ITenantEntity` → platform config puts it in the same bucket as Currency/Language/
   Country, is currency-neutral (plans are CZK-only by design, no `CurrencyId`, currency was never
   per-tenant), and avoids half-building resolution infra for a zero-row table that would have to trust
   the client `Host` header (S3). Proceed with A1's implementation contract verbatim (§7a).
5. **The 6-entity catalog batch (Service/ServiceCategory/Package/Extra/ServiceCity + MembershipPlan via
   T-0113) — marching orders:** same Option-A treatment, in **its own batch** (not folded into T-0113);
   include the **`Order/Quote` pricing read** (covered by the flip; test the anonymous-Quote path).
   `Referral/Validate` is **accepted-as-is, NOT in the batch, no ticket** (panel ruling — fails shut, §3b).
   The ticket MUST carry the **per-entity index reality** + the **forward-safe/reverse-constrained FK
   note** (§7b — the populated catalogs are NOT the cheap inverse `MembershipPlan` is). A consistency-scan
   rule failing any `ITenantEntity` returned from an `[AllowAnonymous]` action is a **recommended
   follow-up tooling add (non-blocking; NOT a gate on the catalog batch).** **Blocking prerequisite for
   the PM:** resolve the **"BSP-9" naming collision** (§7b / Cross-references) — that label currently means
   the `Order/LookupBatch` hardening in T-0123, so the catalog fix has **no dedicated ticket**; create one
   (or explicitly expand T-0123) **before** scheduling (this is where the index/FK conditions live).
6. **Multi-currency PLANS (if ever):** not now. Minimum future change (§7c) = one **Stripe Price per
   (plan × currency)** as source of truth, plus either a `(PlanCode, CurrencyCode)` display mirror or
   currency-neutral display fields resolved from **country/preference, never tenant**. Fully decoupled
   from the T-0113 tenancy fix — neither blocks the other.
