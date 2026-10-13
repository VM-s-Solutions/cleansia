using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Outbox;
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

namespace Cleansia.IntegrationTests.Features.Memberships;

[Collection("PostgresCollection")]
public sealed class MembershipNotificationAtomicityTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    : BaseIntegrationTest(fixture)
{
    private DateTime _now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Recipient_Failure_Refuses_The_Whole_Lifecycle_Tick(bool cancellation)
    {
        await ResetAsync();
        await SeedAsync(cancellation, cancellation ? 1.5 : 3);
        var fault = new AtSecondRecipientRead((_, _) => throw new InvalidOperationException("Recipient company unavailable"));

        var error = await Record.ExceptionAsync(() => RunAsync(fault));
        await SnapshotAsync("recipient-failure", cancellation);

        Assert.Equal(2, fault.Recipients.Count);
        Assert.Equal(2, fault.Recipients.Distinct().Count());
        Assert.Equal("Recipient company unavailable", Assert.IsType<InvalidOperationException>(error).Message);
        await AssertUnsentAsync();
        _now = _now.AddDays(1);
        await AssertRetryAsync(cancellation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Webhook_Xmin_Conflict_Refuses_Every_Staged_Lifecycle_Notice(bool cancellation)
    {
        await ResetAsync();
        await SeedAsync(cancellation, cancellation ? 1.5 : 3);
        var race = new AtSecondRecipientRead(async (userId, _) =>
        {
            await using var webhook = NewContext();
            var membership = await webhook.UserMemberships.IgnoreQueryFilters().SingleAsync(m => m.UserId == userId);
            membership.UpdateFromStripeWebhook("active", membership.CurrentPeriodStart.AddHours(1), membership.CurrentPeriodEnd, null);
            await webhook.CommitAsync(CancellationToken.None);
        });

        var error = await Record.ExceptionAsync(() => RunAsync(race));
        await SnapshotAsync("xmin-conflict", cancellation);

        Assert.Equal(2, race.Recipients.Count);
        Assert.IsType<DbUpdateConcurrencyException>(error);
        await AssertUnsentAsync();
        await using var read = NewContext();
        Assert.Equal(_now.AddDays(-10).AddHours(1),
            (await read.UserMemberships.IgnoreQueryFilters().SingleAsync(m => m.UserId == race.Recipients[1])).CurrentPeriodStart);
        _now = _now.AddDays(1);
        await AssertRetryAsync(cancellation);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 0.5)]
    public async Task A_Failed_Last_Daily_Attempt_Can_Leave_The_Current_Window(bool cancellation, double endDays)
    {
        await ResetAsync();
        await SeedAsync(cancellation, endDays);
        var fault = new AtSecondRecipientRead((_, _) => throw new InvalidOperationException("Recipient company unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(fault));
        await AssertUnsentAsync();
        _now = _now.AddDays(1);
        Assert.Equal(new SendMembershipLifecycleNotifications.Response(0, 0), await RunAsync());
        await SnapshotAsync("missed-next-day", cancellation);
        await AssertUnsentAsync();
    }

    [Fact]
    public async Task Only_Active_Memberships_Are_Selected_And_Domain_Changes_Rearm_Their_Stamps()
    {
        await ResetAsync();
        await SeedAsync(false, 3);
        await using (var change = NewContext())
        {
            var memberships = await change.UserMemberships.IgnoreQueryFilters().OrderBy(m => m.Id).ToListAsync();
            memberships[0].UpdateFromStripeWebhook("past_due", memberships[0].CurrentPeriodStart, memberships[0].CurrentPeriodEnd, null);
            memberships[1].UpdateFromStripeWebhook("canceled", memberships[1].CurrentPeriodStart, memberships[1].CurrentPeriodEnd, null);
            await change.CommitAsync(CancellationToken.None);
        }
        Assert.Equal(new SendMembershipLifecycleNotifications.Response(0, 0), await RunAsync());
        await AssertUnsentAsync();
        await using var rearm = NewContext();
        var member = await rearm.UserMemberships.IgnoreQueryFilters().FirstAsync();
        member.MarkRenewalReminderSent(_now).MarkCancellationReminderSent(_now);
        member.MarkRenewalReminderSent(_now.AddHours(1)).MarkCancellationReminderSent(_now.AddHours(1));
        Assert.Equal(_now, member.RenewalReminderSentAt);
        Assert.Equal(_now, member.CancellationReminderSentAt);
        member.UpdateFromStripeWebhook("active", member.CurrentPeriodStart, member.CurrentPeriodEnd.AddMonths(1), null);
        Assert.Null(member.RenewalReminderSentAt);
        member.ApplyPlanSwap(member.MembershipPlanId, member.CurrentPeriodStart, member.CurrentPeriodEnd);
        Assert.Null(member.RenewalReminderSentAt);
        Assert.Null(member.CancellationReminderSentAt);
        Assert.Null(member.CancelledAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Host_Stop_Escapes_Before_The_Second_Recipient_Effect(bool cancellation)
    {
        await ResetAsync();
        await SeedAsync(cancellation, cancellation ? 1.5 : 3);
        using var stop = new CancellationTokenSource();
        var fault = new AtSecondRecipientRead((_, _) => { stop.Cancel(); stop.Token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(stop.Token, fault));
        await SnapshotAsync("host-stop", cancellation);
        await AssertUnsentAsync();
        Assert.Equal(2, fault.Recipients.Count);
    }

    private async Task AssertRetryAsync(bool cancellation)
    {
        Assert.Equal(cancellation ? new SendMembershipLifecycleNotifications.Response(0, 2) : new(2, 0), await RunAsync());
        Assert.Equal(new SendMembershipLifecycleNotifications.Response(0, 0), await RunAsync());
        await SnapshotAsync("next-daily-tick", cancellation);
        await using var read = NewContext();
        var memberships = await read.UserMemberships.IgnoreQueryFilters().OrderBy(m => m.Id).ToListAsync();
        var users = await read.Users.IgnoreQueryFilters().ToDictionaryAsync(u => u.Id, u => u.TenantId);
        var feed = await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
        var outbox = await read.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, feed.Count);
        Assert.Equal(2, outbox.Count);
        var eventKey = cancellation ? NotificationEventCatalog.MembershipCancellationEffective : NotificationEventCatalog.MembershipExpiringSoon;
        foreach (var membership in memberships)
        {
            Assert.NotEqual(membership.TenantId, users[membership.UserId]);
            Assert.Equal(_now, cancellation ? membership.CancellationReminderSentAt : membership.RenewalReminderSentAt);
            Assert.Null(cancellation ? membership.RenewalReminderSentAt : membership.CancellationReminderSentAt);
            var notice = Assert.Single(feed, n => n.UserId == membership.UserId);
            Assert.Equal(users[membership.UserId], notice.TenantId);
            Assert.Equal(eventKey, notice.EventKey);
            Assert.Equal("{}", notice.ArgsJson);
            var intent = Assert.Single(outbox, n => n.MessageKey == MessageKeys.Push(membership.UserId, eventKey, membership.Id));
            Assert.Equal(QueueNames.NotificationsDispatch, intent.QueueName);
            Assert.Equal(users[membership.UserId], intent.TenantId);
            using var body = JsonDocument.Parse(intent.Body);
            Assert.Equal(intent.MessageKey, body.RootElement.GetProperty("messageKey").GetString());
            Assert.Equal(intent.TenantId, body.RootElement.GetProperty("tenantId").GetString());
            var payload = body.RootElement.GetProperty("payload");
            Assert.Equal(intent.TenantId, payload.GetProperty("tenantId").GetString());
            Assert.Equal(membership.UserId, payload.GetProperty("userId").GetString());
            Assert.Equal(eventKey, payload.GetProperty("eventKey").GetString());
            Assert.Empty(payload.GetProperty("args").EnumerateObject());
        }
    }

    private async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        var reset = await Respawner.CreateAsync(connection, new RespawnerOptions
        { DbAdapter = DbAdapter.Postgres, SchemasToExclude = ["pg_catalog", "information_schema"] });
        await reset.ResetAsync(connection);
        await SeedTenantRegistryAsync(connection);
    }

    private async Task SeedAsync(bool cancellation, double endDays)
    {
        await using var seed = NewContext();
        seed.Languages.Add(Language.Create("en", "English"));
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = "membership-notice-czk";
        seed.Currencies.Add(currency);
        var plan = MembershipPlan.Create("NOTICE_MONTHLY", "Monthly", 5m, true);
        plan.Id = "membership-notice-plan";
        seed.MembershipPlans.Add(plan);
        for (var index = 0; index < 2; index++)
        {
            var user = User.CreateWithPassword($"membership-notice-{index}@cleansia.test", "Seed-Password-123", "Member", "Notice");
            user.Id = $"membership-notice-user-{index}";
            user.TenantId = index == 0 ? TestTenants.Second : TestTenants.Default;
            seed.Users.Add(user);
            var membership = UserMembership.Create(user.Id, plan.Id, currency.Id, $"sub_notice_{index}", _now.AddDays(-10), _now.AddDays(endDays));
            membership.Id = $"membership-notice-{index}";
            membership.TenantId = index == 0 ? TestTenants.Default : TestTenants.Second;
            if (cancellation) membership.MarkCancellationRequested();
            seed.UserMemberships.Add(membership);
        }
        await seed.CommitAsync(CancellationToken.None);
    }

    private Task<SendMembershipLifecycleNotifications.Response> RunAsync(params IInterceptor[] interceptors) => RunAsync(CancellationToken.None, interceptors);

    private async Task<SendMembershipLifecycleNotifications.Response> RunAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        await using var context = NewContext(interceptors);
        var handler = new SendMembershipLifecycleNotifications.Handler(new UserMembershipRepository(context),
            new NotificationProducer(new UserNotificationRepository(context), new OutboxPendingDispatch(context),
                new UserRepository(context), NullLogger<NotificationProducer>.Instance),
            new TickClock(_now));
        var command = new SendMembershipLifecycleNotifications.Command();
        var result = await new UnitOfWorkPipelineBehavior<SendMembershipLifecycleNotifications.Command,
            BusinessResult<SendMembershipLifecycleNotifications.Response>>(context)
            .Handle(command, ct => handler.Handle(command, ct), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider());

    private async Task AssertUnsentAsync()
    {
        await using var read = NewContext();
        var memberships = await read.UserMemberships.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, m => { Assert.Null(m.RenewalReminderSentAt); Assert.Null(m.CancellationReminderSentAt); });
        Assert.Empty(await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await read.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    private async Task SnapshotAsync(string phase, bool cancellation)
    {
        await using var read = NewContext();
        var rows = new
        {
            Phase = phase, Cancellation = cancellation,
            Memberships = await read.UserMemberships.IgnoreQueryFilters().OrderBy(m => m.Id)
                .Select(m => new { m.Id, m.TenantId, m.UserId, m.Status, m.CurrentPeriodStart, m.CurrentPeriodEnd, m.RenewalReminderSentAt, m.CancellationReminderSentAt }).ToListAsync(),
            Feed = await read.Set<UserNotification>().IgnoreQueryFilters().OrderBy(n => n.Id)
                .Select(n => new { n.Id, n.TenantId, n.UserId, n.EventKey, n.ArgsJson }).ToListAsync(),
            Outbox = await read.OutboxMessages.IgnoreQueryFilters().OrderBy(n => n.MessageKey)
                .Select(n => new { n.TenantId, n.QueueName, n.MessageKey, n.Body }).ToListAsync()
        };
        var json = JsonSerializer.Serialize(rows);
        output.WriteLine("Persisted rows: " + json);
        output.WriteLine("Persisted rows SHA256: " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
    }

    private sealed class AtSecondRecipientRead(Func<string, CleansiaDbContext, Task> act) : DbCommandInterceptor
    {
        public List<string> Recipients { get; } = [];
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.StartsWith("SELECT u.\"TenantId\"", StringComparison.Ordinal))
            {
                Recipients.Add(Assert.IsType<string>(Assert.Single(command.Parameters.Cast<DbParameter>()).Value));
                if (Recipients.Count == 2) await act(Recipients[^1], (CleansiaDbContext)data.Context!);
            }
            return result;
        }
    }

    private sealed class TickClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        private string? _tenant = TestTenants.Default;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenantId) => _tenant = tenantId;
        public void ClearTenantOverride() => _tenant = null;
    }
}
