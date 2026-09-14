# Domain model

Generated from the EF Core entity configurations, not described from memory. A relationship on a
diagram is a `HasOne(...)` declared in a configuration file; if it is not there, it is not enforced.

One diagram per area, because a single picture of all 87 entities is a picture nobody reads. Entities
appear in the area they are owned by, not everywhere they are referenced.

::: tip Checking the count
`grep -c 'migrationBuilder.CreateTable' src/Cleansia.Infra.Database/Migrations/*_Initial.cs` — the
migration is regenerated rather than stacked, so it always reflects the current model. The count above
was 70 for long enough to be wrong by six before anyone noticed, and then 76 for long enough to be wrong
by five, which is why the check is written down rather than the number being trusted.
:::

## Tenancy — who a row belongs to

```mermaid
erDiagram
  CountryConfiguration }o--o| Tenant : "OperatorTenant"
```

**A tenant is an operating company under the holding** — the legal entity that contracts the customer,
employs the cleaner, issues the receipt and pays the payout ([ADR-0061](/decisions/adr-0061)). `Tenant`
is a three-column registry (`Id varchar(26)`, assigned not generated — `cleansia-cz`; `Name`;
`IsActive`), written only by the seed; there is no repository, no DTO and no admin surface until a
second company exists. **A country is served by at most one operator and an operator serves one or
more countries**: `CountryConfiguration.OperatorTenantId` (nullable, FK Restrict, indexed) is the whole
map. Null means nobody serves that market — `Market/GetOverview` does not list it and an anonymous
write naming it is refused `tenant.not_found`.

**Every stamped row carries its operator.** 47 entities implement `ITenantEntity`; the `TenantId`
column is **NOT NULL** on 45 of them and nullable only on `OutboxMessage` and `DeadLetter` (an envelope
may have no tenant). The value is written at commit time from the ambient tenant — the JWT claim, the
market's operator for an anonymous write, the user's tenant on a token mint, the row's own tenant in a
job — and the database refuses a row with none. **There is no foreign key from a stamped table to
`Tenants`**: a tenant id enters the system at exactly two points, the operator map (which is FK'd) and
the claim (minted from a row that was itself stamped through the map), and the closure is proven by
`SeededDatabaseHasNoOrphanTenantRowsTests` rather than by 45 constraints. Tenantless `Auditable`
tables (`CountryConfiguration`, `Service`, …) still carry a nullable `TenantId` column by inheritance;
it is dead — unfiltered, unstamped, meaning nothing.

| Entity | |
|---|---|
| `Tenant` | — ; referenced by `CountryConfiguration.OperatorTenantId` only |

## Identity and access

```mermaid
erDiagram
  Device }o--|| User : "User"
  Employee ||--|| User : "User"
  EmployeePayoutDetails ||--|| Employee : "Employee"
  EmployeePayoutDetails }o--o| Currency : "Currency"
  RefreshToken }o--|| User : "User"
  UserConsent }o--|| User : "User"
  UserConsent }o--o| LegalDocument : "LegalDocument"
  UserStripeCustomer }o--|| User : "User"
  UserStripeCustomer }o--|| Currency : "Currency"
  EmployeeDocument }o--|| Employee : "Employee"
  DocumentDeletionRequest }o--|| EmployeeDocument : "Document"
  EmployeeDocumentRequirement }o--|| Country : "Country"
```

| Entity | |
|---|---|
| `User` | references `PreferredLanguage`; unique `Email` (`citext`) **with no tenant term** — one identity per email across the holding ([ADR-0061](/decisions/adr-0061) D5.1); `TenantId` NOT NULL is the operating company the account belongs to, the market's operator at registration |
| `Employee` | references `Nationality`, `User`, `WorkCountry` — at approval the work country's operator must be the admin's own tenant (`employee.work_country_operator_mismatch`) |
| `RefreshToken` | references `User` |
| `Device` | references `User` |
| `EmployeePayoutDetails` | references `BankCountry`, `Currency` (nullable, Restrict — the currency the cleaner declares the account holds), `Employee` (Cascade); unique `(TenantId, EmployeeId)`, nulls not distinct |
| `EmployeeDocument` | references `Employee`, `PreviousVersion` |
| `EmployeeDocumentRequirement` | references `Country` (Restrict); unique `(CountryId, DocumentType)` — which document types a country requires of a cleaner |
| `DocumentDeletionRequest` | references `Document` (Restrict) — a cleaner's request to have a document removed, answered by an admin |
| `EmployeeActionAudit` | — bare `EmployeeId` and `OrderId` scalars with no FK, because the act it records deletes the `OrderEmployee` row it describes and must survive an erased order; indexed `(OrderId, CreatedOn DESC)` for the admin timeline that reads it by order ([ADR-0062](/decisions/adr-0062) D6) |
| `CustomerActionAudit` | — bare `UserId` scalar with no FK (null for a guest act), because the row must outlive everything it names; `ClientAudience`, `IpAddress`, `DeviceLabel`, `DeviceId` are the request context, `PayloadJson` (jsonb) the typed evidence a handler emitted, `ErrorCode` the refusal key on a failure row. `TenantId` NOT NULL. Append-only — `Pseudonymise()` (erasure blanks the three request-metadata columns) is the one mutator; indexed `(TenantId, OccurredOn DESC)`, `(UserId, OccurredOn DESC)`, `(ResourceType, ResourceId)`, `(OccurredOn)` for the three-year-per-row retention scan → [ADR-0062](/decisions/adr-0062), [`customer-action-audit`](/domain/roles/customer-action-audit) |
| `UserConsent` | references `User`, `LegalDocument` (nullable, Restrict — a text a customer accepted can never be deleted from under the row); one row per `(UserId, ConsentType)` — the **current state**, overwritten on regrant, with `DocumentVersion` (`varchar(32)`, nullable — the document's effective date as `yyyy-MM-dd`) and `LegalDocumentId` written together (both null on consent types with no document and on every employee row, which stamps nothing until ADR-0041's agreement lands). A regrant under a *different document identity* moves the row. IP, user agent, version and document id **survive erasure** on the withdrawn row (`RetainedByPolicy`). The history of grants and withdrawals is the `customer.consent.*` rows in `CustomerActionAudit` → [ADR-0063](/decisions/adr-0063), [ADR-0062](/decisions/adr-0062) D4 |
| `UserStripeCustomer` | references `User` (Restrict), `Currency` (Restrict); unique `(UserId, CurrencyId)` with no tenant term, unique `StripeCustomerId` — the Stripe Customer that bills this user in **one** currency. Stripe locks a Customer to the currency of its first invoice, so a user holds one per currency and can re-subscribe to Plus in a new market; `User.StripeCustomerId` stays as the legacy field one-off order payments use, adopted as the first row for a currency it has only ever billed. GDPR erasure deletes the rows; `DeleteCurrency` answers `currency.in_use` for them. → [ADR-0059](/decisions/adr-0059) amendment |

## Ordering

```mermaid
erDiagram
  OrderNote }o--|| Order : "Order"
  CartPackageItem }o--|| Cart : "Cart"
  SavedAddress }o--|| Address : "Address"
  CartServiceItem }o--|| Cart : "Cart"
  OrderPhoto }o--|| Order : "Order"
  OrderIssue }o--|| Order : "Order"
  OrderService }o--|| Order : "Order"
  OrderService }o--|| Service : "Service"
  OrderPackage }o--|| Order : "Order"
  OrderPackage }o--|| Package : "Package"
  OrderPackageService }o--|| OrderPackage : "OrderPackage"
  OrderPackageService }o--|| Service : "Service"
  OrderExtra }o--|| Order : "Order"
  OrderExtra }o--|| Extra : "Extra"
  OrderStatusTrack }o--|| Order : "Order"
  Order }o--|| Currency : "Currency"
  Order }o--o| PromoCode : "PromoCode"
```

**`OrderService`, `OrderPackage` and `OrderExtra` are the order's line items** — what was actually
bought. An order with none bought nothing. `OrderPackageService` records which services a package
contained at purchase, so a later edit to the package cannot restate a historical order. `OrderExtra`
snapshots the extra's `Slug` and `UnitPrice` for the same reason. `OrderStatusTrack` is the append-only
status history; `Order.CurrentStatus` is a denormalisation of its latest row, and the history is
authoritative.

**`Order.CurrencyId` is the unit of every money column on the row**, stamped at creation from the
service address's country and never changed. The foreign key is `ON DELETE RESTRICT`, so a currency
that any order was ever priced in cannot be deleted. → [Business rules](/product/business-rules#price-stages)

| Entity | |
|---|---|
| `Order` | references `Currency` (Restrict), `PromoCode` (nullable, Restrict — the code that was actually honoured; a losing promo leaves it null), `Receipt` |
| `OrderEmployee` | — |
| `OrderExtra` | references `Order` (Cascade), `Extra` (Restrict — a catalogue extra referenced by any order line cannot be deleted, only deactivated); unique `(OrderId, ExtraId)` |
| `OrderPackageService` | references `OrderPackage` (Cascade), `Service` (Restrict); unique `(OrderPackageId, ServiceId)` |
| `OrderPhoto` | references `CapturedBy`, `Order` |
| `OrderNote` | references `Order` |
| `OrderIssue` | references `Order` |
| `OrderReceipt` | references `Language`; unique `(TenantId, ReceiptNumber)`, nulls not distinct — the number comes from a per-operator `FiscalCounter`, so two operators' first receipts of a year are the same string and the tenant term is what keeps them apart |
| `Address` | — |
| `RecurringBookingTemplate` | references `User` |
| `SavedAddress` | references `Address`, `User` |
| `Cart` | references `User` |
| `CartServiceItem` | references `Cart`, `Service` |
| `CartPackageItem` | references `Cart`, `Package` |
| `OrderService` | references `Order`, `Service` |
| `OrderPackage` | references `Order`, `Package` |
| `OrderStatusTrack` | references `Order` |

## Catalogue and configuration

```mermaid
erDiagram
  CountryConfiguration ||--|| Country : "Country"
  ServiceCity }o--|| Country : "Country"
  CompanyInfo }o--|| Country : "Country"
  PropertySizePreset }o--|| Country : "Country"
  ServicePrice }o--|| Service : "Service"
  ServicePrice }o--|| Currency : "Currency"
  PackagePrice }o--|| Package : "Package"
  PackagePrice }o--|| Currency : "Currency"
  ExtraPrice }o--|| Extra : "Extra"
  ExtraPrice }o--|| Currency : "Currency"
  LegalDocument }o--o| Country : "Country"
  LegalDocumentText }o--|| LegalDocument : "Document"
```

**The legal texts are stored documents, versioned by the date they start applying.** `LegalDocument`
is one version of one text — `Audience` (Customer | Employee), `Type` (TermsOfService |
PrivacyPolicy), `CountryId` (the market the copy is for; **null = the platform-wide text a market
without its own falls back to**), `EffectiveFrom` and `Version`, which *is* `EffectiveFrom` as
`yyyy-MM-dd` — with one `LegalDocumentText` per language (`Language`, `Title`, `ContentMarkdown`,
`ContentHash` SHA-256). Tenantless like `CountryConfiguration`: platform copy per market. **A document
in force is immutable** — the seeder that writes them refuses to change an in-force text, and a
wording change is a new document with a new date — so every text a customer ever accepted is still
in the table, pointed at by `UserConsent.LegalDocumentId`. The documents are seeded from embedded
markdown at every host start; there is no admin writer. → [ADR-0063](/decisions/adr-0063),
[`legal-document`](/domain/roles/legal-document)

**A catalogue entry carries no price.** `Service`, `Package` and `Extra` have no price column; a price
is a row in `ServicePrices` (`BasePrice`, `PerRoomPrice`), `PackagePrices` (`Price`) or `ExtraPrices`
(`Price`), one per (entry, currency). Nothing converts between rows — an entry with no row in a
currency is simply not sold in it. `Currency` has no exchange rate: its columns are `Code` (unique,
case-insensitive), `Symbol`, `Name`, `IsDefault` (a filtered unique index holds exactly one), `IsActive`
(the market switch — a new currency is born switched off), `LoyaltyPointsDivisor` (how much of the
currency earns one point; required before the market can be switched on) and `NoShowCredit` (the
apology credit paid on an order in this currency when its slot arrives with no cleaner; null = none —
not an activation gate).
→ [Platform expandability](/architecture/platform-expandability#market-switch)

**A market is not an entity.** It is a `Country` that is serviced, whose `CountryConfiguration.DefaultCurrencyCode`
names an active `Currency` — three existing rows joined by the anonymous `Market/GetOverview` read.
`Country` carries `IsoCode` (alpha-3, what clients persist) and `IsoAlpha2` (what the market chip
prints); `CountryConfiguration` carries, besides the fiscal and formatting columns, `InsuranceCoverageAmount`
— the one marketing figure in customer copy, a number in the country's currency, per country because
a policy is written per jurisdiction, null = the copy names no figure (CZE seeded at 1 000 000) — and
`IsDefaultMarket`, the market a customer surface pre-selects before any choice is made: a filtered
unique index (`IX_CountryConfigurations_IsDefaultMarket_Unique`, the `Currency.IsDefault` shape) holds
at most one, `SetDefaultMarket` is the only writer, CZE is seeded with it — and **`OperatorTenantId`**,
the operating company that serves the market (see [Tenancy](#tenancy-who-a-row-belongs-to)); a market
with none is not listed and cannot be flagged the default. → [ADR-0058](/decisions/adr-0058),
[ADR-0060](/decisions/adr-0060), [ADR-0061](/decisions/adr-0061)

| Entity | |
|---|---|
| `Service` | references `Category` |
| `Package` | — |
| `Extra` | — |
| `ServicePrice` | references `Service` (Cascade — a price has no meaning without the thing it prices), `Currency` (Restrict); unique `(ServiceId, CurrencyId)` |
| `PackagePrice` | references `Package` (Cascade), `Currency` (Restrict); unique `(PackageId, CurrencyId)` |
| `ExtraPrice` | references `Extra` (Cascade), `Currency` (Restrict); unique `(ExtraId, CurrencyId)` |
| `Currency` | — ; `NoShowCredit` (nullable) is the per-currency apology credit |
| `Country` | — ; `IsoCode` alpha-3 + `IsoAlpha2` (required, two letters) |
| `Language` | — |
| `CompanyInfo` | references `Country`; **stamped** — it is the operator's legal issuer identity on every receipt and invoice, so each operating company holds its own row |
| `CountryConfiguration` | references `Country`, `OperatorTenant` (nullable, Restrict — the company that serves this market); `InsuranceCoverageAmount` (nullable) is the per-country copy figure; `IsDefaultMarket` (filtered unique — at most one row) is the landing-page pre-selection. Tenantless: a per-country fact |
| `PropertySizePreset` | references `Country` (Restrict); unique `(CountryId, Code)` — the size chips a country's booking wizard offers |
| `ServiceCity` | references `Country` |
| `LegalDocument` | references `Country` (nullable, Restrict — null is the platform-wide copy); unique `(Audience, Type, CountryId, Version)`, indexed `(Audience, Type, EffectiveFrom)`; tenantless; **no delete path** |
| `LegalDocumentText` | references `Document` (Cascade); unique `(LegalDocumentId, Language)`; `ContentHash` is what the seeder compares and the admin preview shows |

## Money and payroll

```mermaid
erDiagram
  DisputeEvidence }o--|| Dispute : "Dispute"
  DisputeMessage }o--|| Dispute : "Dispute"
  DisputeLine }o--|| Dispute : "Dispute"
  Refund }o--|| Dispute : "Dispute"
  OrderEmployeePay }o--o| EmployeeInvoice : "EmployeeInvoice"
  OrderEmployeePay }o--|| Currency : "Currency"
  EmployeeInvoice }o--|| Currency : "Currency"
  EmployeePayConfig }o--|| Currency : "Currency"
  CreditAccount }o--|| User : "User"
  CreditTransaction }o--|| CreditAccount : "Account"
```

**Every money-carrying row names its currency.** A pay row is in the currency of the order that earned
it, an invoice is in the currency of the pay rows it invoices (one invoice per employee, period and
currency), a pay rate is an amount in a currency and counts for nothing in any other, and a customer
holds one credit account per currency. Every one of those foreign keys is `Restrict`, so a currency
that money was ever recorded in cannot be deleted. → [Pay and payouts](/flows/pay-and-payouts#one-invoice-per-currency)

| Entity | |
|---|---|
| `OrderEmployeePay` | references `Currency` (Restrict), `EmployeeInvoice` (nullable, SetNull), `Employee`, `Order`; unique `(OrderId, EmployeeId)` |
| `EmployeeInvoice` | references `Country`, `Currency` (Restrict), `Employee`, `Language`; unique `(EmployeeId, PayPeriodId, CurrencyId)`, unique `InvoiceNumber`, unique filtered `VariableSymbol` |
| `PayPeriod` | — |
| `EmployeePayConfig` | references `Currency`, `Employee`, `Package`, `Service`; unique `IX_EmployeePayConfigs_Tenant_Scope` on `(TenantId, EmployeeId, ServiceId, PackageId, CurrencyId)`, nulls not distinct — the tenant term is what lets two operators each hold a platform default (`EmployeeId` null) for the same service and currency |
| `CreditAccount` | references `User` (Restrict); `CurrencyId` is a plain column with **no declared foreign key**; unique `(UserId, CurrencyId)` |
| `CreditTransaction` | references `Account` (Cascade); unique `IdempotencyKey` — unfiltered and with no tenant term, because this is money and the backstop has to fire |
| `Refund` | references `Dispute`, `Order`, `Receipt` |
| `Dispute` | references `Order`, `User`; `TextRetainedUntil` (nullable, indexed) is the stamp an erasure sets to `now + retention.dispute_text.years` — the description, messages and resolution notes stay readable for defence of claims until the weekly `DisputeText` sweep finds the stamp past, blanks them and clears it (owner ruling 2026-09-14, Q-AUD-L3; the evidence blobs still go at erasure) → [ADR-0062](/decisions/adr-0062) D5 as amended |
| `DisputeLine` | references `Dispute` (Cascade), `Service` and `Package` (both Restrict, no navigation); unique `(DisputeId, ServiceId, PackageId)` — the order lines a dispute is about |
| `DisputeEvidence` | references `Dispute` |
| `DisputeMessage` | references `Author`, `Dispute` |
| `FiscalCounter` | — |

## Loyalty and membership

```mermaid
erDiagram
  UserMembership }o--|| MembershipPlan : "MembershipPlan"
  UserMembership }o--|| Currency : "Currency"
  MembershipPlanPrice }o--|| MembershipPlan : "MembershipPlan"
  MembershipPlanPrice }o--|| Currency : "Currency"
  Referral }o--|| ReferralCode : "ReferralCode"
  PromoCodeRedemption }o--|| PromoCode : "PromoCode"
  MembershipBenefitUsage }o--|| UserMembership : "UserMembership"
```

**A plan carries no price, like a catalogue entry.** `MembershipPlan` is the benefits and the billing
cadence; what it costs, and which Stripe Price charges it, is a `MembershipPlanPrice` row per
(plan, currency) — the `PackagePrice` shape plus the Stripe id. A plan with no row in a currency is not
on sale in that market. `UserMembership.CurrencyId` is the currency the subscription was created in
(the chosen market's), never updated: Stripe refuses a currency change on a live subscription, so a
plan swap reads the target plan's row in this currency. Both plan tables are platform config; the
membership is tenant-scoped. → [ADR-0059](/decisions/adr-0059)

| Entity | |
|---|---|
| `LoyaltyAccount` | references `User` |
| `LoyaltyTransaction` | — |
| `LoyaltyTierConfig` | — ; **tenantless** since [ADR-0061](/decisions/adr-0061) D7 — the brand's programme, sold identically by every operator (the `MembershipPlan` sibling); unique `Tier` |
| `ReferralCode` | references `User` |
| `Referral` | references `FirstQualifyingOrder`, `ReferralCode`, `Referred`, `Referrer` |
| `PromoCode` | references `Currency`; stamped — a code gives away the operator's money, and a sitewide campaign fans out to its operator's users only |
| `PromoCodeRedemption` | references `Order`, `PromoCode`, `User` |
| `MembershipPlan` | — (no price column) |
| `MembershipPlanPrice` | references `MembershipPlan` (Cascade), `Currency` (Restrict); unique `(MembershipPlanId, CurrencyId)` with no tenant term, unique `StripePriceId` |
| `UserMembership` | references `MembershipPlan`, `Currency` (Restrict — the subscription's currency for life), `User` |
| `MembershipBenefitUsage` | references `Order`, `UserMembership`, `User` |

## Platform

*Mostly referenced by id rather than by a configuration-declared relationship. The exceptions are
named on their rows: `OrderReview` and `OrderReviewLine` (declared, with delete behaviour) and
`PackageService` (the package–service join).*

| Entity | |
|---|---|
| `AdminActionAudit` | — |
| `CountryInvoiceConfig` | references `Country` |
| `DeadLetter` | — |
| `EmailTemplateTranslation` | references `Language` |
| `EmailTranslation` | — |
| `GdprRequest` | references `User`; `Status` gained a live `Failed` writer — an erasure that throws or is refused after its walk began leaves a `Failed` row written out of band (`ProcessedBy` the actor — `"self"`, the admin's e-mail or `"system"` — and `Notes` the exception type and message with any e-mail-shaped token blanked, **appended** per attempt within the 1 000-char bound, oldest text dropped first); a `Failed` row, or a `Processing` row older than 30 minutes, is what the daily retry sweep and the admin **Retry** re-run, and every row not yet `Completed` counts as *pending* for a second filing → [ADR-0062](/decisions/adr-0062) D5 as amended |
| `LiveActivityToken` | — |
| `OrderReview` | references `Order` |
| `OrderReviewLine` | references `Review` (Cascade), `Service` and `Package` (both Restrict, no navigation); unique `(OrderReviewId, ServiceId, PackageId)` — the per-line ratings under a review |
| `OutboxMessage` | — |
| `ServiceCategory` | — |
| `TenantConfiguration` | — |
| `UserNotification` | — |
| `UserNotificationPreferences` | references `User` |
| `PackageService` | references `Package`, `Service` — which services a package contains |
| `ProcessedStripeEvent` | — the replay guard; a unique index makes a redelivered webhook a no-op |
| `ProcessedMessage` | — the same guard for queue messages |
| `PayoutReferenceCounter` | — atomic `ON CONFLICT` numbering for payout references |
| `CampaignProgress` | — resumable cursor for a long-running campaign sweep |

## What the diagrams do not show

A foreign key is not an invariant. The rules that actually hold this data together — a seat is unique
per order, a promo code is redeemed once per user, a receipt number is never reused — are enforced by
unique indexes and append-only seams rather than by relationships, and are documented in
[Offerability](/domain/offerability) and [Order lifecycle](/domain/order-lifecycle).
