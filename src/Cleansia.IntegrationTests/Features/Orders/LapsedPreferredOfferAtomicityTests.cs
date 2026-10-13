using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;
using Xunit.Abstractions;

namespace Cleansia.IntegrationTests.Features.Orders;

[Collection("PostgresCollection")]
public sealed class LapsedPreferredOfferAtomicityTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    : BaseIntegrationTest(fixture)
{
    private DateTime _now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
    private DateTime _seedNow;

    [Fact]
    public async Task A_Failed_Account_Notice_Leaves_The_Direct_Notifiers_Local_Stamp_Absent()
    {
        await ResetAndSeedAsync(65, false);
        var fault = new RefuseRecipientRead(1);
        await using var context = NewContext(new ScopeTenantProvider(), fault);
        var order = await context.Orders.IgnoreQueryFilters().OrderBy(o => o.Id).FirstAsync();
        var producer = Producer(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PreferredOfferClosedNotifier.NotifyPreferredOfferClosedAsync(order, producer, _now, CancellationToken.None));
        await SnapshotAsync("direct-notifier-failure");
        output.WriteLine("Local lapse stamp: " + order.PreferredOfferLapseNotifiedAt?.ToString("O"));
        Assert.Null(order.PreferredOfferLapseNotifiedAt);
        Assert.Empty(context.ChangeTracker.Entries<Cleansia.Core.Domain.Outbox.OutboxMessage>());
        Assert.Empty(context.ChangeTracker.Entries<UserNotification>());
    }

    [Fact]
    public async Task A_Second_Recipient_Failure_Refuses_The_Companys_Unsaved_Receipts()
    {
        await ResetAndSeedAsync(65, false);
        var fault = new RefuseRecipientRead(2);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("company-failure");
        Assert.Equal(2, fault.Recipients.Count);
        Assert.Equal("Recipient company unavailable", Assert.IsType<InvalidOperationException>(error).Message);
        await AssertStateAsync([], null);
        _now = _now.AddMinutes(5);
        Assert.Equal(2, (await RunAsync()).NotifiedCount);
        Assert.Equal(0, (await RunAsync()).NotifiedCount);
        await SnapshotAsync("next-five-minute-tick");
        await AssertStateAsync(fault.Recipients.ToArray(), _now);
    }

    [Fact]
    public async Task An_Earlier_Company_Stays_Durable_When_A_Later_Companys_Second_Recipient_Fails()
    {
        await ResetAndSeedAsync(65, true);
        var fault = new RefuseRecipientRead(4);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("later-company-failure");
        Assert.Equal(4, fault.Recipients.Count);
        Assert.IsType<InvalidOperationException>(error);
        await using var read = NewContext();
        var owners = await read.Orders.IgnoreQueryFilters().ToDictionaryAsync(o => o.UserId!, o => o.TenantId);
        Assert.Equal(owners[fault.Recipients[0]], owners[fault.Recipients[1]]);
        Assert.Equal(owners[fault.Recipients[2]], owners[fault.Recipients[3]]);
        Assert.NotEqual(owners[fault.Recipients[0]], owners[fault.Recipients[2]]);
        await AssertStateAsync(fault.Recipients.Take(2).ToArray(), _now);
        var earlierStamp = _now;
        _now = _now.AddMinutes(5);
        Assert.Equal(2, (await RunAsync()).NotifiedCount);
        Assert.Equal(0, (await RunAsync()).NotifiedCount);
        await SnapshotAsync("later-company-retry");
        await AssertStateAsync(fault.Recipients.ToArray(), null);
        await using var verify = NewContext();
        Assert.All(await verify.Orders.IgnoreQueryFilters().ToListAsync(), o =>
            Assert.Equal(fault.Recipients.Take(2).Contains(o.UserId!) ? earlierStamp : _now, o.PreferredOfferLapseNotifiedAt));
    }

    [Fact]
    public async Task Same_Round_Producers_Collapse_And_A_Later_Round_Has_A_Different_Subject()
    {
        await ResetAndSeedAsync(65, false);
        await using (var direct = NewContext())
        {
            var order = await direct.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == "lapse-notice-order-0");
            var producer = Producer(direct);
            await PreferredOfferClosedNotifier.NotifyPreferredOfferClosedAsync(order, producer, _now, CancellationToken.None);
            await PreferredOfferClosedNotifier.NotifyPreferredOfferClosedAsync(order, producer, _now, CancellationToken.None);
            Assert.Single(direct.ChangeTracker.Entries<Cleansia.Core.Domain.Outbox.OutboxMessage>());
            await direct.CommitAsync(CancellationToken.None);
        }
        Assert.Equal(1, (await RunAsync()).NotifiedCount);
        Assert.Equal(0, (await RunAsync()).NotifiedCount);
        await using (var next = NewContext())
        {
            var order = await next.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == "lapse-notice-order-0");
            order.GrantPreferredHold(order.PreferredEmployeeId!, _now.AddMinutes(1), _now, BookingPolicy.MaxPreferredOfferRounds);
            await next.CommitAsync(CancellationToken.None);
        }
        _now = _now.AddMinutes(5);
        Assert.Equal(1, (await RunAsync()).NotifiedCount);
        Assert.Equal(0, (await RunAsync()).NotifiedCount);
        await SnapshotAsync("second-round");
        await using var read = NewContext();
        var keys = await read.OutboxMessages.IgnoreQueryFilters().Select(m => m.MessageKey).OrderBy(k => k).ToListAsync();
        Assert.Equal(new[]
        {
            MessageKeys.Push("lapse-notice-user-0", NotificationEventCatalog.PreferredOfferClosed, "lapse-notice-order-0:1"),
            MessageKeys.Push("lapse-notice-user-0", NotificationEventCatalog.PreferredOfferClosed, "lapse-notice-order-0:2"),
            MessageKeys.Push("lapse-notice-user-1", NotificationEventCatalog.PreferredOfferClosed, "lapse-notice-order-1:1")
        }.Order(), keys);
        var roundTwo = await read.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == "lapse-notice-order-0");
        Assert.Equal(2, roundTwo.PreferredOfferRound);
        Assert.Equal("lapse-notice-employee-0", roundTwo.PreferredEmployeeId);
        Assert.Equal(_seedNow.AddMinutes(1), roundTwo.PreferredHoldUntilUtc);
        Assert.Equal(_now, roundTwo.PreferredOfferLapseNotifiedAt);
        Assert.Empty(await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Guests_Are_Stamped_Without_A_Notice_And_Excluded_From_The_Next_Tick()
    {
        await ResetAndSeedAsync(65, false, guestOnly: true);
        var noRecipient = new RefuseRecipientRead(1);
        Assert.Equal(2, (await RunAsync(noRecipient)).NotifiedCount);
        Assert.Empty(noRecipient.Recipients);
        _now = _now.AddMinutes(5);
        Assert.Equal(0, (await RunAsync()).NotifiedCount);
        await SnapshotAsync("guests");
        await AssertStateAsync([], _seedNow);
    }

    [Fact]
    public async Task Host_Stop_Escapes_Before_The_Next_Recipient_Effect()
    {
        await ResetAndSeedAsync(65, false);
        using var stop = new CancellationTokenSource();
        var fault = new RefuseRecipientRead(2, stop);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(stop.Token, fault));
        await SnapshotAsync("host-stop");
        await AssertStateAsync([], null);
        Assert.Equal(2, fault.Recipients.Count);
    }

    private async Task ResetAndSeedAsync(int minutes, bool twoCompanies, bool guestOnly = false)
    {
        await using (var connection = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await connection.OpenAsync();
            var reset = await Respawner.CreateAsync(connection, new RespawnerOptions
            { DbAdapter = DbAdapter.Postgres, SchemasToExclude = ["pg_catalog", "information_schema"] });
            await reset.ResetAsync(connection);
            await SeedTenantRegistryAsync(connection);
        }
        _seedNow = _now;
        await using var seed = NewContext();
        seed.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "lapse-notice-country";
        seed.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "lapse-notice-currency";
        seed.Currencies.Add(currency);
        for (var index = 0; index < (twoCompanies ? 4 : 2); index++)
        {
            var tenant = index < 2 ? TestTenants.Default : TestTenants.Second;
            var recipientTenant = tenant == TestTenants.Default ? TestTenants.Second : TestTenants.Default;
            var user = User.CreateWithPassword($"lapse-notice-{index}@cleansia.test", "Seed-Password-123", "Customer", "Notice");
            user.Id = $"lapse-notice-user-{index}";
            user.TenantId = recipientTenant;
            var cleaner = User.CreateWithPassword($"lapse-cleaner-{index}@cleansia.test", "Seed-Password-123", "Cleaner", "Notice");
            cleaner.Id = $"lapse-cleaner-user-{index}";
            cleaner.TenantId = tenant;
            var employee = Employee.CreateWithUser(cleaner);
            employee.Id = $"lapse-notice-employee-{index}";
            employee.TenantId = tenant;
            seed.Users.AddRange(user, cleaner);
            seed.Employees.Add(employee);
            var order = Order.Create("Customer Notice", user.Email, "+420000000000",
                Address.Create("Test St 1", "Brno", "60200", country.Id), 1, 1, _now.AddHours(24),
                PaymentType.Cash, 1000m, currency.Id, PaymentStatus.Pending, userId: guestOnly ? null : user.Id,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = $"lapse-notice-order-{index}";
            order.TenantId = tenant;
            order.CalculateRequiredEmployees(0);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            order.GrantPreferredHold(employee.Id, _now.AddMinutes(-1-index), _now.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
            seed.Orders.Add(order);
        }
        await seed.CommitAsync(CancellationToken.None);
    }

    private Task<NotifyLapsedPreferredOffers.Response> RunAsync(params IInterceptor[] interceptors) => RunAsync(CancellationToken.None, interceptors);

    private async Task<NotifyLapsedPreferredOffers.Response> RunAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var tenant = new ScopeTenantProvider();
        await using var context = NewContext(tenant, interceptors);
        var handler = new NotifyLapsedPreferredOffers.Handler(new OrderRepository(context),
            Producer(context), tenant, context,
            NullLogger<NotifyLapsedPreferredOffers.Handler>.Instance, new TickClock(_now));
        var command = new NotifyLapsedPreferredOffers.Command();
        var result = await new UnitOfWorkPipelineBehavior<NotifyLapsedPreferredOffers.Command,
            BusinessResult<NotifyLapsedPreferredOffers.Response>>(context)
            .Handle(command, ct => handler.Handle(command, ct), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static NotificationProducer Producer(CleansiaDbContext context) => new(new UserNotificationRepository(context), new OutboxPendingDispatch(context),
        new UserRepository(context), NullLogger<NotificationProducer>.Instance);

    private CleansiaDbContext NewContext() => NewContext(new ScopeTenantProvider());
    private CleansiaDbContext NewContext(ITenantProvider tenant, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), tenant);

    private async Task AssertStateAsync(string[] sentRecipients, DateTime? expectedStamp)
    {
        await using var read = NewContext();
        var orders = await read.Orders.IgnoreQueryFilters().ToListAsync();
        var users = await read.Users.IgnoreQueryFilters().ToDictionaryAsync(u => u.Id, u => u.TenantId);
        var intents = await read.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(sentRecipients.Length, intents.Count);
        Assert.Empty(await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        foreach (var order in orders)
        {
            var index = int.Parse(order.Id.Split('-')[^1]);
            Assert.Equal($"lapse-notice-employee-{index}", order.PreferredEmployeeId);
            Assert.Equal(_seedNow.AddMinutes(-1-index), order.PreferredHoldUntilUtc);
            Assert.Equal(1, order.PreferredOfferRound);
            if (order.UserId is null) { Assert.Equal(expectedStamp, order.PreferredOfferLapseNotifiedAt); continue; }
            Assert.NotEqual(order.TenantId, users[order.UserId]);
            if (!sentRecipients.Contains(order.UserId!)) { Assert.Null(order.PreferredOfferLapseNotifiedAt); continue; }
            Assert.NotNull(order.PreferredOfferLapseNotifiedAt);
            if (expectedStamp is not null) Assert.Equal(expectedStamp, order.PreferredOfferLapseNotifiedAt);
            var intent = Assert.Single(intents, n => n.MessageKey == MessageKeys.Push(order.UserId!, NotificationEventCatalog.PreferredOfferClosed, $"{order.Id}:{order.PreferredOfferRound}"));
            Assert.Equal(QueueNames.NotificationsDispatch, intent.QueueName);
            Assert.Equal(users[order.UserId!], intent.TenantId);
            using var body = JsonDocument.Parse(intent.Body);
            Assert.Equal(intent.MessageKey, body.RootElement.GetProperty("messageKey").GetString());
            Assert.Equal(intent.TenantId, body.RootElement.GetProperty("tenantId").GetString());
            var payload = body.RootElement.GetProperty("payload");
            Assert.Equal(intent.TenantId, payload.GetProperty("tenantId").GetString());
            Assert.Equal(order.UserId, payload.GetProperty("userId").GetString());
            Assert.Equal(NotificationEventCatalog.PreferredOfferClosed, payload.GetProperty("eventKey").GetString());
            Assert.Equal(order.Id, payload.GetProperty("args").GetProperty("orderId").GetString());
            Assert.Equal(order.DisplayOrderNumber, payload.GetProperty("args").GetProperty("orderNumber").GetString());
        }
    }

    private async Task SnapshotAsync(string phase)
    {
        await using var read = NewContext();
        var rows = new
        {
            Phase = phase,
            Orders = await read.Orders.IgnoreQueryFilters().OrderBy(o => o.Id)
                .Select(o => new { o.Id, o.TenantId, o.UserId, o.CurrentStatus, o.PaymentStatus, o.CleaningDateTime, o.PreferredEmployeeId, o.PreferredHoldUntilUtc, o.PreferredOfferRound, o.PreferredOfferLapseNotifiedAt }).ToListAsync(),
            Feed = await read.Set<UserNotification>().IgnoreQueryFilters().OrderBy(n => n.Id)
                .Select(n => new { n.Id, n.TenantId, n.UserId, n.EventKey, n.ArgsJson }).ToListAsync(),
            Outbox = await read.OutboxMessages.IgnoreQueryFilters().OrderBy(n => n.MessageKey)
                .Select(n => new { n.TenantId, n.QueueName, n.MessageKey, n.Body }).ToListAsync()
        };
        var json = JsonSerializer.Serialize(rows);
        output.WriteLine("Persisted rows: " + json);
        output.WriteLine("Persisted rows SHA256: " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
    }

    private sealed class RefuseRecipientRead(int failAt, CancellationTokenSource? stop = null) : DbCommandInterceptor
    {
        public List<string> Recipients { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.StartsWith("SELECT u.\"TenantId\"", StringComparison.Ordinal))
            {
                Recipients.Add(Assert.IsType<string>(Assert.Single(command.Parameters.Cast<DbParameter>()).Value));
                if (Recipients.Count == failAt)
                {
                    if (stop is not null) { stop.Cancel(); stop.Token.ThrowIfCancellationRequested(); }
                    throw new InvalidOperationException("Recipient company unavailable");
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TickClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class ScopeTenantProvider : ITenantProvider
    {
        private string? _tenant = TestTenants.Default;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenantId) => _tenant = tenantId;
        public void ClearTenantOverride() => _tenant = null;
    }
}
