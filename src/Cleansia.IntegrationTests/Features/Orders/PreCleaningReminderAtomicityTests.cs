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
public sealed class PreCleaningReminderAtomicityTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    : BaseIntegrationTest(fixture)
{
    private DateTime _now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_Second_Recipient_Failure_Refuses_The_Companys_Unsaved_Reminders()
    {
        await ResetAndSeedAsync(65, false);
        var fault = new RefuseRecipientRead(2);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("company-failure");
        Assert.Equal(2, fault.Recipients.Count);
        Assert.Equal("Recipient company unavailable", Assert.IsType<InvalidOperationException>(error).Message);
        await AssertStateAsync([], null);
        _now = _now.AddMinutes(5);
        Assert.Equal(2, (await RunAsync()).RemindersSent);
        var sentAt = _now;
        _now = _now.AddMinutes(1);
        Assert.Equal(0, (await RunAsync()).RemindersSent);
        await SnapshotAsync("next-five-minute-tick");
        await AssertStateAsync(fault.Recipients.ToArray(), sentAt);
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
        Assert.Equal(2, (await RunAsync()).RemindersSent);
        Assert.Equal(0, (await RunAsync()).RemindersSent);
        await SnapshotAsync("later-company-retry");
        await AssertStateAsync(fault.Recipients.ToArray(), null);
        await using var verify = NewContext();
        var orders = await verify.Orders.IgnoreQueryFilters().ToListAsync();
        Assert.All(orders, o => Assert.Equal(fault.Recipients.Take(2).Contains(o.UserId!) ? earlierStamp : _now, o.PreCleaningReminderSentAt));
    }

    [Fact]
    public async Task A_Failed_55_Minute_Attempt_Is_Outside_The_Window_Five_Minutes_Later()
    {
        await ResetAndSeedAsync(55, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(new RefuseRecipientRead(2)));
        await AssertStateAsync([], null);
        _now = _now.AddMinutes(5);
        Assert.Equal(0, (await RunAsync()).RemindersSent);
        await SnapshotAsync("missed-lower-edge");
        await AssertStateAsync([], null);
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

    private async Task ResetAndSeedAsync(int minutes, bool twoCompanies)
    {
        await using (var connection = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await connection.OpenAsync();
            var reset = await Respawner.CreateAsync(connection, new RespawnerOptions
            { DbAdapter = DbAdapter.Postgres, SchemasToExclude = ["pg_catalog", "information_schema"] });
            await reset.ResetAsync(connection);
            await SeedTenantRegistryAsync(connection);
        }
        await using var seed = NewContext();
        seed.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "pre-notice-country";
        seed.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "pre-notice-currency";
        seed.Currencies.Add(currency);
        for (var index = 0; index < (twoCompanies ? 4 : 2); index++)
        {
            var tenant = index < 2 ? TestTenants.Default : TestTenants.Second;
            var recipientTenant = tenant == TestTenants.Default ? TestTenants.Second : TestTenants.Default;
            var user = User.CreateWithPassword($"pre-notice-{index}@cleansia.test", "Seed-Password-123", "Customer", "Notice");
            user.Id = $"pre-notice-user-{index}";
            user.TenantId = recipientTenant;
            var cleaner = User.CreateWithPassword($"pre-cleaner-{index}@cleansia.test", "Seed-Password-123", "Cleaner", "Notice");
            cleaner.Id = $"pre-cleaner-user-{index}";
            cleaner.TenantId = tenant;
            var employee = Employee.CreateWithUser(cleaner);
            employee.Id = $"pre-notice-employee-{index}";
            employee.TenantId = tenant;
            seed.Users.AddRange(user, cleaner);
            seed.Employees.Add(employee);
            var order = Order.Create("Customer Notice", user.Email, "+420000000000",
                Address.Create("Test St 1", "Brno", "60200", country.Id), 1, 1, _now.AddMinutes(minutes),
                PaymentType.Cash, 1000m, currency.Id, PaymentStatus.Pending, userId: user.Id,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = $"pre-notice-order-{index}";
            order.TenantId = tenant;
            order.CalculateRequiredEmployees(0);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
            var assignment = OrderEmployee.Create(order, employee);
            assignment.Id = $"pre-notice-seat-{index}";
            order.AddAssignedEmployee(assignment);
            seed.Orders.Add(order);
        }
        await seed.CommitAsync(CancellationToken.None);
    }

    private Task<SendPreCleaningReminders.Response> RunAsync(params IInterceptor[] interceptors) => RunAsync(CancellationToken.None, interceptors);

    private async Task<SendPreCleaningReminders.Response> RunAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var tenant = new ScopeTenantProvider();
        await using var context = NewContext(tenant, interceptors);
        var handler = new SendPreCleaningReminders.Handler(new OrderRepository(context),
            new NotificationProducer(new UserNotificationRepository(context), new OutboxPendingDispatch(context),
                new UserRepository(context), NullLogger<NotificationProducer>.Instance), tenant, context,
            NullLogger<SendPreCleaningReminders.Handler>.Instance, new TickClock(_now));
        var command = new SendPreCleaningReminders.Command();
        var result = await new UnitOfWorkPipelineBehavior<SendPreCleaningReminders.Command,
            BusinessResult<SendPreCleaningReminders.Response>>(context)
            .Handle(command, ct => handler.Handle(command, ct), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

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
            Assert.NotEqual(order.TenantId, users[order.UserId!]);
            if (!sentRecipients.Contains(order.UserId!)) { Assert.Null(order.PreCleaningReminderSentAt); continue; }
            Assert.NotNull(order.PreCleaningReminderSentAt);
            if (expectedStamp is not null) Assert.Equal(expectedStamp, order.PreCleaningReminderSentAt);
            var intent = Assert.Single(intents, n => n.MessageKey == MessageKeys.Push(order.UserId!, NotificationEventCatalog.OrderStartingSoon, order.Id));
            Assert.Equal(QueueNames.NotificationsDispatch, intent.QueueName);
            Assert.Equal(users[order.UserId!], intent.TenantId);
            using var body = JsonDocument.Parse(intent.Body);
            Assert.Equal(intent.MessageKey, body.RootElement.GetProperty("messageKey").GetString());
            Assert.Equal(intent.TenantId, body.RootElement.GetProperty("tenantId").GetString());
            var payload = body.RootElement.GetProperty("payload");
            Assert.Equal(intent.TenantId, payload.GetProperty("tenantId").GetString());
            Assert.Equal(order.UserId, payload.GetProperty("userId").GetString());
            Assert.Equal(NotificationEventCatalog.OrderStartingSoon, payload.GetProperty("eventKey").GetString());
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
                .Select(o => new { o.Id, o.TenantId, o.UserId, o.CurrentStatus, o.PaymentStatus, o.CleaningDateTime, o.PreCleaningReminderSentAt }).ToListAsync(),
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
