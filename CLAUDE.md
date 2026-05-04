# Cleansia — Project Guide for Claude Code

> Cleaning services management platform — Customer booking, Partner job management, Admin oversight.

## Quick Reference

| Layer | Tech | Location |
|---|---|---|
| Backend | .NET 10, PostgreSQL 16, EF Core 10, MediatR | `src/Cleansia.Core.*`, `src/Cleansia.Infra.*`, `src/Cleansia.Web.*` |
| Frontend | Angular 19, Nx 21, NgRx, PrimeNG, ngx-translate | `src/Cleansia.App/` |
| Mobile | Kotlin, Jetpack Compose, MVVM + Hilt | `src/cleansia_android/` |
| Orchestration | .NET Aspire 13.1.1 | `src/Cleansia.AppHost/` |
| Docs | VitePress | `docs/` |

## Repository Structure

```
cleansia/
├── src/
│   ├── Cleansia.App/                    # Angular Nx monorepo (frontend)
│   │   ├── apps/
│   │   │   ├── cleansia.app/            # Customer app (SSR)
│   │   │   ├── cleansia-partner.app/    # Partner app (SPA)
│   │   │   └── cleansia-admin.app/      # Admin app (SPA)
│   │   └── libs/
│   │       ├── cleansia-customer-features/  # Customer feature modules
│   │       ├── cleansia-partner-features/   # Partner feature modules
│   │       ├── cleansia-admin-features/     # Admin feature modules
│   │       ├── core/services/               # NSwag-generated API clients
│   │       ├── data-access/                 # NgRx stores (admin/customer/partner)
│   │       └── shared/                      # Components, pipes, directives, utils
│   │
│   ├── Cleansia.Core.Domain/           # Domain entities, enums, value objects
│   ├── Cleansia.Core.AppServices/      # CQRS handlers, DTOs, validators (MediatR)
│   ├── Cleansia.Infra.Database/        # EF Core DbContext, migrations, entity configs
│   ├── Cleansia.Infra.Services/        # PDF (QuestPDF), email, blob services
│   ├── Cleansia.Infra.Clients/         # SendGrid, Stripe HTTP clients
│   ├── Cleansia.Config/                # Shared startup base, DI registration
│   ├── Cleansia.Web/                   # Partner API (port 5000)
│   ├── Cleansia.Web.Admin/             # Admin API (port 5001)
│   ├── Cleansia.Web.Mobile/            # Mobile API (port 5002)
│   ├── Cleansia.Web.Customer/          # Customer API (port 5003)
│   ├── Cleansia.Functions/             # Azure Functions (receipt, invoice, cleanup)
│   ├── Cleansia.Tests/                 # Unit tests (xUnit)
│   └── cleansia_android/               # Native Android app
│
├── docs/                                # VitePress documentation site
├── agents/                              # AI agent configs and plans
├── deploy/                              # Deployment configs
├── scripts/                             # Utility scripts
├── sql-scripts/                         # Database seed/migration scripts
└── Cleansia.Api.sln                     # .NET solution file
```

## Build & Run Commands

### Backend
```bash
# Build entire solution
dotnet build Cleansia.Api.sln

# Run with Aspire orchestration (starts all 4 APIs + PostgreSQL)
dotnet run --project src/Cleansia.AppHost

# Run individual API
dotnet run --project src/Cleansia.Web              # Partner API :5000
dotnet run --project src/Cleansia.Web.Admin         # Admin API :5001
dotnet run --project src/Cleansia.Web.Mobile        # Mobile API :5002
dotnet run --project src/Cleansia.Web.Customer      # Customer API :5003

# Run tests
dotnet test src/Cleansia.Tests
dotnet test src/Cleansia.IntegrationTests
```

### Frontend (from `src/Cleansia.App/`)
```bash
# Dev servers
npx nx serve cleansia-partner-app       # Partner :4200
npx nx serve cleansia-admin-app         # Admin :4201
npx nx serve cleansia-app               # Customer :4202

# Production builds
npx nx build cleansia-partner-app --configuration=production
npx nx build cleansia-admin-app --configuration=production
npx nx build cleansia-app --configuration=production

# Regenerate NSwag API clients (after backend changes)
npm run generate-partner-client
npm run generate-admin-client
npm run generate-customer-client

# Lint & test
npx nx lint <project>
npx nx test <project>
```

## Architecture Patterns

### Backend — CQRS with MediatR

Every backend operation is either a **Command** (write) or **Query** (read):

```
Feature/
├── CreateSomething.cs        # Command + Handler + Validator + Response
├── UpdateSomething.cs        # Command + Handler + Validator + Response
├── GetPagedSomethings.cs     # Query + Handler + Filter + Sort + Spec
├── DTOs/
│   └── SomethingDto.cs       # Record type DTO
└── Mappers/
    └── SomethingMapper.cs    # Extension methods: .MapToDto()
```

**Key rules:**
- Handlers contain happy-path logic ONLY — no validation, no error checking
- All validation goes in `Validator` classes (FluentValidation with `Cascade.Stop`)
- Never call `CommitAsync()` in handlers — UnitOfWork pipeline handles it
- Queries never modify data; Commands never return collections
- All DTOs are `record` types with positional syntax
- Return `BusinessResult<T>` from commands, `PagedData<T>` from paged queries
- Error messages defined in `BusinessErrorMessage` constants with dot notation

### Frontend — Facades + Signals + NgRx

```
Feature/
├── feature.component.ts       # UI logic only, delegates to facade
├── feature.component.html     # Template (uses cleansia-* components)
├── feature.facade.ts          # Business logic, API calls, signal state
└── feature.models.ts          # Table definitions, action configs
```

**Key rules:**
- Components delegate ALL business logic to facades
- Facades manage state via Angular signals
- NgRx stores for cross-feature state (auth, user, services/packages lists)
- Always use `<cleansia-button>`, `<cleansia-section>`, `<cleansia-table>`, etc.
- Never use raw HTML `<select>`, `<button>`, `<input>` — use PrimeNG or shared wrappers
- Translations via `TranslatePipe` (standalone) — never hardcode user-visible strings
- SCSS files go in shared assets, not inline
- `ChangeDetectionStrategy.OnPush` on presentational components
- All facades extend `UnsubscribeControlDirective` for RxJS cleanup

### NSwag Client Generation

API clients are auto-generated from backend OpenAPI specs. After any backend DTO/endpoint change:
1. Run the backend
2. Run `npm run generate-{partner|admin|customer}-client`
3. The generated client files are at `libs/core/{partner|admin|customer}-services/src/lib/client/`

### Manual Steps (owner does these, NOT Claude)

- **EF Core migrations** — The owner creates and applies migrations manually. Claude should NOT run `dotnet ef migrations add` or `dotnet ef database update`. When a migration is needed, add a `MANUAL_STEP` entry to the task spec.
- **NSwag client regeneration** — The owner regenerates TypeScript API clients manually. Claude should NOT run `npm run generate-*-client`. When backend DTOs or endpoints change, flag it as a `MANUAL_STEP` so the owner knows to regenerate before frontend work begins.

## Multi-Tenancy

- Shared PostgreSQL database with `TenantId` column on tenant-scoped entities
- EF Core global query filters auto-scope reads
- JWT tokens include `tenant_id` claim
- Backward compatible: `null` TenantId = single-tenant mode

## i18n — 5 Languages

All 3 frontend apps support: **English (en)**, **Czech (cs)**, **Slovak (sk)**, **Ukrainian (uk)**, **Russian (ru)**

Translation files: `apps/<app>/src/assets/i18n/{en,cs,sk,uk,ru}.json`

Every backend error key in `BusinessErrorMessage` must have a corresponding frontend translation under `errors.*`.

## Order Lifecycle

```
New (0) → Pending (1) → Confirmed (2) → InProgress (3) → Completed (4)
              ↓
          Cancelled (5)
```

- `New`: Order just created
- `Pending`: Card payment initiated (waiting for Stripe webhook)
- `Confirmed`: Cleaner took the order (or cash payment auto-confirmed)
- `InProgress`: Cleaner started work
- `Completed`: Cleaner finished

## Pay Calculation

```
basePay = (services × serviceRate) + (packages × packageRate)
extrasPay = sum(extras × extraRate)
expensesPay = distance × distanceRate
totalPay = clamp(basePay + extrasPay + expensesPay, minPay, maxPay)
finalPay = totalPay + bonus - deduction
```

Pay configs are per service/package (with per-employee overrides in progress — IMP-3).

## Key Entities

| Entity | Description |
|---|---|
| `Employee` | Partner/cleaner — extends User with profile, availability, documents |
| `Order` | Aggregate root — services, packages, photos, notes, issues, status history |
| `EmployeePayConfig` | Pay rates per service/package (optional per-employee override via EmployeeId) |
| `EmployeeInvoice` | Generated per pay period per employee |
| `PayPeriod` | Bi-weekly pay cycle (auto or manual creation) |
| `Service` / `Package` | Cleaning service types and bundles with pricing |
| `Currency` / `Language` / `Country` | Platform configuration entities |

## Active Bug/Improvement Tracker

See `planning/active/bugfix-improvements.md` for the full list.

**Completed (v3 + v4):** BUG-1 through BUG-21, IMP-2, IMP-4, CONTENT-1 through CONTENT-4

**In Progress:**
- **IMP-3**: Per-employee pay config — `EmployeeId` field added to `EmployeePayConfig` entity and EF config. Remaining: migration, backend commands, admin UI tab on employee detail.

**Remaining (needs external setup):**
- **IMP-1**: Google OAuth — needs Google Cloud Console project
- **BUG-22**: Email badge colors — email template CSS

## Conventions Summary

- **File naming**: PascalCase for C# files, kebab-case for Angular files
- **Branches**: `feature/*`, `fix/*`, `bugfix/*` from `master`
- **Commits**: Conventional-style — `feat:`, `fix:`, `refactor:`, `docs:`
- **PRs**: Target `master` branch
- **Backend errors**: `category.specific_error` pattern in `BusinessErrorMessage`
- **Frontend errors**: `errors.category.specific_error` in i18n files
- **API clients**: Never hand-edit — always regenerate via NSwag
- **Tests**: xUnit for backend, Jest for frontend
- **No inline templates/styles** in Angular components
- **No `any` type** in TypeScript — use proper types and enums
- `Address.State` is nullable — used for US/CA when we launch there; empty for CZ/SK/UA/RU/DE/PL. Do not remove.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `python3 -c "from graphify.watch import _rebuild_code; from pathlib import Path; _rebuild_code(Path('.'))"` to keep the graph current
