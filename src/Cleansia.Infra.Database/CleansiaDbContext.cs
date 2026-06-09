using System.Linq.Expressions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        var actorId = userSessionProvider.GetUserId();
        var stateUser = string.IsNullOrWhiteSpace(actorId) ? "System" : actorId;
        var currentTime = DateTime.UtcNow;
        var currentTenantId = tenantProvider?.GetCurrentTenantId();

        foreach (var entity in ChangeTracker.Entries<Auditable>())
        {
            if (entity.State == EntityState.Added)
            {
                // Only auto-stamp when the entity did NOT already carry an explicit creation audit
                // (CreatedBy is unset). Domain factories that deliberately set CreatedOn/CreatedBy
                // up front (e.g. EmployeeDocument/Referral/ReferralCode, backdated/imported rows, and
                // the tests that seed stale CreatedOn) must keep that value — the previous
                // unconditional overwrite clobbered it to "now", which (among other things) made every
                // deliberately-stale seed look fresh.
                if (string.IsNullOrEmpty(entity.Entity.CreatedBy))
                {
                    entity.Entity.Created(stateUser, currentTime);
                }

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

        ApplySqliteDateTimeOffsetCompatibility(modelBuilder);

        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Test-provider shim. Production runs on Npgsql, where
    /// <see cref="DateTimeOffset"/> maps to <c>timestamp with time zone</c> and is fully comparable /
    /// sortable in SQL. The reconciliation query tests run against the in-memory SQLite provider, which
    /// has NO native <see cref="DateTimeOffset"/> support — any <c>WHERE CreatedOn &lt;= cutoff</c> or
    /// <c>ORDER BY CreatedOn</c> throws "could not be translated" / "SQLite does not support
    /// expressions of type 'DateTimeOffset'". Storing <see cref="DateTimeOffset"/> as an order-preserving
    /// binary value makes those operators translate identically under SQLite.
    ///
    /// <para>Guarded by provider name so it is a strict no-op on Npgsql — production schema and behaviour
    /// are unchanged; only the SQLite test backend gets the converter.</para>
    /// </summary>
    private void ApplySqliteDateTimeOffsetCompatibility(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
        {
            return;
        }

        // Store as UTC ticks (a long) — strictly monotonic in real time, so SQL comparison and ORDER BY
        // match prod's timestamptz semantics exactly. (DateTimeOffsetToBinaryConverter is NOT real-time
        // monotonic — ToBinary() interleaves the offset into the value — so it broke the recon cutoff.)
        var converter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        var nullableConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableConverter);
                }
            }
        }
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

            // tenantProvider == null (migrations/design-time only)
            var providerNullCheck = Expression.Equal(
                tenantProviderField,
                Expression.Constant(null, typeof(ITenantProvider)));

            // tenantProvider.GetCurrentTenantId()
            var currentTenantCall = Expression.Call(
                tenantProviderField,
                typeof(ITenantProvider).GetMethod(nameof(ITenantProvider.GetCurrentTenantId))!);

            // currentTenantId == null  (single-tenant / unauthenticated mode)
            var currentTenantNullCheck = Expression.Equal(
                currentTenantCall,
                Expression.Constant(null, typeof(string)));

            // e.TenantId == null
            var entityTenantNullCheck = Expression.Equal(
                tenantIdProperty,
                Expression.Constant(null, typeof(string)));

            // Single-tenant mode: callers without a tenant claim should see
            // entities that were also created without one. SQL's
            // `null == null` is NULL (not true), which would otherwise hide
            // every row in single-tenant deployments and in queue/webhook
            // contexts where the user's TenantId happens to also be null.
            var singleTenantMatch = Expression.AndAlso(
                currentTenantNullCheck,
                entityTenantNullCheck);

            // e.TenantId == currentTenantId  (multi-tenant happy path)
            var tenantMatch = Expression.Equal(tenantIdProperty, currentTenantCall);

            // Final: tenantProvider == null
            //     || (currentTenantId == null && e.TenantId == null)
            //     || e.TenantId == currentTenantId.
            //
            // The middle clause is what makes single-tenant mode work — without
            // it, null/null is filtered out and queue functions / unauthenticated
            // reads return zero rows even when the entity matches.
            //
            // Background jobs that need to read across tenants must still call
            // ITenantProvider.SetTenantOverride() (or use IgnoreQueryFilters)
            // before reading other tenants' data.
            var body = Expression.OrElse(
                providerNullCheck,
                Expression.OrElse(singleTenantMatch, tenantMatch));

            var filter = Expression.Lambda(body, parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    public virtual DbSet<Country> Countries { get; set; }
    public virtual DbSet<ServiceCity> ServiceCities { get; set; }
    public virtual DbSet<Address> Addresses { get; set; }
    public virtual DbSet<SavedAddress> SavedAddresses { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CartServiceItem> CartServiceItems { get; set; }
    public virtual DbSet<CartPackageItem> CartPackageItems { get; set; }
    public virtual DbSet<Service> Services { get; set; }
    public virtual DbSet<ServiceCategory> ServiceCategories { get; set; }
    public virtual DbSet<Extra> Extras { get; set; }
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
    public virtual DbSet<CountryInvoiceConfig> CountryInvoiceConfigs { get; set; }
    public virtual DbSet<EmailTemplateTranslation> EmailTemplateTranslations { get; set; }
    public virtual DbSet<OrderReceipt> OrderReceipts { get; set; }
    public virtual DbSet<FiscalCounter> FiscalCounters { get; set; }
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
    public virtual DbSet<LoyaltyAccount> LoyaltyAccounts { get; set; }
    public virtual DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
    public virtual DbSet<LoyaltyTierConfig> LoyaltyTierConfigs { get; set; }
    public virtual DbSet<PromoCode> PromoCodes { get; set; }
    public virtual DbSet<PromoCodeRedemption> PromoCodeRedemptions { get; set; }
    public virtual DbSet<ReferralCode> ReferralCodes { get; set; }
    public virtual DbSet<Referral> Referrals { get; set; }
    public virtual DbSet<MembershipPlan> MembershipPlans { get; set; }
    public virtual DbSet<UserMembership> UserMemberships { get; set; }
    public virtual DbSet<RecurringBookingTemplate> RecurringBookingTemplates { get; set; }
    public virtual DbSet<UserNotificationPreferences> UserNotificationPreferences { get; set; }
    public virtual DbSet<ProcessedStripeEvent> ProcessedStripeEvents { get; set; }
    public virtual DbSet<Refund> Refunds { get; set; }
    public virtual DbSet<DeadLetter> DeadLetters { get; set; }
    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; }
}
