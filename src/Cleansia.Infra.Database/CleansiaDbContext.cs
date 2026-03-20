using System.Linq.Expressions;
using System.Security.Claims;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.ReceiptTemplates;
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
    private readonly ITenantProvider tenantProvider;

    public CleansiaDbContext()
    {
    }

    public CleansiaDbContext(IUserSessionProvider userSessionProvider, ITenantProvider tenantProvider)
    {
        this.userSessionProvider = userSessionProvider;
        this.tenantProvider = tenantProvider;
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions)
        : base(dbContextOptions)
    {
    }

    public CleansiaDbContext(DbContextOptions dbContextOptions, IUserSessionProvider userSessionProvider, ITenantProvider tenantProvider)
        : base(dbContextOptions)
    {
        this.userSessionProvider = userSessionProvider;
        this.tenantProvider = tenantProvider;
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
        var currentTenantId = tenantProvider?.GetCurrentTenantId();

        foreach (var entity in ChangeTracker.Entries<Auditable>())
        {
            if (entity.State == EntityState.Added)
            {
                entity.Entity.Created(stateUser, currentTime);

                if (entity.Entity is ITenantEntity tenantEntity && string.IsNullOrEmpty(tenantEntity.TenantId))
                {
                    tenantEntity.TenantId = currentTenantId;
                }
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

        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");

        ApplyTenantQueryFilters(modelBuilder);
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");

            // e.TenantId
            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));

            // this.tenantProvider.GetCurrentTenantId()
            var tenantProviderField = Expression.Field(
                Expression.Constant(this),
                typeof(CleansiaDbContext).GetField(nameof(tenantProvider),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!);

            // tenantProvider == null (for migrations/design-time)
            var providerNullCheck = Expression.Equal(
                tenantProviderField,
                Expression.Constant(null, typeof(ITenantProvider)));

            // tenantProvider.GetCurrentTenantId()
            var currentTenantCall = Expression.Call(
                tenantProviderField,
                typeof(ITenantProvider).GetMethod(nameof(ITenantProvider.GetCurrentTenantId))!);

            // currentTenantId == null (no tenant context = show all)
            var tenantIdNullCheck = Expression.Equal(
                currentTenantCall,
                Expression.Constant(null, typeof(string)));

            // e.TenantId == currentTenantId
            var tenantMatch = Expression.Equal(tenantIdProperty, currentTenantCall);

            // Final: tenantProvider == null || currentTenantId == null || e.TenantId == currentTenantId
            var body = Expression.OrElse(
                providerNullCheck,
                Expression.OrElse(tenantIdNullCheck, tenantMatch));

            var filter = Expression.Lambda(body, parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    // Entities

    public virtual DbSet<Country> Countries { get; set; }
    public virtual DbSet<Address> Addresses { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
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
    public virtual DbSet<OrderNote> OrderNotes { get; set; }
    public virtual DbSet<OrderIssue> OrderIssues { get; set; }
    public virtual DbSet<OrderReview> OrderReviews { get; set; }
    public virtual DbSet<EmailTranslation> EmailTranslations { get; set; }
    public virtual DbSet<EmployeePayConfig> EmployeePayConfigs { get; set; }
    public virtual DbSet<OrderEmployeePay> OrderEmployeePays { get; set; }
    public virtual DbSet<PayPeriod> PayPeriods { get; set; }
    public virtual DbSet<EmployeeInvoice> EmployeeInvoices { get; set; }
    public virtual DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
    public virtual DbSet<CountryInvoiceConfig> CountryInvoiceConfigs { get; set; }
    public virtual DbSet<EmailTemplateTranslation> EmailTemplateTranslations { get; set; }
    public virtual DbSet<ReceiptTemplate> ReceiptTemplates { get; set; }
    public virtual DbSet<OrderReceipt> OrderReceipts { get; set; }
    public virtual DbSet<CompanyInfo> CompanyInfo { get; set; }
    public virtual DbSet<Device> Devices { get; set; }
    public virtual DbSet<Dispute> Disputes { get; set; }
    public virtual DbSet<DisputeMessage> DisputeMessages { get; set; }
    public virtual DbSet<DisputeEvidence> DisputeEvidence { get; set; }
    public virtual DbSet<TenantConfiguration> TenantConfigurations { get; set; }
    public virtual DbSet<CountryConfiguration> CountryConfigurations { get; set; }
    public virtual DbSet<FeatureFlag> FeatureFlags { get; set; }
    public virtual DbSet<UserConsent> UserConsents { get; set; }
    public virtual DbSet<GdprRequest> GdprRequests { get; set; }

    // Views
}
