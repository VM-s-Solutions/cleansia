# Cleansia Architecture Improvements Roadmap

> Generated: 2026-02-07
> Status: Planned - For Future Implementation

---

## Phase 1: Foundation

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Move secrets to Azure Key Vault / User Secrets | CRITICAL | 2 days | Pending |
| Add .NET Aspire for orchestration | HIGH | 3 days | Pending |
| Create `Cleansia.Web.Mobile` project | HIGH | 3 days | **In Progress** |
| Add Device entity for push notification tokens | MEDIUM | 1 day | Pending |

---

## Phase 2: Multi-Tenancy Preparation

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Add `TenantId` to all entities | HIGH | 1 week | Pending |
| Implement EF Core query filters | HIGH | 3 days | Pending |
| Create tenant resolution middleware | HIGH | 2 days | Pending |
| Database migration for multi-tenancy | HIGH | 2 days | Pending |

### Multi-Tenancy Approach

Recommended: **Shared database with TenantId filter** (simpler, sufficient for current scale)

- Add `TenantId` column to all main entities
- Implement EF Core global query filters
- Add middleware to resolve tenant from JWT/subdomain
- Row-level security in PostgreSQL as additional safety

---

## Phase 3: Configuration & Country Expansion

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Create `TenantConfiguration` entity | HIGH | 2 days | Pending |
| Create `CountryConfiguration` entity | HIGH | 2 days | Pending |
| Implement configuration provider | MEDIUM | 3 days | Pending |
| Add feature flags system | MEDIUM | 3 days | Pending |
| VAT/Tax per country | MEDIUM | 1 week | Pending |

### Configuration Layers

```
Level 1: Global (all countries)
  - JWT settings, API keys, feature flags

Level 2: Country-specific
  - VAT rates, tax rules, payment gateways
  - Legal requirements, invoice templates

Level 3: Company-specific (tenant)
  - Branding, custom settings, feature toggles
```

---

## Phase 4: Android Refactoring

| File | Lines | Refactoring Strategy | Status |
|------|-------|---------------------|--------|
| `OrderDetailsScreen.kt` | 1803 | Split into 5-6 composables | Pending |
| `AvailabilityCalendar.kt` | 1410 | Extract logic to ViewModel/UseCase | Pending |
| `AnalyticsDetailScreen.kt` | 1189 | Split charts into separate components | Pending |
| `ProfileScreen.kt` | 1124 | Split into sections (Personal, Bank, Emergency) | Pending |
| `DashboardScreen.kt` | 1094 | Extract cards and charts | Pending |
| `AvailabilitySection.kt` | 1042 | Extract to smaller sections | Pending |

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

## Phase 5: .NET 10 Migration

| Task | Priority | Effort | Status |
|------|----------|--------|--------|
| Update SDK in `global.json` | MEDIUM | 1 hour | Pending |
| Update all `.csproj` to `net10.0` | MEDIUM | 1 hour | Pending |
| Update NuGet packages to .NET 10 versions | MEDIUM | 2 hours | Pending |
| Test all functionality | MEDIUM | 2 days | Pending |
| Update Docker base images | LOW | 1 hour | Pending |

### Benefits

- 10-20% faster JSON serialization
- Better GC performance
- Native AOT improvements (faster cold start)
- EF Core 10 query improvements

---

## Phase 6: .NET Aspire Adoption

### Target Architecture

```
Cleansia.AppHost/           # Aspire orchestrator
├── Program.cs              # Defines all services
Cleansia.ServiceDefaults/   # Shared defaults (telemetry, health checks)
Cleansia.Web.Partner/       # Partner API
Cleansia.Web.Admin/         # Admin API
Cleansia.Web.Mobile/        # Mobile API
Cleansia.Worker/            # Background jobs (extract from Hangfire)
```

### Benefits

- Centralized service orchestration
- Built-in OpenTelemetry (logging, metrics, traces)
- Service discovery (no hardcoded URLs)
- Easy local development of multi-service setup
- Cloud-ready deployment to Azure Container Apps

---

## Security Issues to Address

| Issue | Priority | Status |
|-------|----------|--------|
| Secrets exposed in `appsettings.json` (SendGrid, Stripe, JWT) | CRITICAL | Pending |
| CORS allows `AllowAnyOrigin` | HIGH | Pending |
| No rate limiting on auth endpoints | HIGH | Pending |
| No API versioning | MEDIUM | Pending |
| Old `Microsoft.AspNetCore.Http 2.3.0` in Infra.Database | MEDIUM | Pending |

---

## Decision Points (To Be Decided)

1. **Aspire adoption** - Start with Mobile API or wait?
2. **Multi-tenancy** - Shared DB with TenantId (recommended) vs DB per tenant
3. **Configuration storage** - Azure App Config vs Database tables vs Hybrid
4. **Android refactoring** - Dedicated sprint vs refactor-as-you-go
5. **Monolith** - Stay monolith (recommended for now), prepare extraction boundaries

---

## Current Architecture (As-Is)

```
Solution: Cleansia.Api.sln (16 projects)

05 Web (Presentation)
├── Cleansia.Web.Partner    # Partner API (17 controllers)
└── Cleansia.Web.Admin      # Admin API (18 controllers)

04 Core (Business Logic)
├── Cleansia.Core.AppServices  # CQRS handlers, features, DTOs
├── Cleansia.Core.Domain       # Entities, domain logic
├── Cleansia.Core.Blobs.Abstractions
└── Cleansia.Core.Clients.Abstractions

03 Infrastructure
├── Cleansia.Infra.Database    # EF Core, PostgreSQL
├── Cleansia.Infra.Services    # PDF, Email, Blob
├── Cleansia.Infra.Common      # Shared utilities
├── Cleansia.Infra.Clients     # SendGrid, Stripe
└── Cleansia.Infra.Azure.Storage.Blobs

02 Configuration
└── Cleansia.Config            # DI registration, all config

99 Test
├── Cleansia.Tests
├── Cleansia.IntegrationTests
└── Cleansia.TestUtilities
```
