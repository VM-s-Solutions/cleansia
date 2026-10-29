using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Moq;
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
public sealed class CleanerReminderAtomicityTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    : BaseIntegrationTest(fixture)
{
    private DateTime _now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
    private bool _soon;

    [Theory]
    [InlineData(120)]
    [InlineData(35)]
    public async Task A_Second_Seat_Recipient_Failure_Refuses_The_Companys_Unsaved_Reminders(int minutes)
    {
        await ResetAndSeedAsync(minutes, false);
        var fault = new RefuseRecipientRead(2);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("company-failure");
        Assert.Equal(2, fault.Recipients.Count);
        Assert.Equal("Recipient company unavailable", Assert.IsType<InvalidOperationException>(error).Message);
        await AssertStateAsync([], null);
        _now = _now.AddMinutes(5);
        var response = await RunAsync();
        Assert.Equal(_soon ? 2 : 0, response.SoonSent);
        Assert.Equal(_soon ? 0 : 2, response.NudgesSent);
        Assert.Equal(0, (await RunAsync()).SoonSent + (await RunAsync()).NudgesSent);
        await SnapshotAsync("next-five-minute-tick");
        await AssertStateAsync(fault.Recipients.ToArray(), _now);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(35)]
    public async Task An_Earlier_Company_Stays_Durable_When_A_Later_Companys_Second_Seat_Fails(int minutes)
    {
        await ResetAndSeedAsync(minutes, true);
        var fault = new RefuseRecipientRead(4);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("later-company-failure");
        Assert.Equal(4, fault.Recipients.Count);
        Assert.IsType<InvalidOperationException>(error);
        await using var read = NewContext();
        var owners = await read.Employees.IgnoreQueryFilters().ToDictionaryAsync(e => e.UserId, e => e.TenantId);
        Assert.Equal(owners[fault.Recipients[0]], owners[fault.Recipients[1]]);
        Assert.Equal(owners[fault.Recipients[2]], owners[fault.Recipients[3]]);
        Assert.NotEqual(owners[fault.Recipients[0]], owners[fault.Recipients[2]]);
        await AssertStateAsync(fault.Recipients.Take(2).ToArray(), _now);
        var earlierStamp = _now;
        _now = _now.AddMinutes(5);
        var response = await RunAsync();
        Assert.Equal(_soon ? 2 : 0, response.SoonSent);
        Assert.Equal(_soon ? 0 : 2, response.NudgesSent);
        var repeated = await RunAsync();
        Assert.Equal(0, repeated.SoonSent + repeated.NudgesSent);
        await SnapshotAsync("later-company-retry");
        await AssertStateAsync(fault.Recipients.ToArray(), null);
        await using var verify = NewContext();
        var seats = await verify.Set<OrderEmployee>().IgnoreQueryFilters().Include(a => a.Employee).ToListAsync();
        Assert.All(seats, a => Assert.Equal(fault.Recipients.Take(2).Contains(a.Employee!.UserId) ? earlierStamp : _now,
            _soon ? a.ReminderSoonSentAt : a.ReminderNotStartedSentAt));
    }

    [Theory]
    [InlineData(110)]
    [InlineData(25)]
    public async Task A_Failed_Lower_Edge_Attempt_Is_Outside_The_Window_Five_Minutes_Later(int minutes)
    {
        await ResetAndSeedAsync(minutes, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(new RefuseRecipientRead(2)));
        await AssertStateAsync([], null);
        _now = _now.AddMinutes(5);
        var response = await RunAsync();
        Assert.Equal(0, response.SoonSent + response.NudgesSent);
        await SnapshotAsync("missed-lower-edge");
        await AssertStateAsync([], null);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(35)]
    public async Task Host_Stop_Escapes_Before_The_Next_Seat_Recipient_Effect(int minutes)
    {
        await ResetAndSeedAsync(minutes, false);
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
        country.Id = "cleaner-notice-country";
        seed.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "cleaner-notice-currency";
        seed.Currencies.Add(currency);
        _soon = minutes >= 100;
        Order? order = null;
        for (var index = 0; index < (twoCompanies ? 4 : 2); index++)
        {
            var tenant = index < 2 ? TestTenants.Default : TestTenants.Second;
            var recipientTenant = tenant == TestTenants.Default ? TestTenants.Second : TestTenants.Default;
            var user = User.CreateWithPassword($"cleaner-notice-{index}@cleansia.test", "Seed-Password-123", "Cleaner", "Notice");
            user.Id = $"cleaner-notice-user-{index}";
            user.TenantId = recipientTenant;
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"cleaner-notice-employee-{index}";
            employee.TenantId = tenant;
            employee.UpdateContractStatus(ContractStatus.Approved);
            seed.Users.Add(user);
            seed.Employees.Add(employee);
            if (index % 2 == 0)
            {
                var customer = User.CreateWithPassword($"cleaner-customer-{index}@cleansia.test", "Seed-Password-123", "Customer", "Notice");
                customer.Id = $"cleaner-customer-{index}";
                customer.TenantId = tenant;
                seed.Users.Add(customer);
                order = Order.Create("Customer Notice", customer.Email, "+420000000000",
                    Address.Create("Test St 1", "Brno", "60200", country.Id), 1, 1, _now.AddMinutes(minutes),
                    PaymentType.Card, 1000m, currency.Id, PaymentStatus.Paid, userId: customer.Id,
                    cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                order.Id = $"cleaner-notice-order-{index/2}";
                order.TenantId = tenant;
                order.UpdateEstimatedTime(180).CalculateRequiredEmployees(0);
                var status = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
                status.TenantId = tenant;
                order.AddOrderStatus(status);
                seed.Orders.Add(order);
            }
            var assignment = OrderEmployee.Create(order!, employee);
            assignment.Id = $"cleaner-notice-seat-{index}";
            order!.AddAssignedEmployee(assignment);
        }
        await seed.CommitAsync(CancellationToken.None);
    }

    private Task<SendCleanerJobReminders.Response> RunAsync(params IInterceptor[] interceptors) => RunAsync(CancellationToken.None, interceptors);

    private async Task<SendCleanerJobReminders.Response> RunAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var tenant = new ScopeTenantProvider();
        await using var context = NewContext(tenant, interceptors);
        var pending = new OutboxPendingDispatch(context);
        var users = new UserRepository(context);
        var feed = new UserNotificationRepository(context);
        var handler = new SendCleanerJobReminders.Handler(new OrderRepository(context),
            new NotificationProducer(feed, pending, users, NullLogger<NotificationProducer>.Instance),
            new AdminNotifier(users, feed, Mock.Of<IAppConfigurationProvider>(), pending, NullLogger<AdminNotifier>.Instance),
            feed, tenant, context, NullLogger<SendCleanerJobReminders.Handler>.Instance, new TickClock(_now));
        var command = new SendCleanerJobReminders.Command();
        var result = await new UnitOfWorkPipelineBehavior<SendCleanerJobReminders.Command,
            BusinessResult<SendCleanerJobReminders.Response>>(context)
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
        var orders = await read.Orders.IgnoreQueryFilters().Include(o => o.AssignedEmployees).ThenInclude(a => a.Employee).ToListAsync();
        var users = await read.Users.IgnoreQueryFilters().ToDictionaryAsync(u => u.Id, u => u.TenantId);
        var intents = await read.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(sentRecipients.Length, intents.Count);
        Assert.Empty(await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        foreach (var order in orders)
        {
            Assert.Equal(2, order.RequiredEmployees);
            Assert.Equal(2, order.AssignedEmployees.Count);
            foreach (var seat in order.AssignedEmployees)
            {
                var recipient = seat.Employee!.UserId;
                Assert.NotEqual(order.TenantId, users[recipient]);
                var stamp = _soon ? seat.ReminderSoonSentAt : seat.ReminderNotStartedSentAt;
                Assert.Null(_soon ? seat.ReminderNotStartedSentAt : seat.ReminderSoonSentAt);
                if (!sentRecipients.Contains(recipient)) { Assert.Null(stamp); continue; }
                Assert.NotNull(stamp);
                if (expectedStamp is not null) Assert.Equal(expectedStamp, stamp);
                var eventKey = _soon ? NotificationEventCatalog.ReminderSoon : NotificationEventCatalog.ReminderNotStarted;
                var intent = Assert.Single(intents, n => n.MessageKey == MessageKeys.Push(recipient, eventKey, AssignmentNotificationSubject.For(order.Id, seat.Id)));
                Assert.Equal(QueueNames.NotificationsDispatch, intent.QueueName);
                Assert.Equal(users[recipient], intent.TenantId);
                using var body = JsonDocument.Parse(intent.Body);
                Assert.Equal(intent.MessageKey, body.RootElement.GetProperty("messageKey").GetString());
                Assert.Equal(intent.TenantId, body.RootElement.GetProperty("tenantId").GetString());
                var payload = body.RootElement.GetProperty("payload");
                Assert.Equal(intent.TenantId, payload.GetProperty("tenantId").GetString());
                Assert.Equal(recipient, payload.GetProperty("userId").GetString());
                Assert.Equal(eventKey, payload.GetProperty("eventKey").GetString());
                Assert.Equal(order.Id, payload.GetProperty("args").GetProperty("orderId").GetString());
                Assert.Equal(order.DisplayOrderNumber, payload.GetProperty("args").GetProperty("orderNumber").GetString());
            }
        }
    }

    private async Task SnapshotAsync(string phase)
    {
        await using var read = NewContext();
        var rows = new
        {
            Phase = phase,
            Orders = await read.Orders.IgnoreQueryFilters().OrderBy(o => o.Id)
                .Select(o => new { o.Id, o.TenantId, o.UserId, o.CurrentStatus, o.PaymentStatus, o.CleaningDateTime, o.RequiredEmployees }).ToListAsync(),
            Seats = await read.Set<OrderEmployee>().OrderBy(a => a.Id).Select(a => new { a.Id, a.OrderId, a.EmployeeId, a.ReminderSoonSentAt, a.ReminderNotStartedSentAt }).ToListAsync(),
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
