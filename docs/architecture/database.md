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
    public string CurrencyId { get; private set; }
    public string? TenantId { get; private set; }        // null = single-tenant mode

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

### Service and Pricing

Services use a two-part pricing model:

```csharp
public class Service : AuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }      // Fixed component
    public decimal PerRoomPrice { get; set; }    // Multiplied by room count
    public Guid CurrencyId { get; set; }
    public Guid TenantId { get; set; }

    public Currency Currency { get; set; }
    public ICollection<Package> Packages { get; set; }
}
```

::: tip Pricing Formula
`TotalServicePrice = BasePrice + (PerRoomPrice * NumberOfRooms)`

This allows flexible pricing where a "Basic Clean" might cost 500 CZK base + 100 CZK per room.
:::

### Supporting Entities

| Entity | Purpose |
|--------|---------|
| `Currency` | Multi-currency support (CZK, EUR, etc.) |
| `Language` | Multi-language support for service names, descriptions |
| `Address` | Customer addresses with GPS coordinates |
| `Package` | Bundled services at a discount |
| `PayPeriod` | Employee payment tracking periods |
| `EmployeeDocument` | Uploaded employee documents (contracts, IDs) |
| `EmployeePayoutDetails` | ADR-0034 — the cleaner's bank destination. **Its own table**, one row per cleaner, `(TenantId, EmployeeId)` unique with `NULLS NOT DISTINCT`. Never `Include`d on a list query |
| `EmployeePayConfig` | Pay rates per service/package; nullable `EmployeeId` = per-employee override, with a filtered unique index on `(EmployeeId, ServiceId, PackageId) WHERE "EmployeeId" IS NOT NULL` |
| `MembershipPlan` / `UserMembership` | Cleansia Plus plans and enrolments |
| `MembershipBenefitUsage` | ADR-0035 — the metered-benefit ledger, keyed `(TenantId, UserId, BenefitKind, PeriodKey)` with a filtered `NULLS NOT DISTINCT` unique index that is the sole arbiter of the reservation race |
| `OrderReceipt` | Generated receipt per order, including fiscal-registration state |
| `FiscalCounter` | Per-issuer gapless fiscal sequence counter (see below) |

::: danger `TenantId` is nullable — a unique index containing it enforces nothing today
Postgres treats NULLs as DISTINCT, so `(TenantId, …)` unique indexes admit unlimited duplicates while
`TenantId` is null, which is production. Nine indexes are in that position. Any design that needs a
concurrent arbiter must declare `.AreNullsDistinct(false)` (as `FiscalCounter`, `LiveActivityToken`,
`MembershipBenefitUsage`, `PromoCodeRedemption` and `EmployeePayoutDetails` do) or carry a second
guard. Adding it to an existing index is an owner-only migration and fails on pre-existing duplicates.
:::

## Fiscal Sequence Allocation

DE TSE, AT RKSV, and ES VeriFactu legally require a **gapless, monotonic, per-issuer** fiscal
sequence. The receipt number is allocated from the `FiscalCounter` table, never by counting receipt
rows.

`FiscalCounter` is tenant-scoped (`ITenantEntity`) and keyed by the unique index
`(TenantId, Year, IssuerScope)` — declared `NULLS NOT DISTINCT` so a single-tenant (null `TenantId`)
deployment collapses onto one counter row per `(Year, IssuerScope)`. `FiscalCounterRepository
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
              └──── Currency (N:1)

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

::: warning Owner-only, and pre-prod there is exactly ONE migration
The committed history is a single `Initial` migration. While the platform is pre-production, schema
changes are folded back into it rather than stacked on top, so the shipped set stays one file. Only
the owner runs `dotnet ef migrations add` / `database update`; agents flag
`manual_step: ef-migration` instead.
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
