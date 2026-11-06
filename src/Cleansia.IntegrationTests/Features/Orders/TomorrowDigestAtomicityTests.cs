using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
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
public sealed class TomorrowDigestAtomicityTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    : BaseIntegrationTest(fixture)
{
    private DateTime _now = new(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc);
    private DateTime _seedNow;
    private TimeZoneInfo _zone = TimeZoneInfo.Utc;

    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public async Task A_Second_Cleaner_Recipient_Failure_Refuses_The_Companys_Unsaved_Digests(int localHour)
    {
        _now = _now.Date.AddHours(localHour);
        await ResetAndSeedAsync(false);
        var fault = new RefuseRecipientRead(2);
        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("company-failure");
        Assert.Equal(2, fault.Recipients.Count);
        Assert.Equal("Recipient company unavailable", Assert.IsType<InvalidOperationException>(error).Message);
        await AssertStateAsync([], null);
        _now = _now.AddHours(1);
        Assert.Equal(2, (await RunAsync()).DigestsSent);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await SnapshotAsync("next-hour-tick");
        await AssertStateAsync(fault.Recipients.ToArray(), _now);
    }

    [Fact]
    public async Task An_Earlier_Company_Stays_Durable_When_A_Later_Companys_Second_Cleaner_Fails()
    {
        await ResetAndSeedAsync(true);
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
        _now = _now.AddHours(1);
        Assert.Equal(2, (await RunAsync()).DigestsSent);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await SnapshotAsync("later-company-retry");
        await AssertStateAsync(fault.Recipients.ToArray(), null);
        await using var verify = NewContext();
        Assert.All(await verify.Employees.IgnoreQueryFilters().ToListAsync(), e =>
            Assert.Equal(new DateTimeOffset(fault.Recipients.Take(2).Contains(e.UserId) ? earlierStamp : _now), e.LastTomorrowDigestAt));
    }

    [Fact]
    public async Task A_Failed_20_Hour_Attempt_Is_Ineligible_At_21()
    {
        _now = _now.Date.AddHours(20);
        await ResetAndSeedAsync(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(new RefuseRecipientRead(2)));
        await AssertStateAsync([], null);
        _now = _now.AddHours(1);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await SnapshotAsync("missed-final-hour");
        await AssertStateAsync([], null);
    }

    [Fact]
    public async Task A_Zero_Count_Tick_Leaves_Cleaners_Eligible_For_Jobs_Taken_That_Evening()
    {
        await ResetAndSeedAsync(false, withJobs: false);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await AssertStateAsync([], null);
        await using (var take = NewContext())
        {
            var order = await take.Orders.IgnoreQueryFilters().SingleAsync();
            foreach (var employee in await take.Employees.IgnoreQueryFilters().ToListAsync())
                order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
            await take.CommitAsync(CancellationToken.None);
        }
        _now = _now.AddHours(1);
        Assert.Equal(2, (await RunAsync()).DigestsSent);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await SnapshotAsync("later-evening-take");
        await AssertStateAsync(["tomorrow-notice-user-0", "tomorrow-notice-user-1"], _now);
    }

    [Fact]
    public async Task Host_Stop_Escapes_Before_The_Next_Cleaner_Recipient_Effect()
    {
        await ResetAndSeedAsync(false);
        using var stop = new CancellationTokenSource();
        var fault = new RefuseRecipientRead(2, stop);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(stop.Token, fault));
        await SnapshotAsync("host-stop");
        await AssertStateAsync([], null);
        Assert.Equal(2, fault.Recipients.Count);
    }

    [Theory]
    [InlineData("Europe/Prague", 2026, 3, 28, 23, false)]
    [InlineData("Europe/Prague", 2026, 10, 24, 25, false)]
    [InlineData("America/Los_Angeles", 2026, 10, 13, 24, true)]
    [InlineData("Invalid/ConfiguredZone", 2026, 10, 13, 24, false)]
    public async Task Local_Tomorrow_Boundaries_And_Local_Date_Keys_Survive_Offsets_And_Dst(
        string zoneId, int year, int month, int day, int hours, bool utcDateDiffers)
    {
        var zone = TimeZoneResolution.Resolve(zoneId);
        _now = TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, 18, 0, 0, DateTimeKind.Unspecified), zone);
        await ResetAndSeedAsync(false, zoneId: zoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_now, zone);
        Assert.Equal(utcDateDiffers, localNow.Date != _now.Date);
        var start = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(1), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(2), zone);
        Assert.Equal(hours, (end - start).TotalHours);
        await using (var seed = NewContext())
        {
            var employees = await seed.Employees.IgnoreQueryFilters().ToListAsync();
            for (var index = 0; index < 3; index++)
            {
                var date = index == 0 ? start : index == 1 ? end : start.AddTicks(-10);
                var order = Order.Create("Boundary Customer", "boundary@cleansia.test", "+420000000000",
                    Address.Create("Test St 1", "Brno", "60200", "tomorrow-notice-country"), 1, 1, date,
                    PaymentType.Card, 1000m, "tomorrow-notice-currency", PaymentStatus.Paid,
                    cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                order.Id = $"tomorrow-boundary-{index}";
                order.TenantId = TestTenants.Default;
                order.UpdateEstimatedTime(180).CalculateRequiredEmployees(0);
                var status = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
                status.TenantId = TestTenants.Default;
                order.AddOrderStatus(status);
                foreach (var employee in employees) order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
                seed.Orders.Add(order);
            }
            await seed.CommitAsync(CancellationToken.None);
        }
        Assert.Equal(2, (await RunAsync()).DigestsSent);
        _now = _now.AddHours(1);
        Assert.Equal(0, (await RunAsync()).DigestsSent);
        await SnapshotAsync("timezone-boundaries");
        await AssertStateAsync(["tomorrow-notice-user-0", "tomorrow-notice-user-1"], _seedNow, expectedCount: 2);
    }

    private async Task ResetAndSeedAsync(bool twoCompanies, bool withJobs = true, string zoneId = "UTC")
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
        _zone = TimeZoneResolution.Resolve(zoneId);
        await using var seed = NewContext();
        seed.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "tomorrow-notice-country";
        seed.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "tomorrow-notice-currency";
        seed.Currencies.Add(currency);
        seed.CountryConfigurations.Add(CountryConfiguration.Create(country.Id, "CZK", "en", 0.21m, timeZoneId: zoneId));
        Order? order = null;
        for (var index = 0; index < (twoCompanies ? 4 : 2); index++)
        {
            var tenant = index < 2 ? TestTenants.Default : TestTenants.Second;
            var recipientTenant = tenant == TestTenants.Default ? TestTenants.Second : TestTenants.Default;
            var user = User.CreateWithPassword($"tomorrow-notice-{index}@cleansia.test", "Seed-Password-123", "Cleaner", "Notice");
            user.Id = $"tomorrow-notice-user-{index}";
            user.TenantId = recipientTenant;
            var employee = Employee.CreateWithUser(user);
            employee.Id = $"tomorrow-notice-employee-{index}";
            employee.TenantId = tenant;
            employee.UpdateContractStatus(ContractStatus.Approved);
            employee.AssignWorkCountry(country.Id);
            employee.MarkNewJobsDigestSent(new DateTimeOffset(_seedNow.AddDays(-2)));
            seed.Users.Add(user);
            seed.Employees.Add(employee);
            if (index % 2 == 0)
            {
                var customer = User.CreateWithPassword($"tomorrow-customer-{index}@cleansia.test", "Seed-Password-123", "Customer", "Notice");
                customer.Id = $"tomorrow-customer-{index}";
                customer.TenantId = tenant;
                seed.Users.Add(customer);
                order = Order.Create("Customer Notice", customer.Email, "+420000000000",
                    Address.Create("Test St 1", "Brno", "60200", country.Id), 1, 1, TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(_now, _zone).Date.AddDays(1).AddHours(12), _zone),
                    PaymentType.Card, 1000m, currency.Id, PaymentStatus.Paid, userId: customer.Id,
                    cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                order.Id = $"tomorrow-notice-order-{index/2}";
                order.TenantId = tenant;
                order.UpdateEstimatedTime(180).CalculateRequiredEmployees(0);
                var status = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
                status.TenantId = tenant;
                order.AddOrderStatus(status);
                seed.Orders.Add(order);
            }
            if (withJobs)
            {
                var assignment = OrderEmployee.Create(order!, employee);
                assignment.Id = $"tomorrow-notice-seat-{index}";
                order!.AddAssignedEmployee(assignment);
            }
        }
        await seed.CommitAsync(CancellationToken.None);
    }

    private Task<SendTomorrowJobDigest.Response> RunAsync(params IInterceptor[] interceptors) => RunAsync(CancellationToken.None, interceptors);

    private async Task<SendTomorrowJobDigest.Response> RunAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var tenant = new ScopeTenantProvider();
        await using var context = NewContext(tenant, interceptors);
        var pending = new OutboxPendingDispatch(context);
        var users = new UserRepository(context);
        var feed = new UserNotificationRepository(context);
        var handler = new SendTomorrowJobDigest.Handler(new EmployeeRepository(context), new OrderRepository(context),
            new CountryConfigurationRepository(context), new NotificationProducer(feed, pending, users, NullLogger<NotificationProducer>.Instance),
            tenant, context, NullLogger<SendTomorrowJobDigest.Handler>.Instance, new TickClock(_now));
        var command = new SendTomorrowJobDigest.Command();
        var result = await new UnitOfWorkPipelineBehavior<SendTomorrowJobDigest.Command,
            BusinessResult<SendTomorrowJobDigest.Response>>(context)
            .Handle(command, ct => handler.Handle(command, ct), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private CleansiaDbContext NewContext() => NewContext(new ScopeTenantProvider());
    private CleansiaDbContext NewContext(ITenantProvider tenant, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), tenant);

    private async Task AssertStateAsync(string[] sentRecipients, DateTime? expectedStamp, int expectedCount = 1)
    {
        await using var read = NewContext();
        var employees = await read.Employees.IgnoreQueryFilters().ToListAsync();
        var users = await read.Users.IgnoreQueryFilters().ToDictionaryAsync(u => u.Id, u => u.TenantId);
        var intents = await read.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        var notices = await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
        Assert.Equal(sentRecipients.Length, intents.Count);
        Assert.Equal(sentRecipients.Length, notices.Count);
        var subject = TimeZoneInfo.ConvertTimeFromUtc(_now, _zone).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var employee in employees)
        {
            Assert.NotEqual(employee.TenantId, users[employee.UserId]);
            Assert.Equal(new DateTimeOffset(_seedNow.AddDays(-2)), employee.LastNewJobsDigestAt);
            if (!sentRecipients.Contains(employee.UserId)) { Assert.Null(employee.LastTomorrowDigestAt); continue; }
            Assert.NotNull(employee.LastTomorrowDigestAt);
            if (expectedStamp is not null) Assert.Equal(new DateTimeOffset(expectedStamp.Value), employee.LastTomorrowDigestAt);
            var notice = Assert.Single(notices, n => n.UserId == employee.UserId);
            Assert.Equal(users[employee.UserId], notice.TenantId);
            Assert.Equal(NotificationEventCatalog.ReminderTomorrow, notice.EventKey);
            Assert.Equal(expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture), JsonSerializer.Deserialize<Dictionary<string,string>>(notice.ArgsJson)!["count"]);
            var intent = Assert.Single(intents, n => n.MessageKey == MessageKeys.Push(employee.UserId, NotificationEventCatalog.ReminderTomorrow, subject));
            Assert.Equal(QueueNames.NotificationsDispatch, intent.QueueName);
            Assert.Equal(users[employee.UserId], intent.TenantId);
            using var body = JsonDocument.Parse(intent.Body);
            Assert.Equal(intent.MessageKey, body.RootElement.GetProperty("messageKey").GetString());
            Assert.Equal(intent.TenantId, body.RootElement.GetProperty("tenantId").GetString());
            var payload = body.RootElement.GetProperty("payload");
            Assert.Equal(intent.TenantId, payload.GetProperty("tenantId").GetString());
            Assert.Equal(employee.UserId, payload.GetProperty("userId").GetString());
            Assert.Equal(NotificationEventCatalog.ReminderTomorrow, payload.GetProperty("eventKey").GetString());
            Assert.Equal(expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture), payload.GetProperty("args").GetProperty("count").GetString());
        }
    }

    private async Task SnapshotAsync(string phase)
    {
        await using var read = NewContext();
        var rows = new
        {
            Phase = phase, Zone = _zone.Id, ClockUtc = _now,
            Orders = await read.Orders.IgnoreQueryFilters().OrderBy(o => o.Id)
                .Select(o => new { o.Id, o.TenantId, o.UserId, o.CurrentStatus, o.PaymentStatus, o.CleaningDateTime, o.RequiredEmployees }).ToListAsync(),
            Employees = await read.Employees.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => new { e.Id, e.TenantId, e.UserId, e.LastTomorrowDigestAt, e.LastNewJobsDigestAt }).ToListAsync(),
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
