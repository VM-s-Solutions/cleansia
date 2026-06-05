# Platform Expandability & Current-State Doctrine — Tenancy · Currency · Region/Country

> Status: **doctrine (ratified by lead-architect panel, 2026-06-02)** — author + YAGNI + corner-painting
> challengers reconciled; index/reversal facts file-verified. Built on the three analyst current-state
> maps and verified against real code. Documentation/decision only — no code.
> Owner ask: "run analysts + architecture discussion one more time to fully define the platform
> expandability and current situation overall."
>
> This is the canonical reference for the question **"is this entity platform-config, tenant-scoped, or
> country-scoped?"** It grounds three pending decisions: **T-0113** (MembershipPlan tenancy),
> **the four sibling anonymous catalogs** (Service/Package/Extra/ServiceCity), and **currency-display**.
> Where it changes a decision it cross-references **ADR-0001 Addendum A1**.
>
> **Architect verdict (this pass):** the wider three-axis picture **CONFIRMS** ADR-0001 Addendum A1's
> Option-A ruling for T-0113 and broadens it into a general entity-classification rule (§6). It does
> **not revise** A1. Only one ground-truth correction was needed: there are **40** `ITenantEntity`
> entities, not 41 (§1).

---

## 0. TL;DR

Cleansia has **three independent expansion axes**, each at a different maturity level:

| Axis | Mechanism in code | Actually used today? | Verdict |
|---|---|---|---|
| **Tenancy** | `ITenantEntity` on 40 entities + EF global query filter + JWT `tenant_id` | **No** — runs effectively single-tenant (`TenantId = null` everywhere) | **Forward-compat scaffolding** |
| **Currency** | `Currency` platform entity (Code/Symbol/ExchangeRate/IsDefault) + per-record `CurrencyId` + `ExchangeRate` conversion | **Partially** — 12 currencies + rates seeded; resolution + conversion code live; but every catalog price is authored in CZK and only CZK is the default | **Real mechanism, single-currency operation** |
| **Region/Country** | `Country` + `CountryConfiguration` + `CountryInvoiceConfig` platform entities, keyed by `CountryId` | **Partially** — config seeded for ~10 countries; consumed by VAT/tax-id/fiscal/invoice code; but only CZE is `IsServiced` | **Real mechanism, single-country operation** |

The three axes are **separate, not coupled**. Currency is resolved from **country** (not tenant);
country is independent of tenant; tenant is independent of both. There is no place in the code where
currency is derived from tenant, or where country is derived from tenant.

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

**Mechanism (real):**
- `Currency : Auditable` (NOT `ITenantEntity`) — **platform config**. Fields: `Code`, `Symbol`,
  `Name`, `ExchangeRate` (default `1.0m`), `IsDefault` (`Currency.cs`).
- Seeded with **12 currencies** with real exchange rates relative to CZK
  (`insert_seed_data.sql:230-248`: CZK=1.0 `IsDefault=true`, EUR=0.041, USD=0.044, GBP, PLN, CHF, SEK,
  NOK, DKK, HUF, RON, BGN). Exactly one default (CZK).
- `CurrencyId` is **per-record** on `Order` (`Order.cs:111`), `PromoCode` (`PromoCode.cs:28`, nullable —
  fixed-discount codes only), `EmployeePayConfig` (`EmployeePayConfig.cs:34`, required),
  `EmployeeInvoice` (`EmployeeInvoice.cs:37`, required). **Not per-tenant.**
- `CurrencyRepository.GetDefaultAsync` returns the `IsDefault` row; `IsInUseAsync` checks Order/
  EmployeePayConfig/EmployeeInvoice referential use before delete.

**How display currency is resolved — CONFIRMED per-record, NOT per-tenant:**
- **Orders / receipts / emails:** the record's own `CurrencyId` → `Order.Currency`. Receipt and email
  render `order.Currency?.Code ?? "CZK"` and `order.Currency?.Symbol ?? "Kč"` (`ReceiptService.cs:120,
  227, 311`; `EmailService.cs:94, 270, 316`). Order currency is chosen at create time:
  `CreateOrder` uses `command.CurrencyId` if supplied, else `GetDefaultAsync` (`CreateOrder.cs:292-294`).
- **Employee-facing money (dashboard, pay):** resolved by `CurrencyResolutionService`
  (`CurrencyResolutionService.cs`). Chain: **`Employee.WorkCountryId` →
  `CountryConfiguration.DefaultCurrencyCode` → platform default `Currency`.** This is the explicit link
  proving **currency derives from COUNTRY, not tenant** (`ICurrencyResolutionService.cs` documents the
  exact chain; `GetDashboardStats.cs:201-209` consumes it).
- **Invoices (PDF):** `EmployeeInvoice.CurrencyId` → `currency?.Code ?? Constants.Currency.Czk`,
  `currency?.Symbol ?? "Kč"` (`FileExtensions.CreatePdfData:50-51`).

There is **no per-tenant currency setting anywhere**. The only tenant-shaped currency surface is the
per-record `CurrencyId`, and even that is populated from country/default, not from a tenant config.

**How `ExchangeRate` is actually used (important nuance):** `OrderPricingCalculator` computes the base
subtotal from catalog prices (`Service.BasePrice`, `Package.Price`, `Extra.Price`) **as authored**, then
multiplies the whole total by the selected currency's `ExchangeRate`
(`OrderPricingCalculator.cs:46-66`: `totalPrice = (baseSubtotal + surcharge) * exchangeRate`). So
multi-currency today is a **flat conversion of CZK-authored prices**, not per-currency catalog pricing.
Catalog prices have no `CurrencyId` — they are implicitly in the default currency (CZK). The Quote
response surfaces `ExchangeRate` and `CurrencyCode` so a client could display a converted price.

**Verdict:** the **exchange-rate multi-currency MECHANISM is real and wired end-to-end** (entity, seed
data with rates, per-record stamping, resolution service, conversion in pricing, symbol/code on
receipts/invoices/emails). But **operation is single-currency**: every catalog price is authored in CZK,
CZK is the sole default, and CZE is the only serviced country, so in practice `ExchangeRate` is always
`1.0` on the live path. The CZK/"Kč" hardcoding is a **safety-net fallback string** for when a record
has no currency row (`Constants.Currency.Czk` comment: "Multi-currency is supported via the Currency
entity; this is just the safety-net string default"), not a design assumption that the platform is
CZK-only.

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
  (passport) and `Address.CountryId` (residency) — see the Employee XML doc (`Employee.cs:68-81`).
  Employee also has a `PreferredCurrencyCode` (`Employee.cs:92`) — a manual override field.

**What per-country config actually DRIVES today (all verified consumers):**
1. **Currency defaulting** — `CountryConfiguration.DefaultCurrencyCode` is step 2 of the employee
   currency-resolution chain (§2).
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

**Verdict:** the **per-country mechanism is real and consumed by live VAT/tax/fiscal/invoice code**, and
seed data exists for ~10 countries. But **operation is single-country**: only CZE is `IsServiced`, so
order creation rejects any non-serviced country (`CreateOrder.ResolveAddressAsync:452-470`, defaults to
the single serviced country when exactly one exists). Multi-country is "flip `IsServiced` + ensure
`CountryConfiguration`/`CountryInvoiceConfig` rows exist," not a code change.

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
| `FeatureFlag/check` (**web only**) | FeatureFlag | **No** (`FeatureFlag.cs:6`) | No — scope/value params (`AppConfigurationProvider.cs:26-55`) | none | **CORRECT (by-design provider)** |
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
            └──(DefaultCurrencyCode)──► CURRENCY ──(per-record CurrencyId + ExchangeRate)──► display/convert
```

- **Currency depends on COUNTRY**, never on tenant (`Employee.WorkCountryId →
  CountryConfiguration.DefaultCurrencyCode → default Currency`). Confirmed in
  `CurrencyResolutionService` + its interface doc.
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
| `ReceiptService.cs:120,227,311` | `order.Currency?.Code ?? "CZK"` / `?? "Kč"` | configurable primary, hardcoded fallback |
| `EmailService.cs:94,270,316` | `order.Currency?.Symbol ?? "Kč"` | configurable primary, hardcoded fallback |
| `FileExtensions.CreatePdfData:50-51` | `currency?.Code ?? Constants.Currency.Czk` / `?? "Kč"` | configurable primary, hardcoded fallback |
| `GetDashboardStats.cs:115-116` | "0 Kč" in a code **comment** only | not runtime |
| `MembershipPlan.MonthlyPriceCzk` / `MonthlyEquivalentPriceCzk` | **genuinely CZK-only** (field name + XML doc "Display price in CZK") | see §7 |
| Catalog prices (`Service.BasePrice`, `Package.Price`, `Extra.Price`) | implicitly CZK (no `CurrencyId`) — converted by `ExchangeRate` | implicitly default-currency |

**Net:** outside MembershipPlan, CZK/"Kč" appears only as a *fallback* when a record has no `Currency`
row. The configurable path (record `CurrencyId` → `Currency.Symbol/Code`) is always preferred. The one
**structurally** CZK-bound surface is **MembershipPlan**.

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
3. **Currency-bearing?** Add a per-record `CurrencyId` only on *money-carrying transactional records*
   (orders, invoices, pay configs, fixed-amount promos). Resolve the value from country/default, never
   from tenant. Do **not** put `CurrencyId` on catalog entities — they are authored in the default
   currency and converted by `ExchangeRate`.

---

## 7. The pending decisions, resolved under this doctrine

### 7a. T-0113 (MembershipPlan tenancy) — Option A is CONFIRMED, and broadened

`MembershipPlan` is today `: Auditable, ITenantEntity` (`MembershipPlan.cs:24`) served on
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

All four are `: Auditable, ITenantEntity` (verified) **and** served `[AllowAnonymous]` on the customer
host (`ServiceController`, `PackageController`, `ExtraController`, `ServiceCityController`, all line ~14).
`ServiceCategory` is **also** `ITenantEntity` — confirm whether it is reachable from any anonymous read
path; if yes it joins the batch, if no it stays untouched (parity with the `LoyaltyTierConfig` carve-out).

**Marching order (per ADR-0001 Addendum A1 D-A1.4):** apply the **same Option-A treatment** — drop
`ITenantEntity`, make them platform config, swap any `(TenantId, …)` unique indexes to drop `TenantId`.
They are the same bug class as MembershipPlan and correct today only by single-tenant coincidence. Keep
them in **their own batch** (do NOT fold into T-0113 — scope discipline avoids the double-fix collision
the T-0113 ticket itself warns of). One doctrine (this doc + A1 D-A1.1) governs both; cross-reference it.

> **DISCREPANCY TO FLAG TO PM (real, needs reconciliation):** the label **"BSP-9"** is overloaded.
> - The **T-0123-prod-config** ticket defines **BSP-9** as the anonymous `Order/LookupBatch`
>   secret-pair/cap hardening (`T-0123.md:37-39, 60-62`, `LookupOrderBatch.cs`) — a *different* finding,
>   NOT the four catalogs.
> - **ADR-0001 Addendum A1 D-A1.4** and the orchestrator's ground truth use **"BSP-9"** to mean the
>   *four sibling anonymous catalogs*.
>
> These are two different bodies of work under one name. The catalog-tenancy fix currently has **no
> dedicated ticket** — Addendum A1 routes it "to BSP-9," but the BSP-9 in the backlog is the LookupBatch
> ticket. **Action:** the PM must either (a) create a dedicated ticket for the four-catalog Option-A fix
> (recommended; mirror the T-0113 contract per entity) and have A1 D-A1.4 reference *that* id, or
> (b) explicitly expand T-0123's BSP-9 scope. Do not let the catalog fix fall through the naming gap.

### 7c. What multi-currency PLANS would require (if ever needed)

> ⚠ **DEFERRED OPTION SKETCH — NOT approved work. Do not implement without a fresh ticket and a committed
> product need.**

Not needed now (plans are CZK-only by design; Stripe holds the canonical price and charge currency per
`StripePriceId`). If a future product wants plan prices shown/charged in multiple currencies, the
**minimum** change set (consistent with §6) would be:
- Register **one Stripe Price per (plan × currency)** — Stripe already supports multi-currency Prices;
  this is the real source of truth and the bulk of the work is Stripe-side, not schema-side.
- Either (a) add a per-currency price **mirror** table keyed by `(PlanCode, CurrencyCode)` for
  no-round-trip display, or (b) rename the `*Czk` display fields to currency-neutral and resolve the
  display currency the same way orders do — from **country** (`CountryConfiguration.DefaultCurrencyCode`)
  or an explicit user preference (`Employee.PreferredCurrencyCode` has a customer-side analog), **never**
  from tenant.
- **This is fully decoupled from the T-0113 tenancy fix.** Tenancy (who owns the plan) and currency
  (what denomination it's shown in) are separate axes (§4). Doing A now does not block or complicate a
  future multi-currency-plans feature, and a future multi-currency-plans feature does not require
  re-tenanting plans.

---

## 8. The expansion path (single-tenant → multi-tenant → multi-region/currency, WITHOUT a rewrite)

The scaffolding is deliberately built so each axis flips on independently:

1. **Today — single everything.** One implicit tenant (`TenantId = null`), CZK default, CZE serviced.
   All mechanisms present and exercised on the single-value path.
2. **Multi-COUNTRY (smallest next step, no code).** Flip `Country.IsServiced` for the new country; ensure
   its `CountryConfiguration` + `CountryInvoiceConfig` rows exist (seed already covers ~10). VAT, tax-id
   validation, fiscal mode, invoice template, date format, default currency all activate automatically
   via the existing consumers (§3). Order creation already gates on `IsServiced`.
3. **Multi-CURRENCY (no code for the conversion path).** Already on: a non-CZK country's
   `DefaultCurrencyCode` flows through `CurrencyResolutionService`; orders/invoices stamp the resolved
   `CurrencyId`; `ExchangeRate` converts CZK-authored catalog prices. *Optional* upgrade later:
   per-currency catalog pricing (add `CurrencyId` to catalog prices) instead of flat `ExchangeRate`
   conversion — but only if rate-based conversion proves insufficient.
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

- **ADR-0001 Addendum A1** (`agents/backlog/adr/0001-authorization-model.md:1134-1184`) — the binding
  ruling this doctrine confirms and broadens. D-A1.1 is the hard rule; D-A1.4 routes the sibling
  catalogs (see the §7b naming discrepancy).
- **T-0113** (`agents/backlog/tickets/T-0113-lg-sec-05.md`) — MembershipPlan build ticket (awaiting owner
  approval; this doctrine recommends proceeding unchanged).
- **T-0123** (`agents/backlog/tickets/T-0123-prod-config.md`) — note its "BSP-9" is the `LookupBatch`
  hardening, a *different* finding from the four-catalog fix.
- **Security law S3** (`agents/knowledge/security-rules.md`) — anonymous routes must not serve
  tenant-scoped data without deliberate, spoof-resistant resolution.
- **Real-code anchors:** `Currency.cs`, `Country.cs`, `Language.cs`, `CountryConfiguration.cs`,
  `CountryInvoiceConfig.cs`, `CurrencyResolutionService.cs`, `OrderPricingCalculator.cs:46-66`,
  `CleansiaDbContext.cs:111-179`, `TenantProvider.cs`, `MembershipPlan.cs:24`, `Employee.cs:68-93`,
  `ProcessedStripeEvent.cs:11-20`, `ProcessedStripeEventRepository.cs:12-19`,
  `insert_seed_data.sql` (currencies :230-248, countries :82-127, invoice configs :1543-1614, country
  configs :1619+, plans :2445-2470).

---

## 10. Architect ratification (this pass — the six asks, answered)

Recorded by the authoring architect on top of ADR-0001 Addendum A1, having verified every load-bearing
premise against real code. This is the single scannable sign-off.

1. **Current-state map (3 axes):** done — §0 table + §1/§2/§3. **Tenancy = forward-compat scaffolding**
   (40 `ITenantEntity` entities, EF filter, JWT claim — but no inbound resolution, every row null-tenant,
   one implicit tenant). **Currency = real exchange-rate mechanism, single-currency operation** (platform
   `Currency` + per-record `CurrencyId`, resolved from **country**, never tenant; flat `ExchangeRate`
   conversion of CZK-authored prices). **Country = real mechanism, single-country operation** (platform
   `Country`/`CountryConfiguration`/`CountryInvoiceConfig` drive live VAT/tax-id/fiscal/invoice; only CZE
   `IsServiced`). The three axes are **orthogonal** (§4).
2. **Entity classification rule:** done — §6. Platform-config (incl. country-keyed) vs tenant-scoped vs
   currency-bearing, with the hard rule: **never `[AllowAnonymous]` + `ITenantEntity` without
   spoof-resistant resolution** (which does not exist today). The 40 are bucketed 33/6/1 in §1.
3. **Expansion path (no rewrite):** done — §8. single → multi-country (flip `IsServiced`, no code) →
   multi-currency (already on the conversion path, no code) → multi-tenant (**the one new piece of infra
   = spoof-resistant inbound tenant resolution** — vetted-proxy header / host-allow-list / SNI pinning,
   never the raw `Host`). Everything else (filter, claim, write-stamping, per-record currency, country
   config) is reused as-is.
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
