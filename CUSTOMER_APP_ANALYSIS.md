# Cleansia Customer App — Feature Analysis & Implementation Roadmap

**Last Updated**: 2026-03-02
**Version**: 1.2.0
**For**: Development Team

---

## Table of Contents

1. [Current State](#current-state)
2. [Current App Features](#current-app-features)
3. [Known Bugs & Issues](#known-bugs--issues)
4. [Backend API Capabilities](#backend-api-capabilities)
5. [New Backend: Cleansia.Web.Customer API Project](#new-backend-cleansiawebcustomer-api-project)
6. [Gap Analysis](#gap-analysis)
7. [Recommended Architecture](#recommended-architecture)
8. [Reusable Shared Components](#reusable-shared-components)
9. [Summary](#summary)

---

## Current State

The customer-facing app (`cleansia.app`) is a **static marketing landing page** built with Angular 19 standalone components and PrimeNG. It has **no API integration**, **no authentication**, and **no order creation flow**. All content is hardcoded in Czech.

**Tech Stack:**
- Angular 19.2 (standalone components)
- PrimeNG (Menubar, Card, Carousel, Accordion, Button, InputText, Avatar)
- PrimeFlex CSS framework
- @ngx-translate (recently added to fix TranslateStore bug)
- No state management (no NgRx)
- No HTTP API calls
- No environment configuration

**Entry Point:** Single route (`/`) loads `CleansiaComponent` from `@cleansia/cleansia` library.

**Files:**
- Component: `libs/cleansia/src/lib/cleansia/cleansia.ts` (235 lines)
- Template: `libs/cleansia/src/lib/cleansia/cleansia.html` (~270 lines)
- Styles: `libs/cleansia/src/lib/cleansia/cleansia.scss` (~370 lines)
- App Config: `apps/cleansia.app/src/app/app.config.ts`
- App Root: `apps/cleansia.app/src/app/app.ts`
- Routes: `apps/cleansia.app/src/app/app.routes.ts`
- Translation: `apps/cleansia.app/src/assets/i18n/cs.json` (minimal — only `global.messages`)

---

## Current App Features

### 1. Header / Navigation

| Element | Details | Status |
|---------|---------|--------|
| Logo | `logo.png` (40x40px, lazy-loaded) | Working |
| Nav Menu | PrimeNG Menubar: "Úvod" (`/home`), "Služby" (`/services`), "FAQ" (`/faq`) | **Broken** — routes don't exist |
| Phone Link | `+420 739 788 108` (tel: link) | Working |
| CTA Button | "Zanechat poptávku" (Request a Quote) | **Not functional** — no click handler |

### 2. Hero Section

| Element | Details | Status |
|---------|---------|--------|
| Heading | Lorem ipsum placeholder | Static placeholder |
| Background | `typ.png` (1.7MB, lazy-loaded) | Working |
| Layout | 50svh height | Working |

### 3. "How It Works" Process Section

| Element | Details | Status |
|---------|---------|--------|
| Title | "Jak funguje celý proces?" | Static |
| Step Cards | 3 cards: Choose service → We come to you → Cleanliness without worry | Static |
| Counter | Animated count from 0 → 2,733 (completed cleanings) | **Functional** — IntersectionObserver triggers animation |
| Background | Decorative rocket image | Working |

### 4. Benefits Section

| Element | Details | Status |
|---------|---------|--------|
| Title | "Proč je mobilní čištění lepší než běžný úklid?" | Static |
| Bullet Points | 4 items with checkmark icons | Static |
| Background | Decorative images | Working |

### 5. Services & Pricing Section

| Element | Details | Status |
|---------|---------|--------|
| Title | "Služby a ceník" | Static |
| Service Cards | 6 cards with frosted-glass effect | Static, **hardcoded** |
| Extra Services | Stain removal +500 Kč, Odor neutralization +500 Kč | Static |

**Hardcoded Services:**

| Service | Price |
|---------|-------|
| Čištění pohovky (Sofa Cleaning) | from 1,500 Kč |
| Čištění matrace (Mattress Cleaning) | from 1,800 Kč |
| Čištění koberce (Carpet Cleaning) | from 1,500 Kč |
| Mytí oken (Window Washing) | from 800 Kč |
| Generální úklid (General Cleaning) | from 2,500 Kč |
| Úklid po malování (Post-Painting Cleanup) | from 2,000 Kč |

> **Note:** These do NOT match the backend seed data (10 services with different pricing). The landing page prices are marketing-oriented "from" prices.

### 6. Before/After Gallery

| Element | Details | Status |
|---------|---------|--------|
| Carousel | PrimeNG carousel, 6 items | **Functional** |
| Images | `pes.webp` and `kocka.webp` duplicated 3x each | Placeholder images |
| Hover Effect | CSS clip-path reveals before/after comparison | Working |

### 7. Reviews / Testimonials

| Element | Details | Status |
|---------|---------|--------|
| Google Rating | 4.6/5 stars | **Hardcoded** |
| Facebook Rating | 4.8/5 stars | **Hardcoded** |
| Testimonial Carousel | 1 review from "Honza" | **Hardcoded**, only 1 item |
| Avatar | External URL (randomuser.me) | Working |

### 8. FAQ Section

| Element | Details | Status |
|---------|---------|--------|
| Accordion | PrimeNG accordion with ngFor | **Functional** |
| Questions | 2 items only | **Hardcoded** in component |

**Current FAQs:**
1. "Jak dlouho trvá čištění?" → "Čištění trvá 2-4 hodiny."
2. "Používáte bezpečné prostředky?" → "Ano, pouze certifikované prostředky."

### 9. Footer

| Element | Details | Status |
|---------|---------|--------|
| Contact Phone | "+7 (999) 123-45-67" | **Wrong number** — doesn't match header |
| Contact Email | info@cleansia.cz | Static |
| Quote Form | Name + Phone inputs, Submit button | **Partially broken** (see bugs) |
| Social Links | Instagram, VK, Telegram | **Empty hrefs** — not linked |
| Copyright | © 2025 Cleansia s.r.o. | Outdated year |

### 10. Scroll Navigation

| Element | Details | Status |
|---------|---------|--------|
| Up/Down Arrows | Fixed position bottom-right | **Functional** |
| Behavior | Smooth scroll between fullscreen sections | Working |
| Visibility | Up hidden at top, Down hidden at bottom | Working |

### 11. Image Lazy Loading

| Element | Details | Status |
|---------|---------|--------|
| Implementation | IntersectionObserver in constructor | **Functional** |
| Fallback | Scroll-based lazy loading for older browsers | Working |
| Targets | Elements with `.lazy` class and `data-src`/`data-bg` attributes | Working |

---

## Known Bugs & Issues

### Critical

| # | Bug | Details |
|---|-----|---------|
| 1 | **FormsModule not imported** | Template uses `ngModel` bindings in the contact form, but `FormsModule` is not in the component's `imports` array. Form inputs won't bind properly. |
| 2 | **Navigation links broken** | `navItems` reference routes `/home`, `/services`, `/faq` — none exist in `app.routes.ts`. Only the root route (`/`) is defined. |
| 3 | **Contact form makes no API call** | `submitRequest()` only calls `snackbarService.showSuccessTranslated()` and resets the form. No data is sent anywhere. |

### Medium

| # | Bug | Details |
|---|-----|---------|
| 4 | **Phone number mismatch** | Header: `+420 739 788 108` / Footer: `+7 (999) 123-45-67` (Russian format placeholder) |
| 5 | **Social media links empty** | Instagram, VK, Telegram links have `href="#"` — they navigate to page top instead of external URLs. |
| 6 | **Single testimonial in carousel** | Carousel component wrapping just 1 item looks awkward with navigation arrows. |
| 7 | **Copyright year outdated** | Shows "© 2025" instead of "© 2026". |

### Low

| # | Bug | Details |
|---|-----|---------|
| 8 | **Unused assets** | `kuchyne.webp` (78KB) and `parallex.webp` (5.3MB) are in `/src` but never referenced. |
| 9 | **Large image files** | `typ.png` (1.7MB), `typnapad.png` (1.7MB), `typpremyslismejese.png` (1.7MB), `bg-typek-ready.png` (1.9MB) — should be optimized to WebP. |
| 10 | **External avatar dependency** | Testimonial uses `randomuser.me` API for avatar — will fail if service is down. |
| 11 | **NgFor import** | `NgFor` imported from `@angular/common` but `@for` is preferred in Angular 19. |
| 12 | **No environment files** | Unlike partner/admin apps, there's no `environment.ts` or `environment.prod.ts`. |

---

## Backend API Capabilities

The backend already has extensive customer-ready CQRS handlers. The handlers are shared across all API projects via `Cleansia.Core.AppServices` — controllers are thin wrappers that call `Mediator.Send()`. Currently, customer-relevant endpoints are **scattered across the Mobile and Partner APIs**. A dedicated `Cleansia.Web.Customer` project will consolidate these into a single, clean API surface.

### Existing Handlers Available for Customer Use

#### Authentication

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `Register` | Command | No | Customer registration (creates user + cart, sends confirmation email) |
| `Login` | Command | No | Email/password login, returns JWT |
| `GoogleAuth` | Command | No | Google OAuth integration |
| `ConfirmUserEmail` | Command | No | Confirm email with 6-digit code |
| `ResendConfirmationEmail` | Command | No | Resend confirmation email |
| `RequestPasswordChange` | Command | No | Password reset via email token |

#### Order Management

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `CreateOrder` | Command | Rate-limited | Create order with services/packages/extras/payment |
| `GetPagedOrders` | Query | Yes | Paginated order list with filters |
| `GetOrderDetails` | Query | Yes | Full order details (services, status history, employees) |
| `DownloadOrderReceipt` | Query | Yes | Download receipt as PDF |
| `GetOrderPhotos` | Query | Yes | Retrieve order before/after photos |
| `ReportOrderIssue` | Command | Yes | Report problem with an order |

**CreateOrder accepts:**
- Customer details (name, email, phone)
- Address (street, city, postal code, country)
- Services array (serviceId + quantity)
- Packages array (packageId + quantity)
- Extras (key-value pairs)
- Number of rooms and bathrooms
- Cleaning date
- Payment type: `Card` or `Cash`
- Currency

**Card payment flow:** Creates Stripe checkout session → redirects customer → Stripe webhook confirms payment → receipt generated and emailed.

**Cash payment flow:** Order created immediately → receipt generated and emailed.

#### Service & Package Catalog

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `GetServiceOverview` | Query | No | All available cleaning services |
| `GetPackageOverview` | Query | No | All available cleaning packages |

**Seeded Services (10):**

| Service | Base Price | Per Room | Duration |
|---------|-----------|----------|----------|
| General Cleaning | 500 CZK | +150/room | 120 min |
| Deep Cleaning | 800 CZK | +250/room | 180 min |
| Bathroom Cleaning | 300 CZK | — | 45 min |
| Kitchen Deep Clean | 400 CZK | — | 90 min |
| Window Cleaning | 200 CZK | +50/room | 60 min |
| Carpet Cleaning | 350 CZK | +100/room | 90 min |
| Upholstery Cleaning | 450 CZK | — | 75 min |
| Post-Construction Cleanup | 1,200 CZK | +300/room | 240 min |
| Move-in/Move-out Cleaning | 1,000 CZK | +200/room | 180 min |
| Eco-Friendly Cleaning | 600 CZK | +180/room | 135 min |

**Seeded Packages (8):**

| Package | Price |
|---------|-------|
| Essential Clean | 799 CZK |
| Complete Home Clean | 1,299 CZK |
| Deep Clean Premium | 1,799 CZK |
| Kitchen & Bathroom Focus | 999 CZK |
| Eco-Green Package | 1,499 CZK |
| Moving Day Special | 2,299 CZK |
| Post-Renovation Clean | 2,799 CZK |
| Luxury Full Service | 3,499 CZK |

#### Payment Processing

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `HandlePaymentNotification` | Command | No (Stripe signature) | Stripe webhook — confirms payment, generates receipt |

#### Dispute Management

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `CreateDispute` | Command | Yes | Create dispute for an order |
| `GetDisputeDetails` | Query | Yes | Retrieve dispute with messages |
| `GetPagedDisputes` | Query | Yes | List disputes with pagination |

#### GDPR Compliance

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `ExportUserData` | Query | Yes | Export all personal data as JSON |
| `DeleteUserAccount` | Command | Yes | Account deletion with PII anonymization |
| `GetUserConsents` | Query | Yes | View all consent statuses |
| `GrantConsent` | Command | Yes | Grant consent |
| `WithdrawConsent` | Command | Yes | Withdraw consent |

#### Localization

| Handler | Type | Auth | Description |
|---------|------|------|-------------|
| `GetCountryOverview` | Query | No | Available countries |
| `GetLanguageOverview` | Query | No | Supported languages |
| `CheckFeatureFlag` | Query | Yes | Check feature flags |

### Supported Currencies

CZK, EUR, USD, GBP, PLN, CHF, SEK, NOK, DKK, HUF, RON, BGN

### Supported Languages

Czech (cs), English (en), Polish (pl), Russian (ru)

---

## New Backend: Cleansia.Web.Customer API Project

### Why a Separate API?

Currently, customer-relevant endpoints are split across:
- **Mobile API** (`Cleansia.Web.Mobile`) — Auth, Orders, GDPR, Localization
- **Partner API** (`Cleansia.Web`) — Payments, Disputes, Services, Packages

This means the customer frontend would need to call **two different API base URLs**, which is messy. A dedicated `Cleansia.Web.Customer` project provides:

- Single API surface for the customer frontend
- Customer-specific authorization policies (no employee/admin endpoints exposed)
- Customer-specific rate limiting (stricter on order creation, auth)
- Clean Swagger docs with only customer-relevant endpoints
- Independent deployment and scaling
- Follows the established 1-app-per-API pattern (Partner → Partner API, Admin → Admin API, Mobile → Mobile API)

### Project Pattern

Following the exact pattern from the existing 3 API projects:

```
Cleansia.Web.Customer/
├── Abstractions/
│   └── CustomerApiController.cs        # Inherits CleansiaApiController
├── Controllers/
│   ├── AuthController.cs               # Register, Login, GoogleAuth, ConfirmEmail, ForgotPassword
│   ├── OrderController.cs              # CreateOrder, GetPaged, GetById, DownloadReceipt, GetPhotos, ReportIssue
│   ├── ServiceController.cs            # GetOverview (public catalog)
│   ├── PackageController.cs            # GetOverview (public catalog)
│   ├── PaymentController.cs            # Stripe webhook
│   ├── DisputeController.cs            # Create, GetById, GetPaged (customer-only)
│   ├── GdprController.cs              # Export, DeleteAccount, Consents
│   ├── CountryController.cs            # GetOverview (public)
│   ├── LanguageController.cs           # GetOverview (public)
│   └── FeatureFlagController.cs        # Check (authenticated)
├── Extensions/
│   └── ServiceExtensions.cs            # DI setup (HttpContextAccessor, CoreBindings, JWT, Swagger)
├── Properties/
│   └── launchSettings.json             # Port 5003
├── Program.cs                          # Standard host with Sentry
├── Startup.cs                          # Extends CleansiaStartupBase
├── appsettings.json
├── appsettings.Development.json
└── Cleansia.Web.Customer.csproj
```

### Key Configuration

| Setting | Value |
|---------|-------|
| **Port (HTTP)** | 5003 |
| **CORS Policy** | `"CleansiaCustomer"` |
| **Swagger Title** | `"Cleansia.Customer.API v1"` |
| **Aspire Name** | `"customer-api"` |
| **Base Controller** | `CustomerApiController : CleansiaApiController` |

### Startup.cs

```csharp
using Cleansia.Config.Abstractions;
using Cleansia.Web.Customer.Extensions;

namespace Cleansia.Web.Customer;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaCustomer";
    protected override string SwaggerTitle => "Cleansia.Customer.API v1";

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
```

### .csproj References

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>{new-guid}</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Cleansia.Config\Cleansia.Config.csproj" />
    <ProjectReference Include="..\Cleansia.Core.AppServices\Cleansia.Core.AppServices.csproj" />
    <ProjectReference Include="..\Cleansia.Infra.Common\Cleansia.Infra.Common.csproj" />
    <ProjectReference Include="..\Cleansia.ServiceDefaults\Cleansia.ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
```

### Aspire AppHost Registration

```csharp
// In Cleansia.AppHost/Program.cs — add after mobileApi:
var customerApi = builder.AddProject<Projects.Cleansia_Web_Customer>("customer-api")
    .WithEndpoint("http", e => { e.Port = 5003; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WaitFor(cleansiaDb);
```

### Customer-Specific Controllers (10 total)

| Controller | Endpoints | Auth | Notes |
|------------|-----------|------|-------|
| `AuthController` | Register, Login, GoogleAuth, ConfirmEmail, ResendConfirmation, ForgotPassword | `[AllowAnonymous]` | Customer registration only (no `RegisterEmployee`) |
| `OrderController` | CreateOrder, GetPaged, GetById, DownloadReceipt, GetPhotos, ReportIssue | Authenticated | Customer sees only their own orders (filtered by UserId) |
| `ServiceController` | GetOverview | `[AllowAnonymous]` | Public catalog browse |
| `PackageController` | GetOverview | `[AllowAnonymous]` | Public catalog browse |
| `PaymentController` | Webhook | `[AllowAnonymous]` (signature-validated) | Stripe webhook endpoint |
| `DisputeController` | Create, GetById, GetPaged | Authenticated | Customer-only: `CanCreateDispute`, `CanViewDispute` |
| `GdprController` | Export, DeleteAccount, Consents (get/grant/withdraw) | Authenticated | Self-service only (no admin overrides) |
| `CountryController` | GetOverview | `[AllowAnonymous]` | For address forms, country selection |
| `LanguageController` | GetOverview | `[AllowAnonymous]` | For language switcher |
| `FeatureFlagController` | Check | Authenticated | Client-side feature gating |

### What's NOT Exposed to Customers

These endpoints from other APIs are intentionally **excluded** from the Customer API:

- Employee registration (`RegisterEmployee`)
- Take/Start/Complete order (employee actions)
- Upload/Delete order photos (employee actions)
- Dashboard analytics (employee/admin)
- Employee profile management
- Employee documents
- Employee payroll/invoices
- Pay period management
- Admin CRUD operations
- Admin GDPR operations (on behalf of users)
- Device push token management

### CI/CD Updates

The `backend-ci.yml` already builds the full solution (`Cleansia.Api.sln`), so the new project will be automatically included once added to the solution file. No CI changes needed.

---

## Gap Analysis

### Tier 0: Backend — Customer API Project (Prerequisite)

| Task | Status | Priority |
|------|--------|----------|
| Create `Cleansia.Web.Customer` project (csproj, Program.cs, Startup.cs) | **Not built** | CRITICAL |
| Add `CustomerApiController` base class | **Not built** | CRITICAL |
| Add `ServiceExtensions.cs` (JWT, Swagger, CoreBindings) | **Not built** | CRITICAL |
| Add 10 controllers (Auth, Order, Service, Package, Payment, Dispute, GDPR, Country, Language, FeatureFlag) | **Not built** | CRITICAL |
| Register in Aspire AppHost (port 5003) | **Not built** | CRITICAL |
| Add to `Cleansia.Api.sln` solution file | **Not built** | CRITICAL |
| Add `launchSettings.json` (port 5003) | **Not built** | CRITICAL |
| Add `appsettings.json` + `appsettings.Development.json` | **Not built** | CRITICAL |
| Generate NSwag customer client for Angular (`CustomerClient`) | **Not built** | CRITICAL |

> **Note:** All CQRS handlers already exist in `Cleansia.Core.AppServices`. The controllers are thin wrappers calling `Mediator.Send()`. Estimated effort: **4-6 hours** (copy pattern from Mobile API, remove employee-specific endpoints).

### Tier 1: Core Customer Journey (Must Have)

These features are **required** for the app to serve its primary purpose — allowing customers to book cleaning services.

| Feature | Backend (Customer API) | Frontend | Priority |
|---------|----------------------|----------|----------|
| Customer Registration & Login | Handlers ready, controller needed | **Not built** | CRITICAL |
| Email Confirmation Flow | Handlers ready, controller needed | **Not built** | CRITICAL |
| Dynamic Service/Package Catalog (from API) | Handlers ready, controller needed | **Hardcoded static** | CRITICAL |
| Order Creation Wizard (service → address → date → payment) | Handlers ready, controller needed | **Not built** | CRITICAL |
| Stripe Checkout Integration (redirect → success/cancel pages) | Handlers ready, controller needed | **Not built** | CRITICAL |
| Order Tracking Page (status, timeline, assigned employee) | Handlers ready, controller needed | **Not built** | CRITICAL |
| Order History (list of past/active orders) | Handlers ready, controller needed | **Not built** | HIGH |
| Receipt Download | Handlers ready, controller needed | **Not built** | HIGH |

### Tier 2: User Account & Profile (Should Have)

| Feature | Backend | Frontend | Priority |
|---------|---------|----------|----------|
| Password Reset Flow | Ready | **Not built** | HIGH |
| Google OAuth Login | Ready | **Not built** | HIGH |
| User Profile Page (name, phone, email, language, currency) | Ready | **Not built** | MEDIUM |
| Order Photos Gallery (before/after from completed jobs) | Ready | **Not built** | MEDIUM |
| Cookie Consent Banner | Shared component exists | **Not wired** | MEDIUM |
| Language Switcher (cs/en/pl) | Translation infra ready | **Not built** | MEDIUM |

### Tier 3: Support & Trust (Nice to Have)

| Feature | Backend | Frontend | Priority |
|---------|---------|----------|----------|
| Dispute Creation (report problem with order) | Ready | **Not built** | MEDIUM |
| Dispute Tracking (view status/messages) | Ready | **Not built** | MEDIUM |
| GDPR Data Export (download my data) | Ready | **Not built** | LOW |
| GDPR Account Deletion | Ready | **Not built** | LOW |
| Consent Management | Ready | **Not built** | LOW |

### Tier 4: Landing Page Improvements (Polish)

| Feature | Current State | Needed |
|---------|---------------|--------|
| Dynamic services/pricing from API | Hardcoded 6 services | Fetch from `/api/Service/GetOverview` |
| Real testimonials | 1 fake review | CMS integration or API endpoint |
| Real before/after photos | 2 placeholder images | Fetch from completed order photos |
| Complete FAQ | 2 questions | Expand to 8-10 questions |
| Working contact form | Toast only, no API call | Submit to backend or email service |
| Fix broken nav links | Routes don't exist | Add proper routing with sections |
| Fix phone number mismatch | Footer has wrong number | Unify contact info |
| Social media links | Empty hrefs | Add real URLs |
| Multi-language landing page | Czech only | Move all text to i18n keys |
| SEO meta tags | None | Add title, description, Open Graph tags |
| Optimize images | 1.7MB PNGs | Convert to WebP, compress |
| Update copyright year | "© 2025" | "© 2026" or dynamic |

---

## Recommended Architecture

Based on the established patterns from the Partner and Admin apps:

### Backend: Cleansia.Web.Customer (New .NET Project)

```
src/Cleansia.Web.Customer/              # 4th API project (port 5003)
├── Abstractions/
│   └── CustomerApiController.cs        # : CleansiaApiController
├── Controllers/                        # 10 controllers (customer-only endpoints)
│   ├── AuthController.cs
│   ├── OrderController.cs
│   ├── ServiceController.cs
│   ├── PackageController.cs
│   ├── PaymentController.cs
│   ├── DisputeController.cs
│   ├── GdprController.cs
│   ├── CountryController.cs
│   ├── LanguageController.cs
│   └── FeatureFlagController.cs
├── Extensions/
│   └── ServiceExtensions.cs
├── Properties/
│   └── launchSettings.json             # Port 5003
├── Program.cs
├── Startup.cs                          # CleansiaStartupBase("CleansiaCustomer")
├── appsettings.json
└── Cleansia.Web.Customer.csproj
```

### Frontend: cleansia.app (Angular)

```
apps/cleansia.app/
├── src/
│   ├── app/
│   │   ├── app.routes.ts              # All customer routes
│   │   ├── app.config.ts              # Providers (translate, HTTP, store, Sentry)
│   │   └── app.ts                     # Root component with language init
│   ├── assets/
│   │   └── i18n/
│   │       ├── cs.json                # Czech translations
│   │       ├── en.json                # English translations
│   │       └── pl.json                # Polish translations
│   └── environments/
│       ├── environment.ts             # Dev: apiBaseUrl = 'http://localhost:5003/api'
│       └── environment.prod.ts        # Prod: apiBaseUrl from config
```

### Frontend Nx Libraries

```
libs/
├── core/
│   ├── customer-services/              # Customer API client + auth
│   │   ├── client/
│   │   │   └── customer-client.ts      # NSwag-generated from Customer API Swagger
│   │   ├── services/
│   │   │   └── customer-auth.service.ts
│   │   ├── interceptors/
│   │   │   ├── auth.interceptor.ts     # JWT + X-Country-Code headers
│   │   │   └── loading.interceptor.ts
│   │   └── guards/
│   │       ├── auth.guard.ts
│   │       └── guest.guard.ts
│   └── services/                       # Shared (already exists)
│
├── data-access/
│   └── customer-stores/                # NgRx state management
│       ├── auth/                       # Login state, token, user info
│       ├── cart/                       # Service selection, booking wizard state
│       ├── orders/                     # Order list, order detail
│       ├── services/                   # Service/package catalog cache
│       ├── country-config/             # Active country, currency, language
│       ├── feature-flags/              # Cached feature flags
│       └── store.config.ts             # Root state definition
│
├── shared/
│   ├── components/                     # Shared (already exists — 30+ components)
│   └── utils/                          # Shared (already exists)
│
└── cleansia-customer-features/         # Feature modules (lazy-loaded)
    ├── landing/                        # Marketing landing page (refactored)
    │   └── components/
    │       ├── hero/
    │       ├── how-it-works/
    │       ├── benefits/
    │       ├── services-preview/       # Dynamic — fetches from API
    │       ├── before-after-gallery/
    │       ├── testimonials/
    │       ├── faq/
    │       └── contact-form/           # Actually submits data
    ├── auth/
    │   ├── login/
    │   ├── register/
    │   ├── confirm-email/
    │   └── forgot-password/
    ├── booking/                        # Order creation wizard
    │   ├── service-selection/
    │   ├── extras-selection/
    │   ├── address-form/
    │   ├── date-time-picker/
    │   ├── payment-selection/
    │   ├── order-summary/
    │   ├── payment-success/
    │   └── payment-cancel/
    ├── orders/
    │   ├── order-list/
    │   └── order-detail/
    ├── profile/
    ├── disputes/
    │   ├── create-dispute/
    │   └── dispute-detail/
    └── gdpr/
```

### Proposed Routes

```typescript
// Public routes (guestGuard)
{ path: '',           component: LandingComponent }
{ path: 'login',     loadComponent: LoginComponent }
{ path: 'register',  loadComponent: RegisterComponent }
{ path: 'confirm-email', loadComponent: ConfirmEmailComponent }
{ path: 'forgot-password', loadComponent: ForgotPasswordComponent }

// Booking flow (no auth required for browsing, required for checkout)
{ path: 'services',  loadComponent: ServiceCatalogComponent }
{ path: 'book',      loadComponent: BookingWizardComponent }   // authGuard
{ path: 'payment/success', loadComponent: PaymentSuccessComponent }
{ path: 'payment/cancel',  loadComponent: PaymentCancelComponent }

// Authenticated routes (authGuard)
{ path: 'orders',        loadComponent: OrderListComponent }
{ path: 'orders/:orderId', loadComponent: OrderDetailComponent }
{ path: 'profile',       loadComponent: ProfileComponent }
{ path: 'disputes',      loadComponent: DisputeListComponent }
{ path: 'disputes/new',  loadComponent: CreateDisputeComponent }
{ path: 'disputes/:id',  loadComponent: DisputeDetailComponent }
{ path: 'privacy',       loadComponent: GdprComponent }

// Fallback
{ path: '**',         loadComponent: NotFoundComponent }
```

---

## Extensibility & Multi-Country Design

This section defines the architecture patterns that make the customer app easy to expand with new features, languages, and countries.

### Design Principle: Everything is Config-Driven

The app should **never hardcode** country-specific values (currency symbols, VAT rates, date formats, phone prefixes, payment methods). Instead, all country/tenant-specific behavior flows from 3 layers:

```
┌─────────────────────────────────────┐
│  Tenant Config  (most specific)     │  TenantConfiguration (key-value)
├─────────────────────────────────────┤
│  Country Config  (mid-level)        │  CountryConfiguration entity
├─────────────────────────────────────┤
│  Global Config   (fallback)         │  FeatureFlag (scope="global")
└─────────────────────────────────────┘
```

Resolution: `IAppConfigurationProvider` checks tenant first → country → global. This pattern already exists in the backend and should drive all frontend behavior.

### 1. Adding a New Country

**Backend (zero code changes):**

All done via Admin API / seed data:

| Step | What | Where |
|------|------|-------|
| 1 | Add `Country` record | `Countries` table (name, ISO code, flag) |
| 2 | Add `CountryConfiguration` | Currency, language, VAT, date format, phone prefix, timezone, payment gateway |
| 3 | Add country-specific `FeatureFlags` | e.g., `CashPayment` enabled for CZ but disabled for DE |
| 4 | Add `CountryInvoiceConfig` | VAT rules, digital signature requirements, legal disclaimer |
| 5 | Seed services/packages with translations | `Service.SetTranslation(langCode, name, description)` |

**Frontend (add translation file only):**

| Step | What |
|------|------|
| 1 | Add `assets/i18n/{lang}.json` translation file |
| 2 | Add lang code to `translate.addLangs([...])` in `app.ts` |
| 3 | Register locale data: `registerLocaleData(localeXx)` in `app.config.ts` |

No component changes needed — everything adapts automatically because:
- Currency symbol comes from `CountryConfiguration.DefaultCurrencyCode`
- Date format comes from `CountryConfiguration.DateFormat`
- Phone prefix comes from `CountryConfiguration.PhonePrefix`
- VAT display comes from `CountryConfiguration.StandardVatRate`
- Payment methods come from feature flags scoped to the country
- Service names/descriptions come from translated service records

### 2. Adding a New Language

**Backend:** No changes — translations are stored in the database per entity.

**Frontend:**

| Step | File | Change |
|------|------|--------|
| 1 | `assets/i18n/{lang}.json` | Create new translation file (copy from `en.json`, translate all keys) |
| 2 | `app.ts` | Add to `translate.addLangs([..., '{lang}'])` |
| 3 | `app.config.ts` | Add `registerLocaleData(locale{Lang})` for Angular pipes (dates, numbers) |
| 4 | `CleansiaLanguageSwitcher` | Already supports dynamic lang list — no changes needed |

**Translation key structure** (follow the Partner app convention):

```json
{
  "customer": {
    "landing": {
      "hero_title": "...",
      "hero_subtitle": "..."
    },
    "booking": {
      "step_services": "Select Services",
      "step_address": "Your Address",
      "step_date": "Choose Date",
      "step_payment": "Payment"
    },
    "orders": {
      "title": "My Orders",
      "empty_state": "You haven't placed any orders yet."
    }
  },
  "global": {
    "messages": {
      "success": "Success",
      "error": "Error"
    },
    "validation": {
      "required": "This field is required",
      "email": "Invalid email address"
    }
  }
}
```

### 3. Adding a New Feature (Feature Flag Pattern)

**Backend:**

1. Add `FeatureFlag` record via Admin API:
   ```json
   { "name": "CustomerReviews", "scope": "global", "isEnabled": false }
   ```
2. Optionally enable per-country:
   ```json
   { "name": "CustomerReviews", "scope": "country", "scopeValue": "CZ", "isEnabled": true }
   ```

**Frontend — NgRx feature flag store:**

```typescript
// customer-stores/feature-flags/feature-flags.effects.ts
loadFlags$ = createEffect(() =>
  this.actions$.pipe(
    ofType(FeatureFlagActions.loadFlags),
    switchMap(() =>
      forkJoin(
        FEATURE_FLAG_NAMES.map(name =>
          this.customerClient.featureFlagClient.check(name, this.countryId)
        )
      ).pipe(
        map(results => FeatureFlagActions.loadFlagsSuccess({ flags: results })),
        catchError(error => of(FeatureFlagActions.loadFlagsFailure({ error })))
      )
    )
  )
);
```

```typescript
// In any component — hide/show features conditionally:
@Component({ template: `
  @if (reviewsEnabled$ | async) {
    <app-customer-reviews [orderId]="orderId" />
  }
` })
export class OrderDetailComponent {
  reviewsEnabled$ = this.store.select(selectFeatureFlag('CustomerReviews'));
}
```

**Recommended feature flag names for the customer app:**

| Flag | Default | Description |
|------|---------|-------------|
| `CustomerApp.Booking` | `true` | Enable/disable entire booking flow |
| `CustomerApp.CashPayment` | `true` | Allow cash payment option |
| `CustomerApp.GoogleOAuth` | `true` | Google login button |
| `CustomerApp.Reviews` | `false` | Customer review system |
| `CustomerApp.Disputes` | `true` | Dispute creation |
| `CustomerApp.GdprSelfService` | `true` | GDPR data export/deletion |
| `CustomerApp.Subscriptions` | `false` | Recurring cleaning subscriptions |
| `CustomerApp.LoyaltyProgram` | `false` | Points/rewards system |
| `CustomerApp.LiveChat` | `false` | In-app customer support chat |

### 4. Adding a New Service Type

**Backend (Admin API only — no code changes):**

1. Create service via Admin API with multi-language translations
2. Set pricing (base price + per-room rate)
3. Set estimated duration
4. Optionally gate behind a country-specific feature flag

**Frontend adapts automatically** because:
- Service catalog fetches from `/api/Service/GetOverview` — any new service appears
- Service cards render dynamically from the API response
- Booking wizard uses the service list from the store, not hardcoded options
- Price calculation happens server-side in `CreateOrder` handler

### 5. Country-Aware Auth Interceptor

The frontend HTTP interceptor should inject country context into every API request:

```typescript
export const CustomerAuthInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(CustomerAuthService);
  const store = inject(Store);

  const token = authService.getToken();
  const countryCode = store.selectSignal(selectActiveCountryCode)();

  let headers = req.headers;

  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }
  if (countryCode) {
    headers = headers.set('X-Country-Code', countryCode);
  }

  return next(req.clone({ headers }));
};
```

This lets the backend resolve country-specific configuration (VAT, currency, payment gateway) from the request without the frontend having to pass these values explicitly.

### 6. NSwag Client Generation

Follow the exact pattern from partner/admin apps:

**`nswag-customer.json`:**
```json
{
  "runtime": "Net80",
  "documentGenerator": {
    "fromDocument": {
      "url": "http://localhost:5003/swagger/v1/swagger.json"
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      "className": "{controller}Client",
      "template": "Angular",
      "httpClass": "HttpClient",
      "injectionTokenType": "InjectionToken",
      "baseUrlTokenName": "CUSTOMERAPIBASEURL",
      "output": "libs/core/customer-services/src/lib/client/customer-client.ts"
    }
  }
}
```

**`package.json` script:**
```json
"generate-customer-client": "npx nswag run nswag-customer.json"
```

Run after any Customer API controller changes. The generated `CustomerClient` provides typed methods for every endpoint.

### 7. Environment Configuration

```typescript
// environment.ts (development)
export const environment = {
  apiBaseUrl: 'http://localhost:5003',
  isDevelopment: true,
  blobStorageUrl: 'http://127.0.0.1:10000/devstoreaccount1',
  googleClientId: '354682423254-...',
  sentryDsn: '',
  featureFlagsRefreshMs: 300_000,  // 5 minutes
};

// environment.prod.ts (production)
export const environment = {
  apiBaseUrl: 'https://api.cleansia.com/customer',
  isDevelopment: false,
  blobStorageUrl: 'https://cleansiablobs.blob.core.windows.net',
  googleClientId: '',
  sentryDsn: '',
  featureFlagsRefreshMs: 300_000,
};
```

---

## Reusable Shared Components

These already exist in `@cleansia/components` and `@cleansia/services` and can be used immediately:

### UI Components (`@cleansia/components`)

| Component | Description | Useful For |
|-----------|-------------|------------|
| `CleansiaButton` | Standardized button with severity, icons, loading state | All CTAs |
| `CleansiaTextInput` | Text input with label, validation, error messages | All forms |
| `CleansiaTextarea` | Textarea with label and validation | Dispute description |
| `CleansiaSelect` | Dropdown select with label | Country, currency selection |
| `CleansiaCalendar` | Date picker with label | Cleaning date selection |
| `CleansiaTimePicker` | Time picker | Cleaning time selection |
| `CleansiaTelephone` | Phone input with country code dropdown | Registration, booking |
| `CleansiaCheckbox` | Checkbox with label | Consent forms, extras |
| `CleansiaCard` | Card container | Service cards, order cards |
| `CleansiaSection` | Section container with shadow-box styling | Page sections |
| `CleansiaTable` | Data table with sorting, pagination | Order history |
| `CleansiaTopNavbar` | Top navigation bar | Customer app header |
| `CleansiaLanguageSwitcher` | Language dropdown (cs/en/pl) | Header/footer |
| `CleansiaCookieConsent` | Cookie consent banner | Landing page |
| `CleansiaLoader` | Loading spinner | Page transitions |
| `CleansiaSkeleton` (4 variants) | Skeleton loading placeholders | All pages |
| `CleansiaNotFound` | 404 page component | Fallback route |

### Services (`@cleansia/services`)

| Service | Description | Useful For |
|---------|-------------|------------|
| `SnackbarService` | Toast notifications with i18n | Success/error messages |
| `DialogService` | Confirmation dialogs with i18n | Delete account, cancel order |
| `PageTitleService` | Dynamic browser tab title | All pages |
| `JsonTranslationLoader` | Load i18n JSON files | Already configured |
| `FileValidationErrorService` | File upload validation | Dispute evidence upload |

### Guards & Interceptors

| Guard/Interceptor | Description |
|-------------------|-------------|
| `authGuard` | Redirect to login if not authenticated |
| `guestGuard` | Redirect to orders if already authenticated |
| `COMMON_INTERCEPTORS_FN` | Shared HTTP interceptors |

---

## Summary

| Aspect | Status |
|--------|--------|
| **CQRS handlers** | ~95% ready — All auth, order, payment, dispute, GDPR handlers exist in `Core.AppServices` |
| **Customer API project** | **Not built** — New `Cleansia.Web.Customer` project needed (10 controllers, port 5003) |
| **Frontend readiness** | ~5% — Static landing page only, no interactivity |
| **Shared components** | ~30+ reusable components available from partner/admin apps |
| **Translation infra** | Ready — just needs cs/en/pl JSON files for customer keys |
| **Biggest gaps** | 1) Customer API project, 2) Order creation wizard, 3) Customer auth flow |
| **Estimated new work** | Backend: ~4-6 hours (controllers only) / Frontend: 15-20 feature components |

### Implementation Order

```
Phase 0: Backend — Cleansia.Web.Customer API project (10 controllers, Aspire, solution)
    ↓
Phase 1: Frontend infra — NSwag client, environment, customer-services lib, customer-stores,
         auth guards, interceptors, language detection
    ↓
Phase 2: Auth flow — Register, Login, Google OAuth, Confirm Email, Forgot Password
    ↓
Phase 3: Booking — Dynamic service catalog, order wizard (5 steps), Stripe checkout,
         success/cancel pages
    ↓
Phase 4: Orders — Order history (paginated, filtered), order detail (status timeline,
         photos, receipt download)
    ↓
Phase 5: Account — Profile page, dispute creation + tracking, GDPR (export, delete, consents)
    ↓
Phase 6: Polish — Landing page dynamic content, SEO meta tags, i18n for all static text,
         image optimization, cookie consent
```

### Extensibility Checklist

| Expansion | Changes Required |
|-----------|-----------------|
| **New country** | Add `CountryConfiguration` record + translation JSON file + locale registration |
| **New language** | Add `assets/i18n/{lang}.json` + add to `addLangs()` + register locale |
| **New service type** | Admin API only — no code changes, catalog auto-updates |
| **New feature** | Add `FeatureFlag` record, wrap UI in `@if (flag$ \| async)`, gate per country/tenant |
| **New payment method** | Add to `CountryConfiguration.DefaultPaymentGateway`, add frontend component behind feature flag |
| **New tenant** | Zero code changes — `TenantId` filter + `TenantConfiguration` overrides handle everything |

All CQRS handlers are shared across projects — the Customer API controllers are thin wrappers calling `Mediator.Send()`. The frontend follows the same Nx monorepo patterns as the partner and admin apps. The 3-level configuration system (tenant → country → global) ensures country expansion requires **configuration changes only, not code changes**.

---

**Document Version**: 1.2.0
**Created**: 2026-03-02
