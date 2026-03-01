# Cleansia Architecture Improvements Roadmap

> Generated: 2026-02-07 | Updated: 2026-03-01 (Phase 11 architecture consistency)
> Status: In Progress

---

## Phase 1: Foundation

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Move secrets to Azure Key Vault / User Secrets | CRITICAL | 2 days | **Done** ✅ |
| Add .NET Aspire for orchestration | HIGH | 3 days | **Done** ✅ — AppHost + ServiceDefaults (Aspire 13.1.1 SDK) |
| Create `Cleansia.Web.Mobile` project | HIGH | 3 days | **Done** ✅ |
| Add Device entity for push notification tokens | MEDIUM | 1 day | **Done** ✅ |

---

## Phase 2: Multi-Tenancy Preparation — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Add `TenantId` to all entities | HIGH | 1 week | **Done** ✅ — `ITenantEntity` interface on 22 entities, `TenantId` on `Auditable` |
| Implement EF Core query filters | HIGH | 3 days | **Done** ✅ — Dynamic query filters for all `ITenantEntity` entities |
| Create tenant resolution middleware | HIGH | 2 days | **Done** ✅ — `ITenantProvider` resolves from JWT `tenant_id` claim |
| Database migration for multi-tenancy | HIGH | 2 days | **Done** ✅ — `AddTenantId` migration: 26 tables + indexes |

### Multi-Tenancy Approach

Implemented: **Shared database with TenantId filter**

- `TenantId` (nullable, max 26 chars) column on all `Auditable` entities
- 22 tenant-specific entities marked with `ITenantEntity` interface
- 6 global entities (Language, Currency, Country, CountryInvoiceConfig, EmailTranslation, EmailTemplateTranslation) are NOT filtered
- EF Core global query filters auto-scope all queries to current tenant
- `CommitAsync` auto-sets `TenantId` on new tenant entities
- JWT tokens include `tenant_id` claim when user has a TenantId
- Backward compatible: null TenantId = no filtering (single-tenant mode)

---

## Phase 3: Configuration & Country Expansion — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Create `TenantConfiguration` entity | HIGH | 2 days | **Done** ✅ — Key-value per tenant with unique (TenantId, Key) |
| Create `CountryConfiguration` entity | HIGH | 2 days | **Done** ✅ — Currency, language, VAT, tax, timezone, payment gateway per country |
| Implement configuration provider | MEDIUM | 3 days | **Done** ✅ — `IAppConfigurationProvider` with 3-level lookup |
| Add feature flags system | MEDIUM | 3 days | **Done** ✅ — `FeatureFlag` entity with global/country/tenant scopes |
| Wire up FeatureFlag CRUD service | MEDIUM | 2 days | **Done** ✅ — Repositories + MediatR handlers + Admin API controller |
| VAT/Tax per country | MEDIUM | 1 week | **Done** ✅ — `CountryConfiguration.StandardVatRate`, `ReducedVatRate`, `TaxIdLabel`, `TaxIdFormat` |

### Configuration Layers (Implemented)

```
Level 1: Global feature flags (scope = "global")
  - Feature flags with no ScopeValue

Level 2: Country-specific (CountryConfiguration entity)
  - StandardVatRate, ReducedVatRate, TaxIdLabel, TaxIdFormat
  - DefaultCurrencyCode, DefaultLanguageCode, DateFormat, TimeZoneId
  - DefaultPaymentGateway, LegalRequirementsJson, PhonePrefix

Level 3: Tenant-specific (TenantConfiguration entity)
  - Key-value pairs scoped by TenantId
  - Feature flags with scope = "tenant"
```

Feature flag resolution: tenant (most specific) → country → global (fallback)

---

## Phase 4: Android Refactoring — **COMPLETE** ✅

All 6 large files have been decomposed across 9 phases. No files over 500 lines remain.

| File | Before | After | Status |
|------|--------|-------|--------|
| `OrderDetailsScreen.kt` | 1985 | ~350 | **Done** ✅ |
| `AvailabilityCalendar.kt` | 1410 | ~300 | **Done** ✅ |
| `AnalyticsDetailScreen.kt` | 1190 | ~350 | **Done** ✅ |
| `ProfileScreen.kt` | 1263 | ~400 | **Done** ✅ |
| `DashboardScreen.kt` | 1062 | ~350 | **Done** ✅ |
| `AvailabilitySection.kt` | 1042 | ~350 | **Done** ✅ |

### Recommended Structure (OrderDetailsScreen example)

```
features/orders/
├── screens/
│   └── OrderDetailsScreen.kt          # ~200 lines (orchestrator only)
├── components/
│   ├── OrderHeader.kt                 # ~150 lines
│   ├── OrderCustomerInfo.kt           # ~100 lines
│   ├── OrderServiceDetails.kt         # ~150 lines
│   ├── OrderPaymentInfo.kt            # ~100 lines
│   ├── OrderTimeline.kt               # ~150 lines
│   ├── OrderActions.kt                # ~200 lines
│   └── OrderPhotosSection.kt          # ~200 lines
└── viewmodels/
    └── OrderDetailsViewModel.kt
```

---

## Phase 5: .NET 10 Migration — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Update SDK in `global.json` | MEDIUM | 1 hour | **Done** ✅ — SDK 10.0.103 |
| Update all `.csproj` to `net10.0` | MEDIUM | 1 hour | **Done** ✅ — All 17 projects |
| Update NuGet packages to .NET 10 versions | MEDIUM | 2 hours | **Done** ✅ — JwtBearer 10.0.2 |
| Test all functionality | MEDIUM | 2 days | **Done** ✅ — 56/56 tests pass |
| Update Docker base images | LOW | 1 hour | Pending |

### Benefits

- 10-20% faster JSON serialization
- Better GC performance
- Native AOT improvements (faster cold start)
- EF Core 10 query improvements

---

## Phase 6: .NET Aspire Adoption — **COMPLETE** ✅

### Implemented Architecture

```
Cleansia.AppHost/           # Aspire orchestrator (Aspire.AppHost.Sdk/13.1.1)
├── Program.cs              # PostgreSQL + 3 API services with pinned ports
Cleansia.ServiceDefaults/   # Shared defaults (OpenTelemetry, health checks, Sentry, resilience)
Cleansia.Web.Partner/       # Partner API (port 5000, references ServiceDefaults)
Cleansia.Web.Admin/         # Admin API (port 5001, references ServiceDefaults)
Cleansia.Web.Mobile/        # Mobile API (port 5002, references ServiceDefaults)
```

### What's Included

- **AppHost**: Orchestrates PostgreSQL (persistent container) + 3 API projects with fixed ports (IsProxied=false)
- **ServiceDefaults**: OpenTelemetry (metrics, traces, logging), Sentry error monitoring, health checks (/health, /alive), HTTP resilience
- **Integration**: All 3 Web projects call `AddServiceDefaults()` and `MapDefaultEndpoints()`
- **Backward compatible**: Supports existing `Startup` class pattern via `IServiceCollection` overload

### Benefits

- Centralized service orchestration
- Built-in OpenTelemetry (logging, metrics, traces)
- Service discovery (no hardcoded URLs)
- Easy local development of multi-service setup
- Cloud-ready deployment to Azure Container Apps

---

## Phase 7: Error Monitoring & Observability — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Add Sentry to 3 backend APIs | CRITICAL | 1 day | **Done** ✅ — `Sentry.AspNetCore` 6.1.0 + OpenTelemetry integration via ServiceDefaults |
| Add Sentry to 2 Angular apps | CRITICAL | 1 day | **Done** ✅ — `@sentry/angular` with ErrorHandler + TraceService |
| Move CORS origins to configuration | HIGH | 1 hour | **Done** ✅ — `CorsOrigins` array in appsettings.json for all 3 APIs |

### Sentry Integration

- **Backend**: Sentry configured in `ServiceDefaults/Extensions.cs` via `UseSentryMonitoring()` extension method
- **Frontend**: Sentry initialized in `main.ts` before Angular bootstrap; `ErrorHandler` + `TraceService` providers in `app.config.ts`
- **DSN**: Empty by default — set via `Sentry:Dsn` in appsettings or `sentryDsn` in environment.ts when account is created
- **Traces**: 20% sample rate in production, 100% in development

---

## Security Issues to Address

| Issue | Priority | Status |
|-------|----------|--------|
| Secrets exposed in `appsettings.json` (SendGrid, Stripe, JWT) | CRITICAL | **Done** ✅ — Moved to User Secrets |
| CORS allows `AllowAnyOrigin` | HIGH | **Done** ✅ — Restricted to known origins (configurable via `CorsOrigins` in appsettings) |
| No rate limiting on auth endpoints | HIGH | **Done** ✅ — Fixed window 10 req/min |
| No API versioning | MEDIUM | **Done** ✅ — Asp.Versioning.Mvc 8.1.0, URL segment `/api/v1/...` |
| Old `Microsoft.AspNetCore.Http 2.3.0` in Infra.Database | MEDIUM | **Done** ✅ — Replaced with FrameworkReference |

---

## Decision Points

1. ~~**Aspire adoption**~~ ✅ Done — All 3 APIs orchestrated via Aspire 13.1.1 with pinned ports
2. ~~**Multi-tenancy**~~ ✅ Done — Shared DB with TenantId filter (backward compatible)
3. ~~**Configuration storage**~~ ✅ Done — Database tables with `IAppConfigurationProvider`
4. ~~**Android refactoring**~~ ✅ Done — 9-phase refactoring completed
5. **Monolith** - Stay monolith (recommended for now), prepare extraction boundaries

## Phase 8: GDPR Compliance — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Data export endpoints (self-service + admin) | CRITICAL | 2 days | **Done** ✅ — Full PII export as JSON |
| Account deletion with PII anonymization | CRITICAL | 3 days | **Done** ✅ — Anonymize + blob cleanup |
| Consent tracking system | CRITICAL | 2 days | **Done** ✅ — Grant/Withdraw/View consents |
| GDPR audit log | CRITICAL | 1 day | **Done** ✅ — GdprRequest entity tracking all operations |

### GDPR Implementation

**Data Export (Art. 15/20 — Right of Access / Data Portability)**
- `GET /api/v1/gdpr/export` — Self-service: exports all user PII as structured JSON
- `GET /api/v1/admingdpr/export/{userId}` — Admin: export any user's data
- Includes: profile, address, employee details, orders, documents (metadata), invoices, consents
- Creates `GdprRequest` audit entry with type "Export"

**Account Deletion (Art. 17 — Right to Erasure)**
- `POST /api/v1/gdpr/delete-account` — Self-service account deletion
- `POST /api/v1/admingdpr/delete-account/{userId}` — Admin deletion
- PII anonymization (User, Employee, Address, Order customer data → `[DELETED]`)
- Blob deletion (profile photos, employee documents, order photos)
- Hard deletes: Device records (push tokens), Cart records
- Preserves: orders (anonymized, financial audit), invoices (tax compliance), disputes (legal)
- Withdraws all active consents on deletion

**Consent Tracking (Art. 7 — Conditions for Consent)**
- `UserConsent` entity with `ConsentType`, `IsGranted`, `GrantedAt`, `WithdrawnAt`, IP/UserAgent
- Types: TermsOfService, PrivacyPolicy, MarketingEmails, DataProcessing
- Unique constraint on (UserId, ConsentType) — one record per type, re-grantable
- Full CRUD: Grant, Withdraw, View (self-service + admin)

**Entities Added:**
- `UserConsent` — Consent records with audit trail
- `GdprRequest` — Audit log for export/deletion operations

**API Endpoints (14 total across 3 APIs):**
- Partner API: 5 endpoints (`GdprController`)
- Mobile API: 5 endpoints (`GdprController`)
- Admin API: 4 endpoints (`AdminGdprController`)

---

## Phase 9: Data Retention Policies — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Automated data retention background job | HIGH | 2 days | **Done** ✅ — Weekly Hangfire job with 6 cleanup tasks |

### Data Retention Implementation

Single `DataRetentionBackgroundService` registered as a Hangfire recurring job (`data-retention-cleanup`, weekly Sunday 3 AM UTC). Controlled by `DataRetentionJobEnabled` feature flag (disabled by default until seeded). Each task reads retention periods from `TenantConfiguration` with hardcoded defaults as fallback.

**6 Cleanup Tasks:**
1. **Expired User Codes** — Nulls out stale `ConfirmationCode`/`ResetPasswordCode` (15-min expiry)
2. **Stale Device Tokens** — Hard-deletes devices inactive for 90+ days (configurable)
3. **Old GDPR Requests** — Anonymizes `ProcessedBy` on completed requests older than 3 years
4. **Order Customer PII** — Calls `AnonymizeCustomerData()` on completed orders older than 2 years
5. **Withdrawn Consents** — Hard-deletes withdrawn consent records older than 3 years
6. **Superseded Documents** — Deletes blobs + DB records for soft-deleted documents older than 1 year

**Error isolation**: Each task runs in try/catch — failure in one does not abort others.
**Batch processing**: 100 records per batch to limit transaction size.

---

## Phase 10: Country Expansion — Remove Czech-Specific Assumptions — **COMPLETE** ✅

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Rename `ICO` → `TaxId` on Employee entity | CRITICAL | 1 hour | **Done** ✅ |
| Remove TaxId from profile completion checks | HIGH | 30 min | **Done** ✅ |
| Make `VariableSymbol` nullable, add `PaymentReference` | HIGH | 1 hour | **Done** ✅ |
| Replace hardcoded CZK/cs fallbacks with EUR/en | CRITICAL | 1 hour | **Done** ✅ |
| Wire `CountryConfiguration.DateFormat` into templates | HIGH | 1 hour | **Done** ✅ |
| Fix ReceiptService hardcoded template + currency | HIGH | 30 min | **Done** ✅ |
| Add `State` field to Address entity | MEDIUM | 30 min | **Done** ✅ |

### What Changed

**Domain Model:**
- `Employee.ICO` → `Employee.TaxId` — generic tax identifier, no longer Czech-specific
- `Employee.IsProfileComplete()` — TaxId removed from required fields (country-dependent)
- `EmployeeInvoice.VariableSymbol` — now nullable (Czech/SK banking only)
- `EmployeeInvoice.PaymentReference` — new generic payment reference field (max 50 chars)
- `Address.State` — new optional field for countries requiring state/region

**Currency & Language Fallbacks:**
- CZK/Kč → EUR/€ as generic fallback currency
- `"cs"` → `"en"` as language fallback
- Removed `Constants.Currency.CZK` and `Constants.Language.Czech`

**Template Engine:**
- `formatDate` / `formatDateTime` Handlebars helpers now accept optional format parameter
- `CreatePdfData()` accepts `dateFormat` parameter, populated from `CountryConfiguration.DateFormat`
- PayPeriodBackgroundService and RegenerateInvoicePdf look up country date format

**Receipt Service:**
- Template lookup via `IReceiptTemplateRepository` instead of hardcoded `"receipt-template-v1.html"`
- `$` fallback → `€` fallback for currency symbol

**Repository Changes:**
- `IInvoiceTemplateRepository.GetActiveByCountryAndLanguageAsync` — countryId now nullable (matches by language only when null)
- `IReceiptTemplateRepository.GetActiveByCountryAndLanguageAsync` — same
- Null countryId handled gracefully throughout (company info, invoice context, template lookup)

**Migration Required:** Rename `Employees.ICO` → `TaxId`, make `VariableSymbol` nullable, add `PaymentReference`, add `Addresses.State`, update filtered unique index on `VariableSymbol`.

---

## Current Architecture

```
Solution: Cleansia.Api.sln + Cleansia.Api.slnx

00 Orchestration
├── Cleansia.AppHost           # Aspire 13.1.1 — PostgreSQL + 3 API services
└── Cleansia.ServiceDefaults   # OpenTelemetry, health checks, Sentry, resilience

05 Web (Presentation)
├── Cleansia.Web.Partner       # Partner API (18 controllers, port 5000)
├── Cleansia.Web.Admin         # Admin API (20 controllers, port 5001)
└── Cleansia.Web.Mobile        # Mobile API (10 controllers, port 5002)

04 Core (Business Logic)
├── Cleansia.Core.AppServices  # CQRS handlers, features, DTOs, FluentValidation
├── Cleansia.Core.Domain       # Entities, domain logic, repository interfaces
├── Cleansia.Core.Blobs.Abstractions
└── Cleansia.Core.Clients.Abstractions

03 Infrastructure
├── Cleansia.Infra.Database    # EF Core 10, PostgreSQL, repositories
├── Cleansia.Infra.Services    # PDF, Email, Blob
├── Cleansia.Infra.Common      # Shared utilities, BusinessResult
├── Cleansia.Infra.Clients     # SendGrid, Stripe
└── Cleansia.Infra.Azure.Storage.Blobs

02 Configuration
└── Cleansia.Config            # DI registration, MediatR, FluentValidation

99 Test
├── Cleansia.Tests             # Unit tests (56 tests)
├── Cleansia.IntegrationTests  # PostgreSQL container integration tests
└── Cleansia.TestUtilities
```

### Tech Stack

- **Runtime**: .NET 10 (SDK 10.0.103)
- **Orchestration**: .NET Aspire 13.1.1
- **Database**: PostgreSQL via EF Core 10
- **Frontend (Web)**: Angular 19.2, NgRx, PrimeNG, Nx monorepo (3 languages: cs/en/pl)
- **Frontend (Mobile)**: Kotlin/Jetpack Compose (Android, 3 languages: cs/en/pl)
- **Error Monitoring**: Sentry (backend + frontend)
- **Background Jobs**: Hangfire
- **Email**: SendGrid
- **Payments**: Stripe
- **Storage**: Azure Blob Storage

---

## Phase 11: Architecture Consistency & Code Quality — **COMPLETE** ✅

> Generated: 2026-03-01
> Status: Complete

### Overview

Architecture review identified 27 gaps across frontend (12) and backend (15). After investigation, some reported gaps were false positives (e.g., all entities already implement `ITenantEntity`). This phase addresses all confirmed, actionable issues.

### Backend Fixes

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Consolidate 3 duplicate ApiController base classes into shared `Cleansia.Config` | CRITICAL | 2 hours | **Done** ✅ |
| Add `[Authorize]` to base controller + `[AllowAnonymous]` on auth endpoints | CRITICAL | 1 hour | **Done** ✅ |
| Consolidate duplicate Startup.cs across 3 web projects into shared base | HIGH | 3 hours | **Done** ✅ |
| Standardize CQRS handlers (GetInvoiceById, DownloadInvoice → IQuery/BusinessResult) | HIGH | 2 hours | **Done** ✅ |
| Fix all controllers to use HandleResult consistently | HIGH | 1 hour | **Done** ✅ |
| Move duplicate EnumSchemaFilter to shared project | LOW | 30 min | Planned |

#### ApiController Consolidation

Created single `CleansiaApiController` in `Cleansia.Config/Abstractions/` with the superset of all methods (HandleResult, HandleRedirectResult, CreateProblemDetails with `GroupBy` for robust duplicate error handling). All 3 web projects (`Cleansia.Web`, `Cleansia.Web.Admin`, `Cleansia.Web.Mobile`) now inherit from this single base via thin wrappers.

#### Authorization Gap

`[Authorize]` attribute added to `CleansiaApiController` base class. Auth controllers override with `[AllowAnonymous]` on login/register endpoints (Partner, Mobile, and Admin).

#### Startup.cs Consolidation

Created `CleansiaStartupBase` in `Cleansia.Config/Abstractions/` with virtual properties for `CorsPolicy` and `SwaggerTitle`. Each web project's `Startup` class inherits and overrides only the 2 values that differ.

#### CQRS Handler Standardization

Converted invoice handlers from non-standard `IRequest`/`IRequestHandler` (returning nullable types) to the standard `IQuery`/`IQueryHandler` pattern (returning `BusinessResult<T>`):

- **`GetInvoiceById`**: `IRequest<EmployeeInvoiceDetailDto?>` → `IQuery<EmployeeInvoiceDetailDto>` with `BusinessResult<T>`
- **`DownloadInvoice`**: `IRequest<Response?>` → `IQuery<Response>` with `BusinessResult<T>`, added `Validator` class with FluentValidation

All 3 `EmployeePayrollController` variants updated to use `HandleResult<T>()` instead of manual null checks / `NotFound()`. Fixed a bug in the Mobile controller that returned `Ok(null)` for not-found invoices.

### Frontend Fixes

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Standardize facade subscription cleanup (4 facades) | CRITICAL | 1 hour | **Done** ✅ |
| Extract duplicate `toSnakeCase`/`toKebabCase` to shared utils | HIGH | 30 min | **Done** ✅ |
| Unify orders & invoices pages (list, detail, filters, help cards) | HIGH | 4 hours | **Done** ✅ |
| Replace `<cleansia-loader>` with skeleton loaders on all pages | HIGH | 2 hours | **Done** ✅ |
| Add missing i18n keys + replace hardcoded error strings | HIGH | 1 hour | **Done** ✅ |
| Add Polish language support | MEDIUM | 2 hours | **Done** ✅ |
| Row click navigation on tables | MEDIUM | 30 min | **Done** ✅ |
| Invoice detail shadow-box sections (match order detail) | MEDIUM | 30 min | **Done** ✅ |
| Language switcher border consistency | LOW | 15 min | **Done** ✅ |

#### Facade Subscription Cleanup

All partner app facades now extend `UnsubscribeControlDirective` and pipe subscriptions through `takeUntil(this.destroyed$)`: `ConfirmEmailFacade`, `OrderDetailsFacade`, `InvoiceDetailFacade`, `InvoicesFacade` (migrated from manual `destroy$` Subject), `OrdersFacade`, `DashboardFacade`, `ProfileFacade`, `LoginFacade`, `RegisterFacade`, `ForgotPasswordFacade`.

#### Utility Deduplication

Created `StringTransformationUtils` in `libs/shared/utils/src/string-transformation.utils.ts` with `toSnakeCase()` (including `KNOWN_SNAKE_CASE_MAPPINGS` for enum values like `InProgress` → `in_progress`) and `toKebabCase()`. Removed duplicate implementations from `orders.component.ts` and `order-details.component.ts`.

#### Orders & Invoices Page Unification

Both pages now follow identical patterns:
- Reactive filter forms with debounced auto-apply (500ms) and filter chips
- Help cards with workflow steps and status flow explanations
- Consistent table definitions with translated columns
- Sort state tracking to prevent duplicate requests
- `(rowClick)` handlers for row-level navigation to detail pages
- Cursor pointer on table rows

Detail pages unified with:
- `CleansiaDetailSkeletonComponent` for loading state
- Consistent breadcrumb navigation with back button
- Shadow-box `cleansia-section` styling (white card, border, shadow)
- Error/not-found states using translated messages via `TranslateService`
- `retryLoadOrder`/`retryLoadInvoice` using route params (not stale signals)

#### Skeleton Loader Components

Created 4 reusable skeleton components in `libs/shared/components/src/lib/cleansia-skeleton/`:
- `CleansiaDashboardSkeletonComponent` — 4 stat card placeholders
- `CleansiaTableSkeletonComponent` — Title, filter bar, 6 table rows
- `CleansiaFormSkeletonComponent` — Form sections with field placeholders
- `CleansiaDetailSkeletonComponent` — Breadcrumb, header card, 3 info sections

Migrated from `<cleansia-loader>` to skeletons: Dashboard, Orders, Invoices, Order Detail, Invoice Detail, Profile.

#### Translation Fixes

- Added missing `pages.invoice_detail.title` key to all 3 language files
- Added `not_found_message` and `load_failed` keys for both order_details and invoice_detail
- Replaced hardcoded English error strings in `OrderDetailsFacade` and `InvoiceDetailFacade` with `translateService.instant()` calls
- All enums use consistent translation key pattern: `enums.order_status.${toSnakeCase(status)}`

#### Polish Language Support

Added `pl.json` translation files for both Partner and Admin Angular apps (~1700 keys each). Updated Android `locales_config.xml` and `strings.xml`. Language switcher updated with Polish flag/code mapping.

### Confirmed False Positives (No Action Needed)

| Reported Gap | Reason |
|-------------|--------|
| Missing `ITenantEntity` on Service, Package, PayPeriod, templates | All 26 entities already implement `ITenantEntity` ✅ |
| Mixed NgRx/Signals state management | Intentional: NgRx for cross-feature state (user, dashboard stats), Signals for local component state (order details, invoice detail) |

### Architecture Decision: Multi-Tenancy Deployment

**Decision**: Keep **single-app, multi-tenant deployment** (not per-domain separation).

**Rationale**:
- 3-level config hierarchy already built (global → `CountryConfiguration` → `TenantConfiguration`)
- Feature flags support per-tenant and per-country scoping
- JWT-based tenant resolution works across all domains
- Per-domain deployment would mean N copies of the same app with hardcoded settings — the exact problem `CountryConfiguration` already solves
- Single DB enables cross-tenant reporting and simpler schema migrations

**Future enhancement**: Add domain→tenant mapping middleware if needed (maps `cleansia.cz` → CZ tenant, `cleansia.pl` → PL tenant). This is a small middleware addition, not an architectural change.

---

## Remaining Work

| Task | Priority | Status |
|------|----------|--------|
| Move duplicate EnumSchemaFilter to shared project | LOW | Planned |
| EF Core migration for country expansion schema changes | HIGH | Pending |
| Expand test coverage (target 80%+) | HIGH | Planned |
| Update Docker base images to .NET 10 | LOW | Planned |
| Sentry account setup + DSN configuration | MEDIUM | Pending account |
