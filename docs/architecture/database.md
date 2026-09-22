# Database Architecture

Cleansia uses PostgreSQL 16 with Entity Framework Core 10 as the ORM. The database is shared across all tenants using a global query filter on `TenantId`.

## CleansiaDbContext

The `CleansiaDbContext` implements `IUnitOfWork` and provides automatic auditing and multi-tenancy filtering.

```csharp
public class CleansiaDbContext : DbContext, IUnitOfWork
{
    private readonly ICurrentUserService _currentUser;

    public CleansiaDbContext(
        DbContextOptions<CleansiaDbContext> options,
        ICurrentUserService currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    // DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Address> Addresses => Set<Address>();
    // ... additional DbSets
}
```

### IUnitOfWork Pattern

The `UnitOfWorkPipelineBehavior` calls `SaveChangesAsync()` on this interface after successful command execution. Handlers never call it directly.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### Automatic Auditing

`SaveChangesAsync` is overridden to stamp `CreatedBy`, `CreatedAt`, `UpdatedBy`, and `UpdatedAt` on every entity that inherits from `AuditableEntity`:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedBy = _currentUser.UserId;
                entry.Entity.CreatedAt = DateTime.UtcNow;
                break;
            case EntityState.Modified:
                entry.Entity.UpdatedBy = _currentUser.UserId;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                break;
        }
    }

    return await base.SaveChangesAsync(cancellationToken);
}
```

### Multi-Tenancy via Global Query Filter

Every entity that implements `ITenantEntity` gets a global query filter automatically applied in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply tenant filter to all ITenantEntity entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(BuildTenantFilter(entityType.ClrType));
        }
    }
}
```

::: warning
Every query automatically includes `WHERE TenantId = @currentTenantId`. To query across tenants (e.g., in admin scenarios), use `IgnoreQueryFilters()` explicitly.
:::

## Key Entities

### User and Profiles

The `User` entity is the authentication root. Each user can have one or more profile types attached:

```csharp
public class User : AuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; }        // citext column
    public string? PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Guid TenantId { get; set; }

    // Profile navigation properties
    public Customer? CustomerProfile { get; set; }
    public Employee? EmployeeProfile { get; set; }
    public Admin? AdminProfile { get; set; }
}
```

### Employee

```csharp
public class Employee : AuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public string PhoneNumber { get; set; }
    public EmployeeStatus Status { get; set; }
    public Guid TenantId { get; set; }

    // Navigation
    public ICollection<Order> Orders { get; set; }
    public ICollection<EmployeeDocument> Documents { get; set; }
    public ICollection<PayPeriod> PayPeriods { get; set; }
}
```

### Order (Aggregate Root)

The `Order` entity is the central aggregate with multiple child collections. Abridged, but with the
columns that carry rules:

```csharp
public class Order : Auditable, ITenantEntity
{
    // --- state, two axes (ADR-0037) -------------------------------------------------
    // NON-NULLABLE (ADR-0040). A persisted denormalization of the latest OrderStatusHistory
    // row, written ONLY by AddOrderStatus. There is no history fallback: dropping the
    // `!= null` conjunct is what lets Postgres seek the leading column of
    // IX_Orders_CurrentStatus_CleaningDateTime.
    public OrderStatus CurrentStatus { get; private set; }
    public PaymentType PaymentType { get; private set; }      // Cash = 1, Card = 2
    public PaymentStatus PaymentStatus { get; private set; }  // Pending = 1, Paid = 2, …
    public string? RecurringTemplateId { get; private set; }

    // --- crew ------------------------------------------------------------------------
    public int RequiredEmployees { get; private set; }   // ceil(EstimatedTime / 120)
    public int MaxEmployees { get; private set; }        // RequiredEmployees + 0 spare seats
    public int EstimatedTime { get; private set; }       // minutes; capped at 24 h at write time

    // --- preferred-cleaner first refusal (ADR-0036) ----------------------------------
    // A pair with one meaning. GrantPreferredHold refuses a deadline with no beneficiary,
    // and OrderVisibility reads a half-written pair as "no hold" — a hold nobody may act on
    // is a hold no actor is permitted to clear.
    public string? PreferredEmployeeId { get; private set; }
    public DateTime? PreferredHoldUntilUtc { get; private set; }

    public DateTime CleaningDateTime { get; private set; }
    public decimal TotalPrice { get; private set; }
    public string CurrencyId { get; private set; }       // FK Restrict; named by the caller, never derived from the address
    public string? TenantId { get; private set; }        // NOT NULL in the DB: the operating company; set at commit

    // Child collections
    public IReadOnlyCollection<OrderService> SelectedServices { get; }
    public IReadOnlyCollection<OrderPackage> SelectedPackages { get; }
    public IReadOnlyCollection<OrderEmployee> AssignedEmployees { get; }  // many-to-many
    public IReadOnlyCollection<OrderPhoto> Photos { get; }
    public IReadOnlyCollection<OrderNote> OrderNotes { get; }
    public IReadOnlyCollection<OrderIssue> OrderIssues { get; }
    public IReadOnlyCollection<OrderReview> Reviews { get; }
    public IReadOnlyCollection<OrderStatusTrack> OrderStatusHistory { get; }
}
```

::: warning An order does not have "an employee"
Assignment is a many-to-many through `OrderEmployee`, bounded by `MaxEmployees`. There is no
`EmployeeId` column on `Orders`, and `PreferredEmployeeId` is a *customer request*, not an
assignment. To ask "has a cleaner taken this job", count `AssignedEmployees` — never read
`CurrentStatus == Confirmed`, which four paths write and only one of them involves a cleaner.
:::

The identifier type across the schema is a 26-character **ULID string**, not `Guid` — the sketches on
this page use `Guid` for brevity where the id type is not the point.

### Catalogue and prices

A catalogue entry — `Service`, `Package`, `Extra` — carries **no price and no currency**. Its price
lives in a sibling table, one row per currency the entry is sold in: `ServicePrices` (`BasePrice`,
`PerRoomPrice`), `PackagePrices` (`Price`) and `ExtraPrices` (`Price`). Each is unique on
`(EntryId, CurrencyId)`, cascades from its entry and restricts from its currency.

```csharp
public class Service : Auditable
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public int EstimatedTime { get; private set; }
    public string CategoryId { get; private set; }
    // No BasePrice, no PerRoomPrice, no CurrencyId — the price is a ServicePrice row.
}

public class ServicePrice : Auditable            // IX_ServicePrices_ServiceId_CurrencyId, unique
{
    public string ServiceId { get; private set; }
    public string CurrencyId { get; private set; }
    public decimal BasePrice { get; private set; }     // flat component
    public decimal PerRoomPrice { get; private set; }  // charged per room AND per bathroom
}
```

A price is **authored, never converted** (owner ruling 2026-09-08). There is no exchange rate
anywhere in the schema — `Currency` lost its `ExchangeRate` column when these tables arrived — so a
EUR price is a number somebody chose for that market, not the CZK figure multiplied by a rate.
`OrderPricingCalculator` reads the row in the order's currency, and an entry with no row in a currency
is not offerable in it: withheld from the catalogue, refused on quote and on create. Fail closed, never
zero. The bulk grade apply's pay-rate multiplier reads the same row, in the admin-chosen currency;
`ApproveEmployee` creates no pay config and only checks that a platform-wide rate exists in the
currency resolved for the cleaner's work country.

::: tip Pricing formula
`ServiceTotal = BasePrice + PerRoomPrice × (rooms + bathrooms)`, from the `ServicePrices` row in the
order's currency. The seed prices General Cleaning at 500 CZK base + 150 CZK per room, so a two-room,
one-bathroom flat quotes 500 + 150 × 3 = 950 CZK. → /product/business-rules#price-stages
:::

### Currency

`Currencies` is a small table that every table denominating money points at.

| Column | Meaning |
|--------|---------|
| `Code` | ISO 4217, `citext`, unique (`IX_Currencies_Code_Unique`); canonicalised to upper case on write because it goes onto receipts as typed |
| `IsDefault` | Exactly one row — the partial unique index `IX_Currencies_IsDefault_Unique` on `"IsDefault" = true` |
| `IsActive` | **The market switch, not a soft-delete flag.** A currency is created switched off; `ActivateCurrency` is the only writer of `true`, and the default cannot be switched off (`currency.cannot_deactivate_default`) |
| `LoyaltyPointsDivisor` | `numeric(18,2)`, nullable. How much of the currency earns one loyalty point — `floor(amount / divisor)`. Authored per currency; CZK is seeded at 10, and a null divisor earns nothing |

There is no `ExchangeRate` column. A currency is **offerable** — nameable on a quote or an order — when
it exists, is active and has at least one row in any of the three price tables
(`ICurrencyRepository.IsOfferableAsync`). The quote and create validators and `SetDefaultCurrency`'s
promotion gate (`currency.not_priced`) ask that one predicate.

The tables that denominate money carry the currency they are in:

| Table | `CurrencyId` | On delete | What it means |
|-------|--------------|-----------|---------------|
| `Orders` | required | Restrict | Stamped from the service address's country (`CountryConfiguration.DefaultCurrencyCode`); a `CurrencyId` the caller sends must equal it or create refuses `currency.invalid` |
| `ServicePrices` / `PackagePrices` / `ExtraPrices` | required | Restrict | Half of the unique key — one price per entry per currency |
| `EmployeePayConfigs` | required | Restrict | In the unique index — a rate is an amount in one currency, and the pay writer reads only rows in the order's currency |
| `OrderEmployeePays` | required | Restrict | The currency the pay row was computed in |
| `EmployeeInvoices` | required | Restrict | In the unique index `IX_EmployeeInvoices_EmployeeId_PayPeriodId_CurrencyId` — one invoice per currency a cleaner's pay spans in a period; derived from the pay rows, never supplied |
| `EmployeePayoutDetails` | nullable | Restrict | The currency the cleaner declares their bank account holds; null = undeclared, which `ApproveInvoice` reads as the work country's currency |
| `CreditAccounts` | required | no FK | Unique `(UserId, CurrencyId)` — one balance per customer per currency, never converted at spend time |

Because the FKs restrict, `DeleteCurrency` asks `ICurrencyRepository.IsInUseAsync` first and answers
`currency.in_use` instead of surfacing a raw `23503` at commit.

### Supporting Entities

| Entity | Purpose |
|--------|---------|
| `Currency` | The currencies the platform operates in — see [Currency](#currency). `IsActive` is the market switch, `LoyaltyPointsDivisor` is authored per currency, and there is no exchange rate |
| `ServicePrice` / `PackagePrice` / `ExtraPrice` | A catalogue entry's price in one currency, unique on `(EntryId, CurrencyId)` — see [Catalogue and prices](#catalogue-and-prices). The entry itself carries no price |
| `Language` | Multi-language support for service names, descriptions |
| `Address` | Customer addresses with GPS coordinates |
| `Package` | Bundled services at a discount; priced per currency in `PackagePrices` |
| `Extra` | An add-on line keyed by `Slug` (which `OrderExtras.Slug` snapshots, so it is fixed at creation); priced per currency in `ExtraPrices` |
| `PayPeriod` | Employee payment tracking periods |
| `OrderEmployeePay` | One pay row per `(OrderId, EmployeeId)`, unique; carries the `CurrencyId` it was computed in |
| `EmployeeInvoice` | A period's payout invoice for one cleaner **in one currency** — unique on `(EmployeeId, PayPeriodId, CurrencyId)`, so a cleaner whose pay spans two currencies in a period holds two invoices |
| `EmployeeDocument` | Uploaded employee documents (contracts, IDs) |
| `EmployeePayoutDetails` | ADR-0034 — the cleaner's bank destination. **Its own table**, one row per cleaner, `(TenantId, EmployeeId)` unique with `NULLS NOT DISTINCT`. Never `Include`d on a list query. Carries a nullable `CurrencyId` (FK Restrict): the currency the cleaner declares the account holds |
| `EmployeePayConfig` | Pay rates per service/package **in one currency**; nullable `EmployeeId` = per-employee override, `null` = the platform-wide default **of one operating company** (the rows are stamped — pay rates are the operator's money). Unique `IX_EmployeePayConfigs_Tenant_Scope` on `(TenantId, EmployeeId, ServiceId, PackageId, CurrencyId)` with `NULLS NOT DISTINCT` and **no filter** — every row carries a null by construction (one config per service *or* per package, never both), so a filtered nulls-distinct index rejected nothing while excluding the platform-wide rows `CalculateOrderPay` reads with no `ORDER BY`; the leading tenant term is what lets two operators each hold a default for the same service and currency |
| `CreditAccount` / `CreditTransaction` | A customer's credit balance, one account per `(UserId, CurrencyId)` (unique), with an append-only ledger. `Balance` is stored, not summed, because the spend is a conditional `UPDATE … WHERE Balance >= amount` |
| `MembershipPlan` / `UserMembership` | Cleansia Plus plans and enrolments |
| `MembershipBenefitUsage` | ADR-0035 — the metered-benefit ledger. Two indexes, and confusing them is the trap: `IX_MembershipBenefitUsages_Slot` on `(TenantId, UserId, BenefitKind, PeriodKey, SlotOrdinal)` is unique, `NULLS NOT DISTINCT`, filtered to live rows, and **is the sole arbiter of the reservation race** — the `SlotOrdinal` column is what lets a quota be N rather than 1. `IX_MembershipBenefitUsages_Quota`, the same key **without** `SlotOrdinal`, is **not unique**; it only serves the remaining-count read |
| `OrderReceipt` | Generated receipt per order, including fiscal-registration state |
| `FiscalCounter` | Per-issuer gapless fiscal sequence counter (see below) |
| `WorkContractAcceptance` | ADR-0068 — one cleaner's acceptance of the contract for work for **one seat** of one job. `IX_WorkContractAcceptances_OrderEmployeeId` is **unique** (one contract per seat; the arbiter of a concurrent double accept), the seat and the cleaner are bare scalars with no FK (the seat row is hard-deleted by the next drop, the cleaner is anonymised), `LegalDocumentTextId` and `OrderId` are `Restrict` FKs, `FactsJson` is `jsonb`. Append-only: `Pseudonymise()` blanks the IP / device columns at erasure and after the per-company window; nothing deletes a row. `Orders.WorkContractDocumentId` (nullable, FK `LegalDocuments` Restrict, indexed) is the order's half |

::: danger A unique index over a nullable column enforces nothing unless it says so
Postgres treats NULLs as DISTINCT, so a unique index containing a nullable column admits unlimited
duplicates while that column is null. `.AreNullsDistinct(false)` is what makes such an index an
arbiter, and **17 indexes carry it in the emitted DDL** (`grep -n NullsDistinct …Initial.cs`) —
thirteen with a tenant term, four without (`DisputeLines`, `LegalDocuments`, `LiveActivityTokens`,
`OrderReviewLines`).

**`TenantId` is no longer the nullable column that matters.** Since ADR-0061 D8 (2026-09-13) the
column is NOT NULL on every stamped table — only `OutboxMessages` and `DeadLetters` keep it nullable —
and since 2026-09-15 it is a foreign key into `Tenants` on every one of them and absent from every
tenantless table (`TenantAuditable` carries it; plain `Auditable` does not), so the tenant term of a
`(TenantId, …)` index can never be the null that disarms it. The option is kept on those thirteen
indexes anyway, because the model guard reads the option, not the column, and the other nullable
terms (`EmployeeId`, `ServiceId`, `PackageId`) still need it. Two indexes *gained* a tenant term on
activation so that two operators can coexist — `OrderReceipts (TenantId, ReceiptNumber)` (the number
comes from a per-operator `FiscalCounter`) and `IX_EmployeePayConfigs_Tenant_Scope` — and three more
on 2026-09-15, when each operating company started numbering its own payout invoices:
`EmployeeInvoices (TenantId, InvoiceNumber)`, `EmployeeInvoices (TenantId, VariableSymbol)` filtered,
and `PayoutReferenceCounters (TenantId, Year, Scope)`. One lost it: `Users (Email)` is globally unique
with no tenant term, because one email is one identity across the holding (ADR-0061 D5.1).
`LoyaltyTierConfig` left the list with its tenant — it is tenantless now, unique on `Tier`.

Six of the seventeen were added on 2026-09-05, after a guard was rewritten from a hand-listed roster
into a sweep of the whole model and found them: `EmployeePayConfig`, `LoyaltyTransaction`, `PromoCode`,
`ReferralCode`, `TenantConfiguration` (and `LoyaltyTierConfig`, since reclassified). Each had read as
enforcing while enforcing nothing. Another, `FeatureFlag`, was on that list until T-0689 deleted the
table outright.

**You do not need to remember to add it.** `NullsNotDistinctIndexModelTests` walks every unique index
in the model, and one carrying a nullable column must either declare `NULLS NOT DISTINCT`, be filtered
so the null cannot appear (`EmployeeInvoice`, `Order`'s recurring-template index), or be named as a
deliberate exception (`UserMembership`, a backstop behind an authoritative read). `TenantIdRequiredModelTests`
is its twin for the column itself: every `ITenantEntity` is required except the two named exemptions,
every one is a `Restrict` foreign key into `Tenants`, and no other type carries a `TenantId`;
`InitialMigrationTenantDdlTests` pins the same three facts on the committed migration's operations.

Two shapes are worth knowing. `LoyaltyTransaction` keeps **both** a tenant term and a filter: the key
is a caller-supplied token, so a bare global index would read a cross-tenant collision as a replay,
while dropping the filter would collapse every null-key row onto one key. And on an index that has
already shipped, adding this fails on pre-existing duplicates — pre-prod that is free, because
`Initial` is regenerated and the database rebuilt.
:::

## Fiscal Sequence Allocation

DE TSE, AT RKSV, and ES VeriFactu legally require a **gapless, monotonic, per-issuer** fiscal
sequence. The receipt number is allocated from the `FiscalCounter` table, never by counting receipt
rows.

`FiscalCounter` is tenant-scoped (`ITenantEntity`) and keyed by the unique index
`(TenantId, Year, IssuerScope)` — one counter row per operating company, year and issuer scope, which
is why `OrderReceipts` is unique on `(TenantId, ReceiptNumber)` rather than on the number alone: two
operators' first receipts of a year are the same string by construction. `FiscalCounterRepository
.AllocateNextAsync` performs a single atomic
`INSERT … ON CONFLICT (…) DO UPDATE SET Value = Value + 1 RETURNING Value`. Postgres row-locks the
conflicting tuple, so N concurrent allocations for one scope return N distinct contiguous numbers.

The allocation runs on the receipt consumer's open transaction — the same one that commits the
phase-1 receipt claim — so a committed claim never holds a rolled-back number, and a rolled-back or
voided claim returns its number to the pool **without shifting** the next allocation. A
reserved-but-never-signed number on a *committed* claim is a documented gap (void); it is never
re-allocated.

### IssuerScope mapping per regime

`IssuerScope` binds gaplessness to the legal counting unit, and the `Year` key encodes the
annual-reset rule. Both are resolved from the fiscal provider key by `FiscalSequenceScope.Resolve`.

| Regime | Provider key | IssuerScope | Year key | Annual reset |
|--------|--------------|-------------|----------|--------------|
| CZ EET 2.0 | `cz-eet2` | provider key | calendar year | Yes |
| SK eKasa | `sk-ekasa` | provider key | calendar year | Yes |
| DE TSE | `de-tss-*` | provider key (TSE identity; provider+device once multi-TSE config lands) | `NoAnnualResetYear` (0) | No |
| AT RKSV / ES VeriFactu | per issuer | provider key | `NoAnnualResetYear` (0) | No |
| No fiscal system (CZ today) | — | `DEFAULT` | calendar year | Yes |

DE TSE's transaction counter is **not** assumed to reset at the year boundary; such regimes key on
`NoAnnualResetYear` so the same counter row keeps incrementing across years. The displayed receipt
number still embeds the calendar year for readability; only the sequence *value* comes from the
continuous counter.

## Entity Relationships

```
User ─────────┬──── Customer (1:0..1)
              ├──── Employee (1:0..1)
              └──── Admin (1:0..1)

Employee ─────┬──── OrderEmployee (1:N) ────┐   # assignment join, bounded by Order.MaxEmployees
              ├──── EmployeeDocument (1:N)  │
              ├──── EmployeePayoutDetails (1:0..1)
              ├──── EmployeePayConfig (1:N, override rows)
              └──── PayPeriod (1:N)         │
                                            │
Order ────────┬──── OrderEmployee (1:N) ────┘
              ├──── OrderService (1:N)
              ├──── OrderPackage (1:N)
              ├──── OrderExtra (1:N)
              ├──── OrderPhoto (1:N)
              ├──── OrderNote (1:N)
              ├──── OrderIssue (1:N)
              ├──── OrderReview (1:N)
              ├──── OrderStatusTrack (1:N)     # the audit trail; Order.CurrentStatus denormalizes it
              └──── MembershipBenefitUsage (1:0..1)

Service ──────┬──── Package (1:N)
              └──── ServicePrice (1:N, cascade)   # one row per currency; Package/Extra mirror this

Currency ─────┬──── ServicePrice / PackagePrice / ExtraPrice (1:N, restrict)
              ├──── Order (1:N, restrict)
              ├──── EmployeePayConfig (1:N, restrict)
              ├──── OrderEmployeePay (1:N, restrict)
              ├──── EmployeeInvoice (1:N, restrict)
              ├──── EmployeePayoutDetails (1:N, restrict, nullable)
              └──── CreditAccount (1:N, no FK)

Address ──────┬──── Order (1:N)
              └──── Customer (N:1)
```

## PostgreSQL Extensions

The database uses two PostgreSQL extensions configured in migrations:

| Extension | Purpose |
|-----------|---------|
| `citext` | Case-insensitive text type, used for `Email` columns to avoid `LOWER()` calls in every query |
| `pg_trgm` | Trigram matching for fuzzy text search (employee search, customer lookup) |

```sql
-- Enabled in initial migration
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Example: Email column uses citext
ALTER TABLE "Users" ALTER COLUMN "Email" TYPE citext;

-- Example: Trigram index for employee search
CREATE INDEX "IX_Users_FirstName_trgm" ON "Users"
    USING gin ("FirstName" gin_trgm_ops);
```

## Migrations Strategy

### Development

In development, the application auto-migrates on startup via the `Cleansia.Config` startup configuration:

```csharp
// Applied in development only
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
    await dbContext.Database.MigrateAsync();
}
```

::: warning
Auto-migration is **disabled in production**. Never rely on startup migration for production deployments.
:::

### Production

Production uses the EF Core migrations bundle, built and executed in the CI/CD pipeline:

```yaml
# Simplified CI/CD step
- name: Build migrations bundle
  run: |
    dotnet tool restore
    dotnet ef migrations bundle \
      --project src/Cleansia.Infra.Database \
      --startup-project src/Cleansia.Web.Partner \
      --output efbundle \
      --self-contained

- name: Apply migrations
  run: |
    # The connection string comes from Key Vault (the same secret the runtime hosts resolve),
    # so a password rotation touches one place.
    DB_CONNECTION_STRING="$(az keyvault secret show \
      --vault-name kv-cleansia-<region>-<env> \
      --name ConnectionStrings--cleansia-db --query value -o tsv)"
    ./efbundle --connection "$DB_CONNECTION_STRING"
```

### Creating a New Migration

```bash
# From the repo root. Cleansia.Web.Partner is the startup host for design-time EF.
dotnet ef migrations add <MigrationName> \
  --project src/Cleansia.Infra.Database \
  --startup-project src/Cleansia.Web.Partner
```

::: warning Pre-prod there is exactly ONE migration, and regenerating it is routine
The committed history is a single `Initial` migration. While the platform is pre-production, schema
changes are folded back into it rather than stacked on top, so the shipped set stays one file.
Regenerating it (`dotnet ef migrations remove --force` then `add Initial`, startup project a web host)
is ordinary work for whoever changes the model, paired with the DEV database drop and proven by the
integration suite — owner rulings 2026-08-15, 2026-08-25 and 2026-09-07; nothing here is a manual
step any more.

Today that file is **`20260920204705_Initial`**, regenerated on 2026-09-20 to drop what nothing read:
the employee availability schedule (`Employees.Availability`), `Employees.PreferredCurrencyCode`, the
`Carts` / `CartServiceItems` / `CartPackageItems` trio and `EmailTranslations` — **84 tables**, 48 of
them stamped with a `Tenants` foreign key (`TenantId` NOT NULL on 46; `OutboxMessages` and
`DeadLetters` nullable). It replaced `20260919231739` (the contract for work,
[ADR-0068](/decisions/adr-0068), 88 tables) and `20260919142517` (the administrator roles,
2026-09-19); the chain of regenerations before that — the per-currency schema of
2026-09-12, the customer audit and legal documents, the tenant FK, the company lifecycle, the status
concurrency token, the recurring pause state — is kept in `agents/cleanup/MANUAL_STEPS.md` MS-2.
Regenerating changes the migration id, so a DEV database whose `__EFMigrationsHistory` records an
older id replays the whole create script against tables that already exist — drop it before the
first start against the new file (MS-2 names the id the owed drop belongs to).
:::

## Database Configuration

Connection to PostgreSQL is configured through .NET Aspire in the `AppHost` and resolved via `Cleansia.Config`:

```csharp
// In Cleansia.AppHost
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("cleansia-db");

// In Cleansia.Config
builder.AddNpgsqlDbContext<CleansiaDbContext>("cleansia-db", options =>
{
    options.UseNpgsql(npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("Cleansia.Infra.Database");
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
    });
});
```

## Conventions

| Convention | Detail |
|-----------|--------|
| Primary keys | `Guid` (generated client-side) |
| Table names | Pluralized entity names (EF default) |
| Soft deletes | Not used — hard deletes with GDPR cleanup function |
| Timestamps | All `DateTime` stored as UTC |
| String columns | `citext` for emails, `text` for everything else (no `varchar` limits) |
| Indexes | Explicit indexes on foreign keys and frequently queried columns |
