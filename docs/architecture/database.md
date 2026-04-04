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

The `Order` entity is the central aggregate with multiple child collections:

```csharp
public class Order : AuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid AddressId { get; set; }
    public OrderStatus Status { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid CurrencyId { get; set; }
    public Guid TenantId { get; set; }

    // Child collections
    public ICollection<OrderService> Services { get; set; }
    public ICollection<OrderPackage> Packages { get; set; }
    public ICollection<OrderExtra> Extras { get; set; }
    public ICollection<OrderPhoto> Photos { get; set; }
    public ICollection<OrderNote> Notes { get; set; }
    public ICollection<OrderIssue> Issues { get; set; }
    public ICollection<OrderReview> Reviews { get; set; }
    public ICollection<OrderStatusHistory> StatusHistory { get; set; }
}
```

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

## Entity Relationships

```
User ─────────┬──── Customer (1:0..1)
              ├──── Employee (1:0..1)
              └──── Admin (1:0..1)

Employee ─────┬──── Order (1:N)
              ├──── EmployeeDocument (1:N)
              └──── PayPeriod (1:N)

Order ────────┬──── OrderService (1:N)
              ├──── OrderPackage (1:N)
              ├──── OrderExtra (1:N)
              ├──── OrderPhoto (1:N)
              ├──── OrderNote (1:N)
              ├──── OrderIssue (1:N)
              ├──── OrderReview (1:N)
              └──── OrderStatusHistory (1:N)

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
      --startup-project src/Cleansia.Web \
      --output efbundle \
      --self-contained

- name: Apply migrations
  run: ./efbundle --connection "${{ secrets.DB_CONNECTION_STRING }}"
```

### Creating a New Migration

```bash
# From the solution root
dotnet ef migrations add <MigrationName> \
  --project src/Cleansia.Infra.Database \
  --startup-project src/Cleansia.Web
```

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
