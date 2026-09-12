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
> **Currency-display (§7c) is the one that is still open.**
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
> switch, the caller-named order currency, per-currency pay coverage and one payout invoice per currency.
> The `Currency.ExchangeRate` column and the conversion path those sections used to describe no longer
> exist. The tenancy material, and the country material except where it names a currency, is unchanged.

---

## 0. TL;DR

Cleansia has **three independent expansion axes**, each at a different maturity level:

| Axis | Mechanism in code | Actually used today? | Verdict |
|---|---|---|---|
| **Tenancy** | `ITenantEntity` on 40 entities + EF global query filter + JWT `tenant_id` | **No** — runs effectively single-tenant (`TenantId = null` everywhere) | **Forward-compat scaffolding** |
| **Currency** | `Currency` platform entity (Code/Symbol/Name/IsDefault/IsActive/LoyaltyPointsDivisor) + per-currency price rows (`ServicePrices`/`PackagePrices`/`ExtraPrices`) + per-record `CurrencyId` on every money-carrying row; **nothing converts** | **Partially** — the whole path is live (caller-named order currency, per-currency pay coverage, one payout invoice per currency), but CZK is the only active currency; EUR is seeded switched off with no prices | **Real mechanism, single-currency operation — adding a currency is data, not code** |
| **Region/Country** | `Country` + `CountryConfiguration` + `CountryInvoiceConfig` platform entities, keyed by `CountryId` | **Partially** — config seeded for ~10 countries; consumed by VAT/tax-id/fiscal/invoice code; but only CZE is `IsServiced` | **Real mechanism, single-country operation** |

The three axes are **separate, not coupled**. Where a currency is derived at all it is derived from
**country** (a cleaner's work country), never from tenant — and an order's currency is not derived: the
caller names it, or the platform default applies. Country is independent of tenant; tenant is
independent of both. There is no place in the code where currency is derived from tenant, or where
country is derived from tenant.

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
- Seeded with **two currencies**: CZK (`IsDefault`, active, divisor 10) and EUR (**inactive**, no
  catalogue prices, no divisor). The other ten — USD, GBP, PLN, CHF, SEK, NOK, DKK, HUF, RON, BGN — were
  removed on 2026-09-09: each was seeded active with a hand-typed rate nobody had reviewed, and until
  the same change any authenticated caller could name one on the quote and create paths.
- **A currency is switched on deliberately.** `Currency.Create` makes a row inactive;
  `ActivateCurrency` / `DeactivateCurrency` (Admin → Currencies, `CanUpdateCurrency`, audited;
  `POST api/AdminCurrency/activate/{id}` and `deactivate/{id}`) flip it, and the default cannot be
  switched off (`currency.cannot_deactivate_default`). Switching one on makes every catalogue save
  require a price in it (`MustCoverAllActiveCurrencies` on the service, package and extra create/update
  validators — `service.missing_price_for_currency`). `SetDefaultCurrency` refuses a currency that is
  inactive (`currency.invalid`) or that has no price row in any of the three price tables
  (`currency.not_priced`) — "offerable" is both, and the booking path (T-0699) checks the same pair.
- **Offerable is one predicate**, `ICurrencyRepository.IsOfferableAsync`: the currency exists, `IsActive`
  is true, and at least one row in `ServicePrices`, `PackagePrices` or `ExtraPrices` is in it. The
  `QuoteOrder`, `CreateOrder` and `QuotePlusSavings` validators and `SetDefaultCurrency`'s promotion gate
  all ask it, so the star and the quote cannot disagree about what "offerable" means. On a Currency,
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

### The order currency comes from the caller {#order-currency}

It is **not derived from the address**, and it is not derived from the country either.

- `QuoteOrder`, `CreateOrder` and `QuotePlusSavings` take an optional `CurrencyId`; null means the
  platform default. A named currency must be offerable (`currency.invalid`), and that rule runs first in
  the chain so the calculator is never asked to price in a currency it cannot. `CreateOrder` re-prices in
  that currency and compares against the client's `totalPrice`, so quoting in one currency and creating
  in another fails as `order.total_price.not_match`. The resolved row's id — not the raw command field —
  is stamped on `Order.CurrencyId`, so the price and the stamp cannot disagree. A recurring occurrence is
  priced in the platform default. → /api/orders#quote
- `OrderPricingCalculator` resolves the currency **first** — it is an input to the prices, not a label
  applied afterwards — then reads the `ServicePrices` / `PackagePrices` / `ExtraPrices` rows in it. A
  service line is `BasePrice + PerRoomPrice × (rooms + bathrooms)` from its row; a package or an extra
  is its row's `Price`. An entry with no row in the order's currency is **not offerable in it**: the
  customer overviews withhold it and quote/create refuse it. Every figure the calculator returns is in
  the catalogue's own currency; the quote carries `currencyId` and `currencyCode` and nothing else about
  currency.
- **Pay coverage is per currency, and it is a second offerability gate.** `EmployeePayConfig` rows carry
  a `CurrencyId` (the unique index is `(EmployeeId, ServiceId, PackageId, CurrencyId)`, NULLS NOT
  DISTINCT). One predicate, `PayCoverage.Applies(config, employeeId, currencyId)`, serves both the
  catalogue gate and the pay writer: an entry is offered in a currency only when every line has a
  platform-wide pay config **in that currency**, and `CalculateOrderPay` reads only rows in the order's
  currency. A rate in another currency counts for nothing — without the currency term every gate
  admitted a EUR order on the strength of a CZK rate, and the writer then found nothing: an order on
  every board with no pay on any of them. `ApproveEmployee` creates no pay config: it resolves the
  cleaner's currency from the work country (`ResolveCurrencyForWorkCountryAsync`) and refuses approval
  (`employee.pay_config_missing`) while any active catalogue entry lacks a platform-wide rate in it. The
  bulk grade apply is what creates per-employee configs, multiplying the `ServicePrices` /
  `PackagePrices` row in the admin-chosen currency and skipping an entry with no row in it.
  → /product/business-rules#cleaner-pay

### The cleaner's currency is resolved from country — never from tenant {#currency-resolution}

- `ICurrencyResolutionService.ResolveCurrencyForEmployeeAsync` returns the `Currency` **entity**, never
  null. Chain: **`Employee.WorkCountryId` → `CountryConfiguration.DefaultCurrencyCode` if it names a
  real currency → platform default.** The middle step is guarded because the column is free text with
  no foreign key — three characters an admin types — so a typo, or a currency deleted after the country
  was configured, falls through to the default rather than labelling money with a code the platform
  does not have. It is deliberately **not** filtered on `IsActive`: a country configured for EUR
  resolves to EUR as soon as the EUR row exists, switched on or not, rather than reading as broken
  until the market opens.
- Every partner-facing money aggregate is scoped to that currency and labelled with its code: dashboard
  stats, the earnings chart, personal bests, order-distribution money columns, the available-jobs
  headline, pending earnings and My Pay (`GetPeriodPays`). Counts stay over all orders. Pay in any other
  currency is not on those screens until the per-row DTO carries a currency.
- **Orders / receipts / e-mails** render the record's own `Order.Currency` — `order.Currency?.Code ??
  "CZK"` and `?.Symbol ?? "Kč"` (`ReceiptService.cs`, `EmailService.cs`). **Invoice PDFs** render
  `EmployeeInvoice.Currency` the same way (`FileExtensions.CreatePdfData`).

There is **no per-tenant currency setting anywhere**. The only tenant-shaped currency surface is the
per-record `CurrencyId`, and it is populated from the caller (orders), from the pay rows (invoices) or
from the work country (pay configs, dashboards) — never from a tenant config.

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
  shown on `MyPayoutDetails`, `MaskedPayoutDetails` and the GDPR export. `ApproveInvoice` refuses
  (`payroll.invoice.payout_currency_mismatch`) when the declared currency — the platform default when
  undeclared — is not the invoice's. Approval is the last point the platform can refuse: the transfer is
  keyed by hand in a bank and `MarkInvoicePaid` only records it. There is **no payout execution path**
  (no bank file, no Stripe Connect, no SEPA), and a missing payout record passes this rule — ADR-0034
  D7's presence gate is not implemented anywhere. → /flows/pay-and-payouts#the-path

### Customer surfaces: the default currency labels the catalogue {#customer-default-currency}

- The catalogue overviews are priced in the platform default currency and carry no code, so the
  Customer and Mobile.Customer hosts expose an anonymous `GET api/Currency/GetOverview`
  (`CurrencyListItem`: `id`, `code`, `symbol`, `name`, `isDefault`) for a surface to learn what to label
  catalogue prices with. The web app has one shared `formatMoney(value, currencyCode, locale)`; every
  quote, order, dispute and credit figure is labelled from its payload's `currencyCode`, catalogue
  figures from the default. The Android and iOS customer apps mirror this.
- There is **no currency picker** on any customer surface yet, so the caller-named `CurrencyId` is a
  contract the shipped clients do not exercise. Known residuals: membership surfaces on both mobile apps
  still print "Kč", both payment-sheet configurations pin CZ/CZK, and customer locale copy still states
  CZK figures as content.

**Admin surfaces** follow the same rule. Revenue and payroll reports answer in **one** currency
(`ReportFilter.CurrencyId`, null = platform default, `currency.not_found` for an unknown one) and carry
`currencyCode`; there is no "all currencies" report, because a sum across two currencies is not a
number. `IssueCustomerCredit` names its currency (it must exist and be active), and the user-loyalty
detail shows one balance block per credit account. → /admin-app/reporting

### What is still bound to the platform default {#default-bound-numbers}

Three numbers are authored in one currency and enforced only on an order in it: the no-show credit
`BookingPolicy.NoShowCreditCzk = 250` (paid by `CancelUnfilledOrders` on a default-currency order only;
any other currency fails closed and logs, the refund is unaffected), the tier floor
`LoyaltyTierConfig.MinimumOrderAmountForDiscount` (1000; no floor applies elsewhere) and the minimum on
a promo code that names no currency (on an order in any other currency the validate preview answers the
`CurrencyMismatch` error code and the create path applies no discount). Promoting a different default with `SetDefaultCurrency` re-denominates all three —
an owner-level event, not an admin click. Loyalty earning is per currency through
`Currency.LoyaltyPointsDivisor`; `IssueCustomerCredit.SanityCap = 10 000` is a unit-free typo guard in
whatever currency the grant names; `CountryConfiguration.RefundStripeFixedFee` is a number in the
country's own currency, deducted only from a refund in it. The figures and the reasoning live on the
business-rules page. → /product/business-rules

**Verdict:** the **per-record currency mechanism is real and wired end-to-end** — entity, per-currency
price rows, caller-named order currency, per-currency pay coverage, one invoice per currency, declared
payout currency, labelled DTOs on every host. Adding a market is data, not code (§8). There is no
conversion half, and it is not coming back.

**Operation is single-currency and the platform enforces it** rather than merely happening to be it:
CZE is the only serviced country, CZK is the only active currency and EUR is seeded switched off and
unpriced, so the only currency a caller can name today is CZK — and the default cannot be moved to a
currency the catalogue is not priced in. The CZK/"Kč" hardcoding is a **safety-net fallback string**
for when a record has no currency row (`Constants.Currency.Czk`), not a design assumption that the
platform is CZK-only.

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
1. **Currency defaulting** — `CountryConfiguration.DefaultCurrencyCode` is step 2 of the cleaner
   currency-resolution chain (§2): it scopes every partner-facing money aggregate and it is the currency
   `ApproveEmployee` checks a cleaner's pay coverage in. In that chain it has to name a real
   `Currency` row or it falls through to the platform default; nothing in the platform writes it — the
   seed authors it. Its other reader is the refund fee rule (item 7).
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
   `RefundStripeFixedFee`, a number in the country's `DefaultCurrencyCode`. The address names the country
   and the caller names the order's currency, and nothing ties the two, so the fixed part is deducted
   only when the order's currency code equals the country's and absorbed otherwise. Dormant today: no
   production writer sets either figure.

**Verdict:** the **per-country mechanism is real and consumed by live VAT/tax/fiscal/invoice code**, and
seed data exists for ~10 countries. But **operation is single-country**: only CZE is `IsServiced`, so
order creation rejects any non-serviced country (`CreateOrder.ResolveAddressAsync`, defaults to the
single serviced country when exactly one exists). Multi-country is "flip `IsServiced` + ensure
`CountryConfiguration`/`CountryInvoiceConfig` rows exist," not a code change — and when the new
country's `DefaultCurrencyCode` is not CZK, it is also the currency step in §8: nothing is offerable in
a currency until it is switched on, priced and covered by pay configs.

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
            └──(DefaultCurrencyCode, cleaner side)──┐
CALLER  ──(CurrencyId on quote/create, order side)──┴──► CURRENCY ──(price rows per currency + per-record CurrencyId)──► price / label
                                                                     nothing converts; a null CurrencyId means the platform default
```

- **Where currency is derived, it is derived from COUNTRY**, never from tenant
  (`Employee.WorkCountryId → CountryConfiguration.DefaultCurrencyCode → default Currency`, in
  `CurrencyResolutionService`). An order's currency is not derived at all — the caller names it or the
  platform default applies (§2) — and it is not read from the address.
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
| `Constants.Currency.Czk = "CZK"` | hardcoded **fallback string** | safety-net only; comment says multi-currency is via `Currency` entity |
| `ReceiptService.cs` | `order.Currency?.Code ?? Constants.Currency.Czk` / `?.Symbol ?? "Kč"` | configurable primary, hardcoded fallback |
| `EmailService.cs` | `order.Currency?.Symbol ?? "Kč"` | configurable primary, hardcoded fallback |
| `FileExtensions.CreatePdfData` | `currency?.Code ?? Constants.Currency.Czk` / `?? "Kč"` | configurable primary, hardcoded fallback |
| `MembershipPlan.MonthlyPriceCzk` / `MonthlyEquivalentPriceCzk` | **genuinely CZK-only** (field name + XML doc "Display price in CZK") | see §7 |
| Catalogue prices (`ServicePrices`, `PackagePrices`, `ExtraPrices`) | **not hardcoded** — one row per (entry, currency), each row carries `CurrencyId` | authored per currency, never converted |
| `BookingPolicy.NoShowCreditCzk`, `LoyaltyTierConfig.MinimumOrderAmountForDiscount`, a promo minimum on a code with no `CurrencyId` | **bound to the platform default** — enforced only on an order in it | §2 "What is still bound to the platform default" |
| Customer clients — membership screens on both mobile apps, both payment-sheet configurations, locale copy | "Kč" / `CZK` as content | known residuals of T-0706, not runtime currency logic |

**Net:** outside MembershipPlan, CZK/"Kč" appears at runtime only as a *fallback* when a record has no
`Currency` row. The configurable path (record `CurrencyId` → `Currency.Symbol/Code`, or the price row in
the order's currency) is always preferred. The one **structurally** CZK-bound surface is
**MembershipPlan**; the three default-bound numbers are CZK today because CZK is the default, not because
they name it.

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
   time from the caller (orders), from the rows being aggregated (invoices) or from the work country
   (pay configs, dashboards), never from tenant. A **catalogue entity carries no price**: its price
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

### 7c. What multi-currency PLANS would require (if ever needed)

> ⚠ **DEFERRED OPTION SKETCH — NOT approved work. Do not implement without a fresh ticket and a committed
> product need.**

Not needed now (plans are CZK-only by design; Stripe holds the canonical price and charge currency per
`StripePriceId`). If a future product wants plan prices shown/charged in multiple currencies, the
**minimum** change set (consistent with §6) would be:
- Register **one Stripe Price per (plan × currency)** — Stripe already supports multi-currency Prices;
  this is the real source of truth and the bulk of the work is Stripe-side, not schema-side.
- Either (a) add a per-currency price **mirror** table keyed by `(PlanCode, CurrencyCode)` — the same
  shape the catalogue now uses (`ServicePrices` keyed `(ServiceId, CurrencyId)`, §2) — for no-round-trip
  display, or (b) rename the `*Czk` display fields to currency-neutral and resolve the display currency
  the way the catalogue does — the platform default, or an explicit customer choice once one exists
  (there is no customer-side currency preference or picker today), **never** from tenant.
- **This is fully decoupled from the T-0113 tenancy fix.** Tenancy (who owns the plan) and currency
  (what denomination it's shown in) are separate axes (§4). Doing A now does not block or complicate a
  future multi-currency-plans feature, and a future multi-currency-plans feature does not require
  re-tenanting plans.

---

## 8. The expansion path (single-tenant → multi-tenant → multi-region/currency, WITHOUT a rewrite)

The scaffolding is deliberately built so each axis flips on independently:

1. **Today — single everything.** One implicit tenant (`TenantId = null`), CZK the default and the only
   active currency, CZE serviced. All mechanisms present and exercised on the single-value path.
2. **Multi-COUNTRY (no code).** Flip `Country.IsServiced` for the new country; ensure its
   `CountryConfiguration` + `CountryInvoiceConfig` rows exist (seed already covers ~10). VAT, tax-id
   validation, fiscal mode, invoice template and date format activate automatically via the existing
   consumers (§3), and order creation already gates on `IsServiced`. The country's `DefaultCurrencyCode`
   resolves for its cleaners as soon as that currency row exists — but nothing is offerable in it until
   step 3 has switched it on and priced it.
3. **Multi-CURRENCY (data, not code).** In order: create the currency (born switched off) and author
   its `LoyaltyPointsDivisor`; price it — every service, package and extra that should be sold in it
   needs a row, and `ActivateCurrency` turns that into a rule for every later catalogue save; add a
   platform-wide `EmployeePayConfig` per entry in it, or the entry is withheld from that currency's
   catalogue; switch it on. From then on the currency is offerable: a caller may name it on quote and
   create, orders stamp it, pay rows and invoices follow it (one invoice per currency per period), and
   cleaners whose work country defaults to it see their dashboards in it and declare it on their
   payout account. **What stays CZK-bound** until someone decides otherwise: the no-show credit, the
   tier floor and any promo minimum on a code without a currency (§2, "What is still bound to the
   platform default"); and no customer client offers a currency picker yet, so the market is reachable
   by API before it is reachable by app.
4. **Multi-TENANT (the one axis that needs new infrastructure).** The `ITenantEntity` filter + JWT claim
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
   row tables, per-record `CurrencyId` on every money-carrying row; the order currency named by the
   caller, the cleaner's resolved from **country**, never tenant; nothing converts — as rewritten
   2026-09-12). **Country = real mechanism, single-country operation** (platform
   `Country`/`CountryConfiguration`/`CountryInvoiceConfig` drive live VAT/tax-id/fiscal/invoice; only CZE
   `IsServiced`). The three axes are **orthogonal** (§4).
2. **Entity classification rule:** done — §6. Platform-config (incl. country-keyed) vs tenant-scoped vs
   currency-bearing, with the hard rule: **never `[AllowAnonymous]` + `ITenantEntity` without
   spoof-resistant resolution** (which does not exist today). The 40 are bucketed 33/6/1 in §1.
3. **Expansion path (no rewrite):** done — §8. single → multi-country (flip `IsServiced`, no code) →
   multi-currency (data: create, price, cover with pay configs, switch on — no code) → multi-tenant
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
