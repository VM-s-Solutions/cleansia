using System.Security.Claims;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cleansia.Infra.Database;

public class CleansiaDbContext : DbContext, IUnitOfWork
{
    private readonly IUserSessionProvider userSessionProvider;

    public CleansiaDbContext()
    {
    }

    public CleansiaDbContext(IUserSessionProvider userSessionProvider)
    {
        this.userSessionProvider = userSessionProvider;
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions)
        : base(dbContextOptions)
    {
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions, IUserSessionProvider userSessionProvider)
        : base(dbContextOptions)
    {
        this.userSessionProvider = userSessionProvider;
    }

    public void Migrate()
    {
        Database.Migrate();
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var fullUserName = userSessionProvider.GetTypedUserClaim(ClaimTypes.Name)?.Value;
        var stateUser = string.IsNullOrWhiteSpace(fullUserName) ? "System" : fullUserName;
        var currentTime = DateTime.UtcNow;
        foreach (var entity in ChangeTracker.Entries<Auditable>())
        {
            if (entity.State == EntityState.Added)
            {
                entity.Entity.Created(stateUser, currentTime);
            }
            else if (entity.State == EntityState.Modified)
            {
                entity.Entity.Updated(stateUser, currentTime);
            }
        }
        await SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    public void Rollback()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            entry.State = EntityState.Unchanged;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        modelBuilder.HasPostgresExtension("pg_trgm");
    }

    // Entities

    public virtual DbSet<Country> Countries { get; set; }
    public virtual DbSet<Address> Addresses { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CartServiceItem> CartServiceItems { get; set; }
    public virtual DbSet<CartPackageItem> CartPackageItems { get; set; }
    public virtual DbSet<Service> Services { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<Currency> Currencies { get; set; }
    public virtual DbSet<Language> Languages { get; set; }
    public virtual DbSet<PackageService> PackageServices { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderService> OrderServices { get; set; }
    public virtual DbSet<OrderPackage> OrderPackages { get; set; }
    public virtual DbSet<OrderEmployee> OrderEmployees { get; set; }
    public virtual DbSet<OrderStatusTrack> OrderStatusHistory { get; set; }
    public virtual DbSet<EmailTranslation> EmailTranslations { get; set; }
    public virtual DbSet<EmployeePayConfig> EmployeePayConfigs { get; set; }
    public virtual DbSet<OrderEmployeePay> OrderEmployeePays { get; set; }
    public virtual DbSet<PayPeriod> PayPeriods { get; set; }
    public virtual DbSet<EmployeeInvoice> EmployeeInvoices { get; set; }
    public virtual DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
    public virtual DbSet<CountryInvoiceConfig> CountryInvoiceConfigs { get; set; }

    // Views
}
