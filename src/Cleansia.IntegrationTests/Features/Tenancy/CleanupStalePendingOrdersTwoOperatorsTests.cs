using System.Data.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D10, the sweep smoke: a job has no claim, derives the tenant of every row it writes from
/// the row it read, and commits inside that scope. Two operating companies, two stale checkouts, one
/// sweep — every child row written for the CZ order carries cleansia-cz and every one for the SK order
/// carries cleansia-sk. The <c>PreCleaningReminderTenantStampTests</c> shape, on the reference sweep.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CleanupStalePendingOrdersTwoOperatorsTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string CountryId = "country-cz-stale";
    private const string CurrencyId = "currency-czk-stale";

    private NpgsqlDataSource _dataSource = default!;
    private readonly MutableTenantProvider _tenantProvider = new();

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "stale_orders_two_operators_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await TestTenants.EnsureCreatedWithRegistryAsync(bootstrap);
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
        await DropOrderForeignKeysAsync(conn);
        await SeedCatalogAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).AddInterceptors(interceptors).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private async Task SeedCatalogAsync()
    {
        _tenantProvider.SetTenantOverride(TestTenants.Default);
        await using var ctx = NewContext();

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);
        ctx.Languages.Add(Language.Create("en", "English"));

        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    /// <summary>The Orders FKs are dropped so the fixture can name a customer without a full identity graph.</summary>
    private static async Task DropOrderForeignKeysAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND conrelid = '"Orders"'::regclass
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedStaleCheckoutAsync(string key, string userId, string tenantId)
    {
        await SeedAccountAsync(userId, tenantId);
        await SeedStaleOrderAsync(key, userId, tenantId);
    }

    private async Task SeedAccountAsync(string userId, string tenantId)
    {
        var customer = User.CreateWithPassword($"{userId}@cleansia.test", "Passw0rd!", "Stale", "Customer");
        customer.Id = userId;

        _tenantProvider.SetTenantOverride(tenantId);
        await using var ctx = NewContext();
        ctx.Users.Add(customer);
        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    private async Task SeedStaleOrderAsync(string key, string userId, string tenantId, decimal creditApplied = 0m)
    {
        var order = Order.Create(
            customerName: "Stale Customer",
            customerEmail: $"{key}@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Stale St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = $"ord-{key}";
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, "seed");
        }
        order.Created("seed", DateTimeOffset.UtcNow.AddHours(-2));

        _tenantProvider.SetTenantOverride(tenantId);
        await using var ctx = NewContext();
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    private async Task<CleanupStalePendingOrders.Response> RunSweepAsync(params IInterceptor[] interceptors)
    {
        _tenantProvider.ClearTenantOverride();
        await using var ctx = NewContext(interceptors);
        var handler = new CleanupStalePendingOrders.Handler(
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), NullLogger<NotificationProducer>.Instance),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            new OutboxPendingDispatch(ctx),
            _tenantProvider,
            ctx,
            NullLogger<CleanupStalePendingOrders.Handler>.Instance);

        var result = await handler.Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public async Task Each_Operators_Cancellation_Rows_Carry_Its_Own_Company_In_One_Pass()
    {
        await SeedStaleCheckoutAsync("cz", "user-cz", TestTenants.Default);
        await SeedStaleCheckoutAsync("sk", "user-sk", TestTenants.Second);

        _tenantProvider.ClearTenantOverride();
        await using (var ctx = NewContext())
        {
            var handler = new CleanupStalePendingOrders.Handler(
                new OrderRepository(ctx),
                new CreditAccountRepository(ctx),
                new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationProducer>.Instance),
                new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
                new OutboxPendingDispatch(ctx),
                _tenantProvider,
                ctx,
                NullLogger<CleanupStalePendingOrders.Handler>.Instance);

            var result = await handler.Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.CancelledCount);
        }

        _tenantProvider.ClearTenantOverride();
        await using var verify = NewContext();

        foreach (var (orderId, userId, expectedTenant) in new[]
                 {
                     ("ord-cz", "user-cz", TestTenants.Default),
                     ("ord-sk", "user-sk", TestTenants.Second),
                 })
        {
            var cancelled = await verify.Set<OrderStatusTrack>().IgnoreQueryFilters()
                .SingleAsync(t => t.OrderId == orderId && t.Status == OrderStatus.Cancelled);
            Assert.Equal(expectedTenant, cancelled.TenantId);

            var feed = await verify.Set<Cleansia.Core.Domain.Notifications.UserNotification>().IgnoreQueryFilters().SingleAsync(n => n.UserId == userId);
            Assert.Equal(expectedTenant, feed.TenantId);

            var outbox = await verify.OutboxMessages.IgnoreQueryFilters()
                .SingleAsync(m => m.MessageKey.Contains(userId));
            Assert.Equal(expectedTenant, outbox.TenantId);
        }

        Assert.Empty(await verify.Set<OrderStatusTrack>().IgnoreQueryFilters().Where(t => t.TenantId == null).ToListAsync());
    }

    /// <summary>
    /// Each notice follows its customer's account, never the order's operator: a CZ order for an SK
    /// account is told under the SK company, an account named twice is told twice, and an order whose
    /// account is gone is cancelled without one. Each operator reads its recipients' companies once.
    /// </summary>
    [Fact]
    public async Task Operators_Recipients_Elsewhere_Absent_And_Repeated_Are_Each_Told_Under_Their_Own_Company()
    {
        await SeedAccountAsync("user-sk", TestTenants.Second);
        await SeedAccountAsync("user-cz", TestTenants.Default);
        await SeedStaleOrderAsync("cz-1", "user-sk", TestTenants.Default);
        await SeedStaleOrderAsync("cz-2", "user-sk", TestTenants.Default);
        await SeedStaleOrderAsync("sk-1", "user-cz", TestTenants.Second);
        await SeedStaleOrderAsync("sk-2", "user-gone", TestTenants.Second);
        var usersReads = new UsersReadCounter();

        var result = await RunSweepAsync(usersReads);

        Assert.Equal(4, result.CancelledCount);
        await using var verify = NewContext();
        var cancelled = await verify.Set<OrderStatusTrack>().IgnoreQueryFilters()
            .Where(t => t.Status == OrderStatus.Cancelled)
            .OrderBy(t => t.OrderId)
            .Select(t => new { t.OrderId, t.TenantId })
            .ToListAsync();
        Assert.Equal(
            new[]
            {
                new { OrderId = "ord-cz-1", TenantId = (string?)TestTenants.Default },
                new { OrderId = "ord-cz-2", TenantId = (string?)TestTenants.Default },
                new { OrderId = "ord-sk-1", TenantId = (string?)TestTenants.Second },
                new { OrderId = "ord-sk-2", TenantId = (string?)TestTenants.Second },
            },
            cancelled);
        var feed = await verify.Set<UserNotification>().IgnoreQueryFilters()
            .OrderBy(n => n.UserId)
            .Select(n => new { n.UserId, n.TenantId })
            .ToListAsync();
        Assert.Equal(
            new[]
            {
                new { UserId = "user-cz", TenantId = (string?)TestTenants.Default },
                new { UserId = "user-sk", TenantId = (string?)TestTenants.Second },
                new { UserId = "user-sk", TenantId = (string?)TestTenants.Second },
            },
            feed);
        var pushes = await verify.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .OrderBy(m => m.MessageKey)
            .Select(m => new { m.MessageKey, m.TenantId })
            .ToListAsync();
        Assert.Equal(
            new[]
            {
                new { MessageKey = MessageKeys.Push("user-cz", NotificationEventCatalog.OrderCancelled, "ord-sk-1"), TenantId = (string?)TestTenants.Default },
                new { MessageKey = MessageKeys.Push("user-sk", NotificationEventCatalog.OrderCancelled, "ord-cz-1"), TenantId = (string?)TestTenants.Second },
                new { MessageKey = MessageKeys.Push("user-sk", NotificationEventCatalog.OrderCancelled, "ord-cz-2"), TenantId = (string?)TestTenants.Second },
            },
            pushes);
        Assert.Equal(2, usersReads.Count);
    }

    /// <summary>
    /// A credit return commits on its own, before the company's commit. So a company's notices are
    /// staged before any of its orders hands credit back: when the read of their recipients fails, the
    /// company's orders stay Pending with their credit still spent, the companies already done stay
    /// committed, and the next tick cancels the rest and returns the credit once.
    /// </summary>
    [Fact]
    public async Task A_Failed_Recipient_Read_Comes_Before_Its_Company_Returns_Any_Credit()
    {
        const string creditReturnKey = "credit-return:order-ended-unpaid:ord-sk-1";
        await SeedStaleCheckoutAsync("cz-1", "user-cz", TestTenants.Default);
        await SeedAccountAsync("user-credit", TestTenants.Second);
        await SeedStaleOrderAsync("sk-1", "user-credit", TestTenants.Second, creditApplied: 300m);
        await SeedStaleCheckoutAsync("sk-2", "user-faulted", TestTenants.Second);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunSweepAsync(new FailFirstUsersReadNaming("user-faulted")));

        await using (var afterFailure = NewContext())
        {
            var orders = await afterFailure.Orders.IgnoreQueryFilters()
                .OrderBy(o => o.Id)
                .Select(o => new { o.Id, o.PaymentStatus })
                .ToListAsync();
            Assert.Equal(
                new[]
                {
                    new { Id = "ord-cz-1", PaymentStatus = PaymentStatus.Failed },
                    new { Id = "ord-sk-1", PaymentStatus = PaymentStatus.Pending },
                    new { Id = "ord-sk-2", PaymentStatus = PaymentStatus.Pending },
                },
                orders);
            Assert.Equal("user-cz", Assert.Single(await afterFailure.Set<UserNotification>().IgnoreQueryFilters().ToListAsync()).UserId);
            Assert.False(await afterFailure.CreditTransactions.AnyAsync(t => t.IdempotencyKey == creditReturnKey));
            Assert.False(await afterFailure.CreditAccounts.IgnoreQueryFilters().AnyAsync(a => a.UserId == "user-credit"));
        }

        var rerun = await RunSweepAsync();

        Assert.Equal(2, rerun.CancelledCount);
        await using var verify = NewContext();
        Assert.Equal(300m, Assert.Single(await verify.CreditTransactions
            .Where(t => t.IdempotencyKey == creditReturnKey).ToListAsync()).Amount);
        var account = await verify.CreditAccounts.IgnoreQueryFilters().Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == "user-credit");
        Assert.Equal(300m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(
            new[] { "user-credit", "user-cz", "user-faulted" },
            await verify.Set<UserNotification>().IgnoreQueryFilters().OrderBy(n => n.UserId).Select(n => n.UserId).ToListAsync());
        Assert.Equal(3, await verify.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(m => m.QueueName == QueueNames.NotificationsDispatch));
    }

    private sealed class UsersReadCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal))
            {
                Count++;
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Fails the first read of Users whose parameters name the customer, whatever its shape.</summary>
    private sealed class FailFirstUsersReadNaming(string userId) : DbCommandInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_failed
                && command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(p => Names(p.Value)))
            {
                _failed = true;
                throw new InvalidOperationException($"Reading the company of {userId} failed.");
            }

            return ValueTask.FromResult(result);
        }

        private bool Names(object? value) => value switch
        {
            string id => id == userId,
            IEnumerable<string> ids => ids.Contains(userId),
            _ => false,
        };
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
