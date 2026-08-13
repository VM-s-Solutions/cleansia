# Cleansia — Project Guide for Claude Code

> Cleaning services management platform — Customer booking, Partner job management, Admin oversight.

## Working agreement — read this first, every session

**Every reply starts with `Hey Mike —` and one short line naming the active scope.** If that line is
missing, or names work you did not agree to, **stop me**. It is a canary, not a courtesy: drift is
silent, it gets likelier the longer a session runs, and it is only ever visible in hindsight
otherwise.

**Every** means every — including one-line answers, mid-investigation updates, and messages that are
mostly tool output. **There is no "this one is just a quick note" exemption**, and asking for one is
itself the signal. Measured within an hour of this rule being written: it was dropped exactly once,
on a message reporting a mid-investigation finding — i.e. on the message where the scope was actually
moving. That is not a coincidence, it is the mechanism: the anchor gets skipped precisely when
attention is on the problem rather than on the agreement, which is when drift starts.

### 1. Scope is agreed before work starts, and it has a NOT list

Before anything non-trivial I state three things and wait:

- **Doing** — what I will change.
- **NOT doing** — what I will deliberately leave alone, *including things I can see are imperfect*.
- **Done looks like** — the observable state that ends the task.

**The NOT list is the one that matters.** Without it, every defect I notice becomes in scope — which
is exactly how *"is it finished?"* kept producing more tickets instead of an answer.

### 2. A finding is reported, never absorbed

Anything found outside the agreed scope goes on a list and is **reported at the end**. I do not fix
it in the same pass, however small, and I do not spawn a lane for it. You decide whether it becomes
work. *"I found X and fixed it while I was there"* is the failure mode, not the service.

### 3. Ground-truth a ticket before doing it

**Does the defect still exist in the tree, right now?** A ticket is a claim about the past. On
2026-08-11 four lanes were dispatched onto 24 tickets that were all already shipped, because the
rows said open. One grep is cheaper than one lane.

### 4. Proportionality, before any new type, file or abstraction

Ask: **what happens if I don't build this, and how likely is that?** If the answer is *"an unlikely
error path fails ugly"* — **don't build it.** A new type is paid by every future reader, forever; a
rare 500 is paid once by support.

The worked example of getting this wrong is `Register.cs`'s `DbConstraintViolation.IsUniqueViolationOn`
+ `DbConstraintNames`: an app-level pre-check already covered the normal case, the race it defends
needs two registrations in the same millisecond, and it bought **two new types and five copies of a
catch block**. The index fix was one line and was right; the mapping around it was not.

Prefer, in order: **do nothing** → **inline at the call site** → **extract only when a second caller
exists today**.

### 5. Review the diff against the scope before committing

Re-read the diff and check each hunk against **Doing**. Anything not on that list comes out, or is
named in the commit message as a deliberate addition. Say what is being pushed, and why, before
pushing.

### What this is not

It is not a licence to stop early. Finish what was agreed, completely, and say so plainly. The
failure this replaces is not *too much work* — it is **work nobody chose**.

## Quick Reference

| Layer | Tech | Location |
|---|---|---|
| Backend | .NET 10, PostgreSQL 16, EF Core 10, MediatR | `src/Cleansia.Core.*`, `src/Cleansia.Infra.*`, `src/Cleansia.Web.*` |
| Frontend | Angular 19, Nx 21, NgRx, PrimeNG, ngx-translate | `src/Cleansia.App/` |
| Android | Kotlin, Jetpack Compose, MVVM + Hilt | `src/cleansia_android/` (multi-module: `:core`, `:partner-app`, `:customer-app`) |
| iOS | Swift/SwiftUI, iOS 16 floor, XcodeGen + SPM | `src/cleansia_ios/` (`CleansiaCore` package + `CleansiaPartner` / `CleansiaCustomer` apps) |
| Orchestration | .NET Aspire 13.1.1 | `src/Cleansia.AppHost/` |
| Docs | VitePress | `docs/` |

> The .NET solution lives at **`src/Cleansia.Api.sln`**, not at the repo root. Every `dotnet` command
> below runs from `src/` — that is what CI does (`.github/workflows/backend-ci.yml:60-65`,
> `working-directory: ./src`).

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
│   │       ├── core/{partner,admin,customer}-services/  # NSwag-generated API clients
│   │       ├── core/services/               # Shared HTTP interceptors, snackbar, guards (hand-written)
│   │       ├── data-access/                 # NgRx stores (admin/customer/partner)
│   │       └── shared/                      # Components, pipes, directives, utils
│   │
│   ├── Cleansia.Core.Domain/           # Domain entities, enums, value objects, specifications
│   ├── Cleansia.Core.AppServices/      # CQRS handlers, DTOs, validators (MediatR)
│   ├── Cleansia.Infra.Database/        # EF Core DbContext, migrations, entity configs
│   ├── Cleansia.Infra.Services/        # PDF (QuestPDF), email, blob services
│   ├── Cleansia.Infra.Clients/         # SendGrid, Stripe HTTP clients
│   ├── Cleansia.Config/                # Shared startup base, DI registration
│   ├── Cleansia.Web.Partner/           # Partner API (port 5000)
│   ├── Cleansia.Web.Admin/             # Admin API (port 5001)
│   ├── Cleansia.Web.Mobile.Partner/    # Partner Mobile API (port 5002)
│   ├── Cleansia.Web.Customer/          # Customer API (port 5003)
│   ├── Cleansia.Web.Mobile.Customer/   # Customer Mobile API (port 5004)
│   ├── Cleansia.Functions/             # Azure Functions host — thin trigger shells only
│   ├── Cleansia.Functions.Core/        # Function bodies (ADR-0002 D5) + DI registration
│   ├── Cleansia.MigrationService/      # Aspire-launched EF migrator (runs before every API)
│   ├── Cleansia.Tests/                 # Unit tests (xUnit)
│   ├── Cleansia.IntegrationTests/      # Testcontainers Postgres — tenancy, FK, webhook, migration
│   ├── Cleansia.HostTests/             # Authz / isolation against a real host + Postgres
│   ├── cleansia_android/               # Native Android multi-module
│   │   ├── core/                       # Shared :core library — theme, components, auth/network, snackbar
│   │   ├── partner-app/                # Partner Android app (cz.cleansia.partner)
│   │   └── customer-app/               # Customer Android app (cz.cleansia.customer)
│   ├── cleansia_ios/                   # Native iOS — see src/cleansia_ios/README.md for the full layout
│   │   ├── CleansiaCore/               # Shared SPM package (the Android :core equivalent)
│   │   ├── CleansiaPartner/            # Partner iOS app (cz.cleansia.partner)
│   │   └── CleansiaCustomer/           # Customer iOS app (cz.cleansia.customer)
│   └── Cleansia.Api.sln                # .NET solution file — under src/, NOT the repo root
│
├── docs/                                # VitePress documentation site
├── agents/                              # AI agent configs and plans
├── deploy/                              # Deployment configs
├── scripts/                             # Utility scripts
└── sql-scripts/                         # Database seed/migration scripts
```

## Build & Run Commands

### Backend

All of these run **from `src/`** — the solution is `src/Cleansia.Api.sln`.

```bash
cd src

# Build entire solution
dotnet build Cleansia.Api.sln

# Run with Aspire orchestration: PostgreSQL, the migrator, all 5 APIs and the Functions host.
# Every API WaitForCompletion(migrations) — a failed migration keeps all of them stopped.
dotnet run --project Cleansia.AppHost

# Run individual API (ports are pinned in Cleansia.AppHost/Program.cs:80-114 and each launchSettings)
dotnet run --project Cleansia.Web.Partner          # Partner API :5000
dotnet run --project Cleansia.Web.Admin            # Admin API :5001
dotnet run --project Cleansia.Web.Mobile.Partner   # Partner Mobile API :5002
dotnet run --project Cleansia.Web.Customer         # Customer API :5003
dotnet run --project Cleansia.Web.Mobile.Customer  # Customer Mobile API :5004

# Run tests — CI runs all three, single-threaded, in this order
dotnet test Cleansia.Tests/Cleansia.Tests.csproj                       # unit (fast)
dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj # real Postgres (Testcontainers)
dotnet test Cleansia.HostTests/Cleansia.HostTests.csproj               # authz/isolation, real Postgres
```

### Frontend (from `src/Cleansia.App/`)
```bash
# Dev servers — prefer the npm aliases; CI invokes these same ones
npm run start:cleansia-partner          # Partner :4200
npm run start:cleansia-admin            # Admin :4201
npm run start:cleansia                  # Customer :4202

# Production builds
npm run build:cleansia-partner
npm run build:cleansia-admin
npm run build:cleansia-customer

# The Nx project names are cleansia-partner.app / cleansia-admin.app / cleansia.app
# — a DOT before `app`, not a hyphen. `npx nx build cleansia-partner-app` fails with
# "Cannot find project". Check with `npx nx show projects` before hand-writing one.

# Regenerate NSwag API clients (after backend changes) — OWNER-ONLY, never run by Claude.
# Flag `manual_step: nswag-regen` instead. See "Manual Steps" below.
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

> ⚠️ **A unique index that includes `TenantId` enforces nothing in single-tenant mode.** `TenantId` is
> nullable and PostgreSQL treats NULLs as DISTINCT, so `(TenantId, …)` unique indexes admit unlimited
> duplicate rows while `TenantId` is null — which is production today. No design may treat such an
> index as its sole concurrency arbiter. `.AreNullsDistinct(false)` is a shipped construct on this
> database — `FiscalCounter`, `LiveActivityToken`, `MembershipBenefitUsage`, `PromoCodeRedemption`,
> `EmployeePayoutDetails` all use it — but adding it to an **existing** index is an `ef-migration`
> **owner-only** step and index creation fails on pre-existing duplicates. De-duplicate first.

System jobs run with no JWT context: query with `GetQueryableIgnoringTenant()`, then
`SetTenantOverride` per tenant group and commit **inside** the loop — rows are stamped from the
ambient tenant at commit time, so one deferred commit stamps every group with the last tenant
processed (`CleanupStalePendingOrders.cs:76-119` is the reference shape). A tenant-scoped repository
call inside such a sweep silently returns null.

## i18n — 5 Languages

All 3 frontend apps support: **English (en)**, **Czech (cs)**, **Slovak (sk)**, **Ukrainian (uk)**, **Russian (ru)**

Translation files: `apps/<app>/src/assets/i18n/{en,cs,sk,uk,ru}.json`

### Backend error keys land under `api.*` — not `errors.*`

Every backend error key in `BusinessErrorMessage` must have a corresponding frontend translation
under **`api.*`**, in all five locales, in **each app that can reach the endpoint**.

The reading path is the shared `HttpErrorInterceptorFn`
(`libs/core/services/src/lib/interceptors/http-error.interceptor.ts:14-20`): it takes the first value
out of the ProblemDetails `errors` bag and resolves `` `api.${dotValue}` ``. All three web apps
register it via `COMMON_INTERCEPTORS_FN` (`interceptors/index.ts:9-15`; wired at
`apps/cleansia-admin.app/src/app/app.config.ts:98`, `cleansia-partner.app/…:87`, `cleansia.app/…:92`).

```jsonc
// BusinessErrorMessage.OrderNotTakeable == "order.not_takeable"
{ "api": { "order": { "not_takeable": "This job is no longer available." } } }
```

**A key written under `errors.*` alone is read by nothing** — ngx-translate echoes the key back, the
interceptor sees `message === candidateKey` and substitutes `api.common.error_occurred`
("An error occurred. Please try again."). That silent generic fallback is exactly the failure this
rule exists to prevent, so it looks like a translation gap rather than a missing key.

> **The admin `errors.*` block is GONE (2026-08-13, PR #194) — there is now exactly one namespace.**
> It used to be live legacy: several admin features resolved through their own `XXX_ERROR_KEY_MAP`
> onto `errors.*`, so admin carried a second copy of 169 keys — of which `api.*` already had **164**.
> All 30 readers were repointed, the five unique keys moved across, and the block was deleted from all
> five locales.
>
> `api.*` is the only error namespace on every app. Two assertions in
> `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts` keep it that way — one that no
> locale carries an `errors` block, one that nothing under the admin app or its feature libs resolves
> an `errors.*` key. The parity guards themselves still assert against `BusinessErrorMessage.cs`
> directly, in every app.

## Order Lifecycle

An order's state is **two independent axes**, not one. Reading only the fulfilment axis is the single
most common mistake made against this domain.

```
FULFILMENT — Order.CurrentStatus (non-nullable)
    New (0) ──→ Confirmed (2) ──→ OnTheWay (3) ──→ InProgress (4) ──→ Completed (5)
      │              │                 │                 │
      └──────────────┴─────────────────┴─────────────────┴──────────→ Cancelled (6)

    Pending (1) is DEAD — no writer. See below.

MONEY — Order.PaymentStatus × Order.PaymentType
    Pending (1) ──→ Paid (2) | Failed (3) | Refunded (4) | Disputed (5) | PartiallyRefunded (6)
    PaymentType: Cash (1) | Card (2)
```

- `New`: order just created. **Every** order starts here, cash and card alike
  (`OrderFactory.cs:221`), with `PaymentStatus.Pending` (`OrderFactory.cs:122`).
- `Pending (1)`: **DEAD — no production writer, and none may be added (ADR-0037 D5).** See below.
- `Confirmed`: written by exactly four paths — `TakeOrder.cs:272` (a cleaner took it),
  `HandlePaymentNotification.cs:261` (the Stripe webhook, which also sets `PaymentStatus.Paid`),
  `ConfirmRecurringOrder.cs:111` (the customer confirms a recurring cash occurrence), and
  `AdminOverrideOrderStatus.cs:126`. It is deliberately overloaded — "money settled" OR "cleaner
  assigned" — so *never* read it as "a cleaner is on this job". Read `AssignedEmployees` for that.
- `OnTheWay`: cleaner is en route (`NotifyOnTheWay.cs:98`)
- `InProgress`: cleaner started work (`StartOrder.cs:140`)
- `Completed`: cleaner finished (`CompleteOrder.cs:255`)

`CurrentStatus` is a **non-nullable** persisted denormalization of the latest `OrderStatusHistory`
row, written only by the `Order.AddOrderStatus` append seam (`Order.cs:295-308`, ADR-0040). There is
no history fallback and no `!= null` conjunct — dropping it is what lets Postgres seek on
`IX_Orders_CurrentStatus_CleaningDateTime`. Do not reintroduce a nullable read.

### `OrderStatus.Pending` is dead — do not look for its writer

**Nothing in production writes `OrderStatus.Pending`** (`OrderStatus.cs:10-22`, ADR-0037 D5). The
state the old docs described — *"card payment initiated, waiting for the Stripe webhook"* — is real
and shipping, but it lives on the **payment** axis:

| Situation | `CurrentStatus` | `PaymentType` | `PaymentStatus` |
|---|---|---|---|
| Card order awaiting the webhook | `New` | `Card` | `Pending` |
| Card order paid | `Confirmed` | `Card` | `Paid` |
| One-off cash order, not yet taken | `New` | `Cash` | `Pending` |
| Cash order a cleaner took | `Confirmed` | `Cash` | `Pending` |

So the "missing" writer is not missing — it is a duplicate that was never built, and a second writer
would give one fact two sources of truth. The sweeps agree: `CleanupStalePendingOrders.cs:67-71`
(15-min timer, `CleanupStalePendingOrdersFunction.cs:10`) matches on
`PaymentStatus == Pending && PaymentType == Card && RecurringTemplateId == null` with **no status
term at all**.

`Pending` is **not deleted**: the integer is on the wire to three generated clients and legacy rows
may hold it. Readers must keep *tolerating* it in the conservative direction — a `Pending` row counts
as live for the calendar (`OrderRepository.cs:261-268`) and for GDPR (`GdprDeletionService.cs:93`),
and it stays in the admin override's rank array so those rows can still be ranked
(`AdminOverrideOrderStatus.cs:59-67`). It is never offerable, and `AdminOverrideOrderStatus.cs:103`
refuses it as a target.

> ⚠️ `StaleOrderCleanupService.cs:34` still filters on an `OrderStatus.Pending` history row and
> therefore matches nothing. It is superseded by `CleanupStalePendingOrders`; do not copy its shape.

### Offerability — which orders a cleaner may see and take (ADR-0037)

`OrderAvailability` (`src/Cleansia.Core.Domain/Orders/OrderAvailability.cs`) is the **one** rule for
"may a cleaner be offered, and take, this order". Every surface reads it; none re-derives it. It is a
property of the *order alone* — four columns in, a bool out — and it spans both axes:

```csharp
(CurrentStatus == Confirmed || (CurrentStatus == New && PaymentType == Cash))
&& (PaymentStatus == Paid  || (PaymentType == Cash && RecurringTemplateId == null))
```

A plain status list cannot express it: `New` is offerable **only for cash** (on a one-off cash order
the take *is* the confirmation), and `Confirmed` only once nothing scheduled can still retract the
order out from under the cleaner. Two evaluation forms exist on purpose — `IsOfferableSql` for
queries (`OrderSpecification.OfferableOnly` / `RestrictToEmployeeId`, `OrderSpecification.cs:147-165`)
and `IsOfferable` for the in-memory write gate — pinned against each other by an equivalence test
over real Postgres, because SQL and C# disagree on null semantics.

**The take is gated, not just the list.** `TakeOrder.Validator` (`TakeOrder.cs:46-71`) is ONE ordered
`Cascade.Stop` chain — existence-with-hold → not cancelled → not completed → **offerable** → free
seat → caller is an employee → complete profile → `ContractStatus.Approved` → not already assigned →
weekly cap → no time conflict. Order matters and a second chain would break it: FluentValidation's
class-level default is `Continue`, so a second chain runs regardless of this one's verdict.

**Preferred-cleaner hold (ADR-0036).** `OrderVisibility.NotHeldFrom`
(`src/Cleansia.Core.Domain/Orders/OrderVisibility.cs`) is a *separate* question conjoined by the
surfaces that need it: until `Order.PreferredHoldUntilUtc`, the order's **first seat** is offered to
`Order.PreferredEmployeeId` alone. It opens for everyone once the deadline passes, once any cleaner
is assigned, or if either half of the pair is null. The hold is folded into `TakeOrder`'s *existence*
check on purpose — a held order must be indistinguishable from a missing one, or the fact that
someone else was named leaks from the refusal. `PreferredEmployeeId` is never on a partner-facing DTO.

### Seats and duration

- `Order.RequiredEmployees = ceil(EstimatedTime / 120)` and `MaxEmployees = RequiredEmployees +
  BookingPolicy.SpareSeatsPerOrder` (`Order.cs:580-590`). **`SpareSeatsPerOrder` is `0`**
  (`BookingPolicy.cs:76`) — there is no spare seat, by owner ruling: pay is one row per assigned
  employee with no crew-size term, so a filled spare seat is a second full wage against an unchanged
  customer price. `CalculateRequiredEmployees` is the only writer of the cap.
- **Booked duration is capped and enforced**: `BookingPolicy.MaxBookableOrderSpanHours = 24`
  (`BookingPolicy.cs:100`). `OrderFactory.cs:158-165` throws above it and `CreateOrder.Validator`
  mirrors it as a business error. Read it as a **disclosure** bound, not a double-booking one — an
  uncapped, caller-chosen window pointed at the preferred-cleaner availability answer is a
  binary-search primitive over a cleaner's private schedule. It is also a crew cap: 24 h implies at
  most 12 seats. `Order.MaxOrderSpanHours = 168` is a different number — the overlap-scan floor
  (`OrderRepository.cs:315-330`); `cap <= floor` is the safety argument and neither moves alone.

## Pay Calculation

Source of truth: `PayCalculatorExtensions.CalculateAggregatedPay` (`src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs:30-61`)
and `OrderEmployeePay.RecomputeTotalPay` (`src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs:185-189`).
One `EmployeePayConfig` is selected per selected service and per selected package, then summed:

```
basePay     = Σ config.BasePay                                    # one config per service / package
extrasPay   = Σ (config.ExtraPerRoom × max(0, rooms - 1))         # the FIRST room is in BasePay
            + Σ (config.ExtraPerBathroom × bathrooms)
expensesPay = Σ (config.DistanceRatePerKm × order.TravelDistance)

minPay      = max(config.MinimumPay > 0)      # strongest guarantee wins; 0 = no bound
maxPay      = min(config.MaximumPay > 0)      # tightest cap wins;        0 = no bound

TotalPay    = max(0, clamp(basePay + extrasPay + expensesPay, minPay, maxPay) + bonus - deduction)
```

`extrasPay` is **rooms and bathrooms**, not the `Order.Extras` dictionary. `PayCalculator.CalculateExtrasPay`
(which does count the `Extras` flags) has no caller on the `CalculateOrderPay` path. The clamp bounds
are persisted on the `OrderEmployeePay` row so a later bonus/deduction re-clamps the core identically
instead of silently dropping the clamp.

**Per-employee overrides are shipped, not in progress.** `EmployeePayConfig.EmployeeId` is nullable —
`null` = the platform-wide rate for that service/package, non-null = an override for one cleaner,
guarded by a filtered unique index on `(EmployeeId, ServiceId, PackageId)`
(`EmployeePayConfigEntityConfiguration.cs:82-84`). Precedence is resolved in
`CalculateOrderPay.Handler.SelectPreferredConfigs` (`CalculateOrderPay.cs:159-167`): per target id,
the employee-specific config wins, otherwise the global one. Admin UI is the pay-config tab on
employee detail, seeded in bulk by `BulkCreateEmployeePayConfigs` (junior/medior/senior multipliers).

## Key Entities

| Entity | Description |
|---|---|
| `Employee` | Partner/cleaner — extends User with profile, availability, documents. Bank data moved off this row (a legacy `IBAN` column survives, see below) |
| `EmployeePayoutDetails` | ADR-0034 — where a cleaner gets paid. Own entity, one row per cleaner (`(TenantId, EmployeeId)` UNIQUE), generalized over `PayoutScheme`. Mutated in place, never tombstoned |
| `Order` | Aggregate root — services, packages, photos, notes, issues, status history. Holds the denormalized non-nullable `CurrentStatus`, `RequiredEmployees`/`MaxEmployees`, and the ADR-0036 `PreferredEmployeeId`/`PreferredHoldUntilUtc` pair |
| `EmployeePayConfig` | Pay rates per service/package; `EmployeeId` non-null = per-employee override (shipped) |
| `EmployeeInvoice` | Generated per pay period per employee |
| `PayPeriod` | Bi-weekly pay cycle (auto or manual creation) |
| `MembershipPlan` / `UserMembership` | Cleansia Plus plans and enrolments (discount, free-cancellation window, express-upgrade quota) |
| `MembershipBenefitUsage` | ADR-0035 — the metered-benefit ledger. One live row = one consumed slot |
| `Service` / `Package` | Cleaning service types and bundles with pricing |
| `Currency` / `Language` / `Country` | Platform configuration entities |

### Payout details never ride an employee DTO

Three routes, three DTOs (`Features/Employees/DTOs/PayoutDetailsDtos.cs`), and a frozen surface test
(`PayoutDtoSurfaceTests`) asserting they are the **only** DTOs in the feature surface that may carry a
payout identifier. Do not add one to `EmployeeDto`.

| DTO | Route | Carries |
|---|---|---|
| `MyPayoutDetails` | the cleaner's own | full identifiers |
| `MaskedPayoutDetails` | `AdminEmployeeController.GetEmployeePayoutDetails` (`:70-80`) | `MaskedAccount` only — **no unmasked field exists on the record** |
| `RevealedPayoutDetails` | `AdminEmployeeController.RevealEmployeePayoutDetails` (`:85-96`, rate-limited `auth`) | full identifiers |

The reveal is a **command**, not a query, precisely so the existing audit engine records it — the
audit trail is the compensating control for storing this in plaintext — and so the entity can stamp
`LastRevealedAt` / `RevealCount`. `Employee.PayoutDetails` is never `Include`d on a paged or list
query. Erasure is an id-keyed hard delete owned by `GdprDeletionService`, not a navigation walk.

The profile-completeness gate reads two **scalars** on the employee row and no navigation:
`HasPayoutDestination() => HasPayoutDetails || !string.IsNullOrEmpty(IBAN)` (`Employee.cs:321`).
The legacy `Employee.IBAN` term is load-bearing and **there is no backfill** — launch and DEV carry
cleaners whose destination predates `EmployeePayoutDetails`, and dropping the term would mark every
one of them incomplete and 403 them off the whole partner surface. It retires when the column does.

### Metered membership benefits (express-upgrade waiver)

The express surcharge (2–4 h lead, +20%) can be waived by a Plus plan, and the waiver is **metered
per calendar month**, not per enrolment:

- `ExpressWaiverResolver` answers `InExpressWindow` / `Waived` / `Quota` / `RemainingBeforeThisBooking`
  for **everyone**, guests included — clients need to tell "express, charged" apart from "not an
  express slot". `BookingPolicy.RequiresExpressSurcharge` owns the window; the resolver never
  re-encodes it.
- The quota key is `(TenantId, UserId, BenefitKind, PeriodKey)` and **nothing else**. `PeriodKey` is
  the calendar month (`"C:2026-08"`), computed once at reservation and never recomputed.
  `UserMembershipId` is a support payload column — it must never appear in a `WHERE`/`GROUP BY`/join
  on a counting path, or the quota resets on re-subscribe.
- A trialing member is active (keeps the discount and the cancellation window) but earns **no**
  waiver; the reported `Quota` still shows the plan's number so the client can say when waivers start.
- Reservation is one atomic `INSERT … SELECT … ON CONFLICT DO NOTHING RETURNING` that derives the
  smallest free ordinal in SQL. It auto-commits **before** the order exists, so `OrderId` is stamped
  afterwards on the unit of work; rows that never get one are reclaimed by
  `ReleaseOrphanedBenefitReservations`. `BookingPolicy.RequiresExpressSurcharge` takes
  `waiverApplies` as an explicit parameter so an omitted call site is greppable.

## Agent Operating System

This project is run by a team of specialized AI sub-agents that coordinate through Git-tracked
artifacts. **If you are coordinating multi-agent or multi-step work, start here:**

- **`agents/WAY-OF-WORKING.md`** — the human-facing guide to the whole flow (read first).
- **`agents/README.md`** — the roster and folder map.
- **`.claude/agents/*.md`** — the 13 agent charters (pm, analyst, architect, backend, db, frontend,
  android, ios, qa, reviewer, security, optimizer, docs). Invoke via the `Agent` tool with
  `subagent_type` = the charter's `name`.
- **`agents/process/*.md`** — ticket lifecycle, quality gates, communication protocol, routing.
- **`agents/knowledge/*.md`** — the "how we build" catalog (patterns + the S1–S12 security laws +
  conventions). **Every developer agent reads its stack catalog first.**
- **`agents/backlog/`** — tickets, stories, ADRs, sprint status, questions, audits, test-plans.

**Slash commands that exist** (`.claude/commands/`): `/feature <request>` — the full-stack entry
point, which invokes the PM end-to-end — plus the direct escape hatches `/backend` `/frontend`
`/mobile` `/review` `/docs` `/sync` for small single-shot work.

> ⚠️ `/feature.md` and older notes still reference `/team`, `/audit`, `/plan` and `/execute`. **No
> command file backs any of them** — `.claude/commands/` holds exactly the seven above. Use `/feature`
> for coordinated work, or invoke the PM directly with the `Agent` tool
> (`subagent_type: "pm"`); for an audit, invoke the relevant charter yourself and have the PM convert
> the findings into `agents/backlog/INDEX.md` rows (`agents/WAY-OF-WORKING.md:151`).

The previous YAML prompt system is archived under `agents/_legacy/` (its knowledge was folded into
`agents/knowledge/`).

## Active Bug/Improvement Tracker

**The live backlog is [`agents/backlog/INDEX.md`](agents/backlog/INDEX.md), managed by the PM. It is
the only source of truth for ticket state — this file deliberately does not enumerate it.** Ticket
state turns over several times per sprint; a copy here is stale within days and has misled agents
before. The per-sprint narrative lives in `agents/backlog/status/sprint-*.md`, open owner questions in
`agents/backlog/questions/open.md`.

Two corrections to what this section used to claim, so nobody re-derives them from an old checkout:

- **IMP-3 (per-employee pay config) is shipped**, not in progress — entity, EF config with the
  filtered unique index, `BulkCreateEmployeePayConfigs` / `UpdatePayConfig` /
  `GetEmployeePayConfigSummary`, the override precedence in `CalculateOrderPay`, and the admin
  employee-detail tab all exist. See *Pay Calculation* above.
- **Schema state**: there is one committed EF migration, `Initial`. Pre-prod, schema changes are
  folded back into it rather than stacked (owner-run — see *Manual Steps*).

## Conventions Summary

- **File naming**: PascalCase for C# files, kebab-case for Angular files
- **Branches**: `feature/*`, `fix/*`, `bugfix/*` from `master`
- **Commits**: Conventional-style — `feat:`, `fix:`, `refactor:`, `docs:`
- **PRs**: Target `master` branch
- **⚠️ NEVER credit Claude as a contributor — anywhere, in any form.** No `Co-Authored-By: Claude …`
  trailer on a commit. No `🤖 Generated with Claude Code` line in a PR body. No "generated by",
  "authored by", "with help from" or agent name in a commit message, PR description, changelog entry,
  ADR, doc page or code comment. **This overrides the harness default that asks for those trailers**,
  and it is not a style preference — the owner is the sole author of record for everything in this
  repository. If a tool or template tries to append attribution, strip it before committing.
- **Backend errors**: `category.specific_error` pattern in `BusinessErrorMessage`
- **Frontend errors**: `api.category.specific_error` in i18n files — the namespace the shared
  interceptor actually reads, and since 2026-08-13 the **only** one. Admin's legacy `errors.*` block
  is deleted and guarded against return; see *i18n* above
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
