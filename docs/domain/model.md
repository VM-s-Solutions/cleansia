# Domain model

Generated from the EF Core entity configurations, not described from memory. A relationship on a
diagram is a `HasOne(...)` declared in a configuration file; if it is not there, it is not enforced.

One diagram per area, because a single picture of all 81 entities is a picture nobody reads. Entities
appear in the area they are owned by, not everywhere they are referenced.

::: tip Checking the count
`grep -c 'migrationBuilder.CreateTable' src/Cleansia.Infra.Database/Migrations/*_Initial.cs` — the
migration is regenerated rather than stacked, so it always reflects the current model. The count above
was 70 for long enough to be wrong by six before anyone noticed, and then 76 for long enough to be wrong
by five, which is why the check is written down rather than the number being trusted.
:::

## Identity and access

```mermaid
erDiagram
  Device }o--|| User : "User"
  Employee ||--|| User : "User"
  EmployeePayoutDetails ||--|| Employee : "Employee"
  EmployeePayoutDetails }o--o| Currency : "Currency"
  RefreshToken }o--|| User : "User"
  UserConsent }o--|| User : "User"
  EmployeeDocument }o--|| Employee : "Employee"
  DocumentDeletionRequest }o--|| EmployeeDocument : "Document"
  EmployeeDocumentRequirement }o--|| Country : "Country"
```

| Entity | |
|---|---|
| `User` | references `PreferredLanguage` |
| `Employee` | references `Nationality`, `User`, `WorkCountry` |
| `RefreshToken` | references `User` |
| `Device` | references `User` |
| `EmployeePayoutDetails` | references `BankCountry`, `Currency` (nullable, Restrict — the currency the cleaner declares the account holds), `Employee` (Cascade); unique `(TenantId, EmployeeId)` with nulls not distinct |
| `EmployeeDocument` | references `Employee`, `PreviousVersion` |
| `EmployeeDocumentRequirement` | references `Country` (Restrict); unique `(CountryId, DocumentType)` — which document types a country requires of a cleaner |
| `DocumentDeletionRequest` | references `Document` (Restrict) — a cleaner's request to have a document removed, answered by an admin |
| `EmployeeActionAudit` | — bare `EmployeeId` and `OrderId` scalars with no FK, because the act it records deletes the `OrderEmployee` row it describes and must survive an erased order |
| `UserConsent` | references `User` |

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
| `OrderReceipt` | references `Language` |
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
```

**A catalogue entry carries no price.** `Service`, `Package` and `Extra` have no price column; a price
is a row in `ServicePrices` (`BasePrice`, `PerRoomPrice`), `PackagePrices` (`Price`) or `ExtraPrices`
(`Price`), one per (entry, currency). Nothing converts between rows — an entry with no row in a
currency is simply not sold in it. `Currency` has no exchange rate: its columns are `Code` (unique,
case-insensitive), `Symbol`, `Name`, `IsDefault` (a filtered unique index holds exactly one), `IsActive`
(the market switch — a new currency is born switched off) and `LoyaltyPointsDivisor` (how much of the
currency earns one point; required before the market can be switched on).
→ [Platform expandability](/architecture/platform-expandability#market-switch)

| Entity | |
|---|---|
| `Service` | references `Category` |
| `Package` | — |
| `Extra` | — |
| `ServicePrice` | references `Service` (Cascade — a price has no meaning without the thing it prices), `Currency` (Restrict); unique `(ServiceId, CurrencyId)` |
| `PackagePrice` | references `Package` (Cascade), `Currency` (Restrict); unique `(PackageId, CurrencyId)` |
| `ExtraPrice` | references `Extra` (Cascade), `Currency` (Restrict); unique `(ExtraId, CurrencyId)` |
| `Currency` | — |
| `Country` | — |
| `Language` | — |
| `CompanyInfo` | references `Country` |
| `CountryConfiguration` | references `Country` |
| `PropertySizePreset` | references `Country` (Restrict); unique `(CountryId, Code)` — the size chips a country's booking wizard offers |
| `ServiceCity` | references `Country` |

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
| `EmployeePayConfig` | references `Currency`, `Employee`, `Package`, `Service` |
| `CreditAccount` | references `User` (Restrict); `CurrencyId` is a plain column with **no declared foreign key**; unique `(UserId, CurrencyId)` |
| `CreditTransaction` | references `Account` (Cascade); unique `IdempotencyKey` — unfiltered and with no tenant term, because this is money and the backstop has to fire |
| `Refund` | references `Dispute`, `Order`, `Receipt` |
| `Dispute` | references `Order`, `User` |
| `DisputeLine` | references `Dispute` (Cascade), `Service` and `Package` (both Restrict, no navigation); unique `(DisputeId, ServiceId, PackageId)` — the order lines a dispute is about |
| `DisputeEvidence` | references `Dispute` |
| `DisputeMessage` | references `Author`, `Dispute` |
| `FiscalCounter` | — |

## Loyalty and membership

```mermaid
erDiagram
  UserMembership }o--|| MembershipPlan : "MembershipPlan"
  Referral }o--|| ReferralCode : "ReferralCode"
  PromoCodeRedemption }o--|| PromoCode : "PromoCode"
  MembershipBenefitUsage }o--|| UserMembership : "UserMembership"
```

| Entity | |
|---|---|
| `LoyaltyAccount` | references `User` |
| `LoyaltyTransaction` | — |
| `LoyaltyTierConfig` | — |
| `ReferralCode` | references `User` |
| `Referral` | references `FirstQualifyingOrder`, `ReferralCode`, `Referred`, `Referrer` |
| `PromoCode` | references `Currency` |
| `PromoCodeRedemption` | references `Order`, `PromoCode`, `User` |
| `MembershipPlan` | — |
| `UserMembership` | references `MembershipPlan`, `User` |
| `MembershipBenefitUsage` | references `Order`, `UserMembership`, `User` |

## Platform

*No configuration-declared relationships between these entities — they are referenced by id.*

| Entity | |
|---|---|
| `AdminActionAudit` | — |
| `CountryInvoiceConfig` | references `Country` |
| `DeadLetter` | — |
| `EmailTemplateTranslation` | references `Language` |
| `EmailTranslation` | — |
| `GdprRequest` | references `User` |
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
