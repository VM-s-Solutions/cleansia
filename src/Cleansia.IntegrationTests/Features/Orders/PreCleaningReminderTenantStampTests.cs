using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The pre-cleaning sweep runs system-level with no JWT, so it reads across tenants and must write each
/// order's notification back under that order's own tenant. Getting it wrong is invisible in a unit
/// test with one tenant and catastrophic in production: the customer's app filters the feed and the
/// drainer routes the push by tenant, so a row stamped with the wrong one is delivered to nobody and
/// looks exactly like a delivery bug.
///
/// <para>Against real PostgreSQL, because tenancy here is enforced by a global query filter compiled
/// into SQL and by NULL semantics that SQLite does not share — a legacy single-tenant order carries
/// <c>TenantId IS NULL</c>, and "NULL stays NULL rather than inheriting the previous group's tenant" is
/// a claim about the provider, not about C#.</para>
///
/// <para>The schema is built from the live EF model via EnsureCreated on a dedicated database (NOT the
/// shared migration-applied one) so it exercises the <c>PreCleaningReminderSentAt</c> column this
/// ticket introduces, ahead of the owner-run ef-migration that lands it in the deployed schema — the
/// same arrangement <c>CatalogDeleteFkRestrictPostgresTests</c> uses.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PreCleaningReminderTenantStampTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string CountryId = "country-cz-reminder";
    private const string CurrencyId = "currency-czk-reminder";

    private NpgsqlDataSource _dataSource = default!;
    private readonly MutableTenantProvider _tenantProvider = new();

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "pre_cleaning_reminder_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await bootstrap.Database.EnsureCreatedAsync();
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
        await DropOrderForeignKeysAsync(conn);
        await SeedCatalogAsync();
    }

    /// <summary>
    /// The address FK is left standing, so the country and currency an order references are real rows.
    /// Only the Orders / OrderEmployees FKs are dropped, and only so the fixture can name a customer and
    /// a cleaner without building two full identity graphs the sweep never reads.
    /// </summary>
    private async Task SeedCatalogAsync()
    {
        _tenantProvider.ClearTenantOverride();
        await using var ctx = NewContext();

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);

        await ctx.CommitAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }
        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private static async Task DropOrderForeignKeysAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f'
                         AND conrelid IN ('"Orders"'::regclass, '"OrderEmployees"'::regclass)
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedDueOrderAsync(string key, string userId, string? tenantId)
    {
        var orderId = $"ord-{key}";
        var order = Order.Create(
            customerName: "Tenant Customer",
            customerEmail: "tenant-customer@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Tenant St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddMinutes(60),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
        order.Id = orderId;
        order.TenantId = tenantId;
        order.CalculateRequiredEmployees(spareSeats: 0);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        // A cleaner is committed to this job, which is the half of "Confirmed" the reminder needs and
        // the half that status alone does not carry. Inserted as a bare row: the sweep asks only whether
        // an assignment EXISTS, and a full employee graph would add nothing but fixtures.
        _tenantProvider.ClearTenantOverride();
        await using var ctx = NewContext();
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"OrderEmployees\" (\"Id\", \"OrderId\", \"EmployeeId\", \"IsActive\", \"SeatOrdinal\") VALUES ({0}, {1}, {2}, true, 0)",
            $"oe-{key}", orderId, $"emp-{key}");
    }

    private async Task<SendPreCleaningReminders.Response> RunSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new SendPreCleaningReminders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            ctx,
            NullLogger<SendPreCleaningReminders.Handler>.Instance);

        var result = await handler.Handle(new SendPreCleaningReminders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public async Task Each_Tenants_Reminder_Is_Stamped_With_Its_Own_Tenant_In_One_Pass()
    {
        await SeedDueOrderAsync("pre-a", "user-tenant-a", TenantA);
        await SeedDueOrderAsync("pre-b", "user-tenant-b", TenantB);
        await SeedDueOrderAsync("pre-legacy", "user-legacy", tenantId: null);

        var response = await RunSweepAsync();

        Assert.Equal(3, response.RemindersSent);

        _tenantProvider.ClearTenantOverride();
        await using var ctx = NewContext();
        var reminders = await ctx.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .Select(m => new { m.MessageKey, m.TenantId, m.Body })
            .ToListAsync();

        foreach (var (userId, expectedTenant) in new[]
                 {
                     ("user-tenant-a", TenantA),
                     ("user-tenant-b", TenantB),
                     ("user-legacy", (string?)null),
                 })
        {
            var reminder = Assert.Single(reminders, r => r.MessageKey.Contains(userId, StringComparison.Ordinal));

            // The row's column, which is what the drainer filters on...
            Assert.Equal(expectedTenant, reminder.TenantId);

            // ...and the envelope's own tenantId, which is what the dispatch consumer sets its override
            // from before it reads the user's devices and mute preferences. The two are written by
            // different mechanisms — the column can be stamped from the ambient tenant at commit, the
            // body only ever carries what the producer passed — so a reminder whose body lost its tenant
            // still lands in a correctly-stamped row and is then delivered against the wrong slice.
            var envelopeTenant = JsonDocument.Parse(reminder.Body).RootElement.GetProperty("tenantId");
            Assert.Equal(
                expectedTenant,
                envelopeTenant.ValueKind == JsonValueKind.Null ? null : envelopeTenant.GetString());
        }
    }

    /// <summary>
    /// The read half of the same property: a system sweep that forgot <c>IgnoreQueryFilters</c> sees
    /// only the ambient tenant's rows, which in a JWT-less job is the NULL-tenant slice — so the two
    /// tenanted orders would be silently skipped rather than fail.
    /// </summary>
    [Fact]
    public async Task A_Tenanted_Order_Is_Not_Invisible_To_A_Sweep_Running_Without_A_Jwt()
    {
        await SeedDueOrderAsync("pre-visible", "user-tenant-a", TenantA);

        var response = await RunSweepAsync();

        Assert.Equal(1, response.Considered);
        Assert.Equal(1, response.RemindersSent);

        _tenantProvider.SetTenantOverride(TenantA);
        await using var ctx = NewContext();
        Assert.NotNull(
            (await ctx.Orders.FirstAsync(o => o.Id == "ord-pre-visible")).PreCleaningReminderSentAt);
    }

    /// <summary>
    /// The shape CLAUDE.md names as the reference for a tenant-sweeping system job, asserted as an
    /// invariant of the unit of work rather than left to inspection: at every commit, the ambient tenant
    /// is the tenant of the rows that commit resolves. Both ways of getting it wrong — never setting the
    /// override, and deferring to one commit at the end — leave a commit whose ambient tenant does not
    /// match its own rows, which is exactly how "every group stamped with the last tenant processed"
    /// happens. Today both durable children carry an explicit tenant from the producer, so neither
    /// mistake is visible in the rows; this is what keeps that a redundancy rather than a coincidence.
    /// </summary>
    [Fact]
    public async Task Every_Commit_Runs_Under_The_Tenant_Of_The_Rows_It_Commits()
    {
        await SeedDueOrderAsync("pre-c1", "user-tenant-a", TenantA);
        await SeedDueOrderAsync("pre-c2", "user-tenant-b", TenantB);
        await SeedDueOrderAsync("pre-c3", "user-legacy", tenantId: null);

        _tenantProvider.ClearTenantOverride();
        await using var ctx = NewContext();
        var recorder = new TenantRecordingUnitOfWork(ctx, _tenantProvider);
        var handler = new SendPreCleaningReminders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            recorder,
            NullLogger<SendPreCleaningReminders.Handler>.Instance);

        var result = await handler.Handle(new SendPreCleaningReminders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.RemindersSent);
        Assert.Equal(3, recorder.Commits.Count);
        Assert.All(recorder.Commits, commit => Assert.All(
            commit.OrderTenants,
            orderTenant => Assert.Equal(commit.AmbientTenant, orderTenant)));
    }

    private sealed class TenantRecordingUnitOfWork(CleansiaDbContext inner, ITenantProvider tenantProvider)
        : IUnitOfWork
    {
        public List<(string? AmbientTenant, List<string?> OrderTenants)> Commits { get; } = [];

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commits.Add((
                tenantProvider.GetCurrentTenantId(),
                inner.ChangeTracker.Entries<Order>()
                    .Where(e => e.State == EntityState.Modified)
                    .Select(e => e.Entity.TenantId)
                    .ToList()));

            await inner.CommitAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            inner.BeginTransactionAsync(cancellationToken);

        public void Rollback() => inner.Rollback();

        public void Dispose() { }
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
