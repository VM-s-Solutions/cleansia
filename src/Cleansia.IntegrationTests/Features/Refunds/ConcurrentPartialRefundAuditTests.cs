using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

[Collection("PostgresCollection")]
public sealed class ConcurrentPartialRefundAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "ord-partial-audit-race";
    private const string ServiceId = "svc-partial-audit-race";
    private const string UserId = "user-partial-audit-race";
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly ConcurrentDictionary<string, decimal> _payouts = new();
    private readonly TaskCompletionSource _firstAtStripe = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _bothAtStripe = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stripeCalls;

    private static IUserSessionProvider Session() => new TestUserSessionProvider("admin-audit-race", "admin@cleansia.test",
        [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);
    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<CleansiaDbContext>()
        .UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(interceptors).Options, Session(), new FixedTenantProvider(TestTenants.Default));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_Concurrent_Identical_Partial_Refunds_Write_One_Success_Audit(bool alreadyPartlyRefunded)
    {
        await ResetAndSeedAsync(alreadyPartlyRefunded);
        ArrangeConcurrentStripe();

        var first = RunHandlerAsync();
        await _firstAtStripe.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = RunHandlerAsync();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => { Assert.True(r.IsSuccess, r.Error?.Message); Assert.Equal(600m, r.Value!.RefundAmount); });
        Assert.Equal(2, _stripeCalls);
        Assert.Equal(600m, Assert.Single(_payouts).Value);
        await using var read = NewContext();
        var refunds = await read.Refunds.Where(r => r.OrderId == OrderId && r.RefundKey != "earlier-partial-audit").ToListAsync();
        var refund = Assert.Single(refunds);
        Assert.Equal((RefundStatus.Succeeded, 600m), (refund.Status, refund.Amount));
        var audit = Assert.Single(await read.AdminActionAudits.Where(a => a.Action == "order.refund.partial" && a.Success).ToListAsync());
        using var snapshot = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(600m, snapshot.RootElement.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(refund.RefundKey, snapshot.RootElement.GetProperty("refundKey").GetString());
        var clawback = Assert.Single(await read.LoyaltyTransactions.Where(t => t.IdempotencyKey == refund.RefundKey).ToListAsync());
        Assert.Equal(alreadyPartlyRefunded ? -70 : -60, clawback.Points);

        var replay = await RunHandlerAsync();
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(600m, replay.Value!.RefundAmount);
        Assert.Equal(2, _stripeCalls);
        Assert.Equal(1, await read.AdminActionAudits.Where(a => a.Action == "order.refund.partial" && a.Success).CountAsync());
    }

    private void ArrangeConcurrentStripe()
    {
        _stripe.Setup(s => s.RefundPaymentIntentAsync("pi_partial_audit", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, decimal amount, string key, CancellationToken ct) =>
            {
                _payouts.TryAdd(key, amount);
                if (Interlocked.Increment(ref _stripeCalls) == 1) _firstAtStripe.SetResult();
                else _bothAtStripe.SetResult();
                await _bothAtStripe.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                return "re_test";
            });
    }

    [Fact]
    public async Task A_Concurrent_Audit_Check_Waits_For_The_First_Audits_Persistence_Transaction()
    {
        await ResetAndSeedAsync(partlyRefunded: false);
        ArrangeConcurrentStripe();
        var pause = new PauseFirstAuditRead();
        var first = RunHandlerAsync(pause);
        await _firstAtStripe.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = RunHandlerAsync(pause);
        try
        {
            await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var observer = new NpgsqlConnection(Fixture.GetConnectionString());
            await observer.OpenAsync();
            var waiting = false;
            for (var attempt = 0; attempt < 200 && !waiting; attempt++)
            {
                await using var command = new NpgsqlCommand("""
                    SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                        AND wait_event_type = 'Lock' AND query LIKE '%"Refunds"%' AND query LIKE '%FOR UPDATE%')
                    """, observer);
                waiting = (bool)(await command.ExecuteScalarAsync())!;
                if (!waiting) await Task.Delay(20);
            }
            Assert.True(waiting, "The second handler must wait for the first refund audit transaction.");
            await using var read = NewContext();
            Assert.Empty(await read.AdminActionAudits.ToListAsync());
        }
        finally
        {
            pause.Release.TrySetResult();
        }
        var results = await Task.WhenAll(first, second);
        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.Message));
        await using var verify = NewContext();
        Assert.Single(await verify.AdminActionAudits.Where(a => a.Success).ToListAsync());
    }

    private sealed class PauseFirstAuditRead : DbCommandInterceptor
    {
        private int _entered;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM \"AdminActionAudits\"", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _entered, 1, 0) == 0)
            {
                Started.SetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            return result;
        }
    }

    [Fact]
    public async Task A_Refused_Audit_Releases_The_Claim_And_A_Sequential_Retry_Records_It_Once()
    {
        await ResetAndSeedAsync(partlyRefunded: false);
        _stripe.Setup(s => s.RefundPaymentIntentAsync("pi_partial_audit", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _payouts.TryAdd(key, amount))
            .ReturnsAsync("re_test");
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunHandlerAsync(new RefuseAuditSave()));
        await using (var read = NewContext()) Assert.Empty(await read.AdminActionAudits.ToListAsync());
        var replay = await RunHandlerAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(600m, replay.Value!.RefundAmount);
        Assert.Single(_payouts);
        await using var verify = NewContext();
        Assert.Single(await verify.AdminActionAudits.Where(a => a.Success).ToListAsync());
        Assert.Single(await verify.LoyaltyTransactions.Where(t => t.Type == LoyaltyTransactionType.Revoke).ToListAsync());
    }

    private sealed class RefuseAuditSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AdminActionAudit>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Refused audit commit");
            return ValueTask.FromResult(result);
        }
    }

    private async Task<BusinessResult<IssuePartialRefund.Response>> RunHandlerAsync(params IInterceptor[] interceptors)
    {
        await using var ctx = NewContext(interceptors);
        var orders = new OrderRepository(ctx);
        var refunds = new RefundRepository(ctx);
        var credit = new CreditAccountRepository(ctx);
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var seam = new RefundService(refunds, orders, credit, factory.Object, Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(ctx), NullLogger<RefundService>.Instance);
        var loyalty = new LoyaltyService(orders, new LoyaltyAccountRepository(ctx), new LoyaltyTierConfigRepository(ctx),
            new LoyaltyTransactionRepository(ctx), new CurrencyRepository(ctx), refunds, credit,
            Mock.Of<INotificationProducer>(), NullLogger<LoyaltyService>.Instance);
        var auditContext = new AuditContext();
        var handler = new IssuePartialRefund.Handler(orders, refunds, new ExtraRepository(ctx), new CountryConfigurationRepository(ctx),
            seam, loyalty, Session(), auditContext, NullLogger<IssuePartialRefund.Handler>.Instance);
        var command = new IssuePartialRefund.Command(OrderId, [new IssuePartialRefund.RefundLineSelection(ServiceId, null)],
            RefundReason.ServiceNotRendered, null);
        var audience = new HostAudienceProvider(JwtAudiences.Admin);
        var audit = new AuditLogBehavior<IssuePartialRefund.Command, BusinessResult<IssuePartialRefund.Response>>(
            Session(), audience, auditContext, new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default)),
            Mock.Of<IAuditFailureSink>(), new AuditEntryFactory(Session(), new TestRequestMetadataProvider(), audience),
            NullLogger<AuditLogBehavior<IssuePartialRefund.Command, BusinessResult<IssuePartialRefund.Response>>>.Instance);
        return await new UnitOfWorkPipelineBehavior<IssuePartialRefund.Command, BusinessResult<IssuePartialRefund.Response>>(ctx)
            .Handle(command, ct => audit.Handle(command, token => handler.Handle(command, token), ct), CancellationToken.None);
    }

    private async Task ResetAndSeedAsync(bool partlyRefunded)
    {
        await using (var conn = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await conn.OpenAsync();
            var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions { DbAdapter = DbAdapter.Postgres,
                SchemasToExclude = ["pg_catalog", "information_schema"] });
            await respawner.ResetAsync(conn);
            await SeedTenantRegistryAsync(conn);
        }
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "country-partial-audit";
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "cur-partial-audit";
        ctx.Currencies.Add(currency);
        var user = User.CreateWithPassword("partial-audit@cleansia.test", "Password123!", "Audit", "Race");
        user.Id = UserId;
        ctx.Users.Add(user);
        var category = ServiceCategory.Create("audit-race", "Cleaning", "Audit fixture");
        ctx.ServiceCategories.Add(category);
        var selected = Service.Create(category.Id, "Selected", "Audit fixture", 60);
        selected.Id = ServiceId;
        var other = Service.Create(category.Id, "Other", "Audit fixture", 60);
        ctx.Services.AddRange(selected, other);
        var order = Order.Create("Audit Race", user.Email, "+420000000000", Address.Create("Test St 1", "Brno", "60200", country.Id),
            1, 1, DateTime.UtcNow.AddDays(-1), PaymentType.Card, 1000m, currency.Id,
            partlyRefunded ? PaymentStatus.PartiallyRefunded : PaymentStatus.Paid, userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AssignStripePaymentIntentId("pi_partial_audit");
        order.AddSelectedServices([OrderService.Create(order, selected, 600m, 0m, 600m), OrderService.Create(order, other, 400m, 0m, 400m)]);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.CompleteOrder(60);
        ctx.Orders.Add(order);
        var account = LoyaltyAccount.Create(UserId);
        account.TenantId = TestTenants.Default;
        account.GrantPoints(100, LoyaltyEarnSource.OrderCompleted, OrderId, "seed", new LoyaltyTierThresholds(1000, 2000, 3000));
        ctx.LoyaltyAccounts.Add(account);
        if (partlyRefunded) ctx.Refunds.Add(Refund.Create(OrderId, "earlier-partial-audit", 100m, "CZK",
            RefundReason.ServiceNotRendered, RefundSource.AppRefund).MarkSucceeded(null, DateTimeOffset.UtcNow));
        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider(string tenant) : ITenantProvider
    {
        private string? _tenant = tenant;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string value) => _tenant = value;
        public void ClearTenantOverride() => _tenant = null;
    }
}
