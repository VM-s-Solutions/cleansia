using System.Text.Json;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Loyalty;

/// <summary>
/// Two loyalty writes for one customer at the same moment, on real Postgres. Each write reads the account,
/// then waits at its tier-threshold read until both have read, so both start from the same totals and one
/// of them must lose. The account's xmin token turns the loser's overwrite into a conflict, and the commit
/// replays the loser's points onto the row the winner committed: the account's total stays the sum of its
/// ledger, its tier is the one that total earns, and a completion is announced as a promotion only when the
/// tier it commits is above the one the winner left.
/// </summary>
[Collection("PostgresCollection")]
public class LoyaltyAccountConcurrentWriteTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string UserId = "01LOYALTYRACEUSER000000001";
    private const string CountryId = "01LOYALTYRACECOUNTRY000001";
    private const string CurrencyId = "01LOYALTYRACECURRENCY00001";
    private const string ActorId = "system";
    private const string OrderA = "01LOYALTYRACEORDERA0000001";
    private const string OrderB = "01LOYALTYRACEORDERB0000001";
    private const string OrderC = "01LOYALTYRACEORDERC0000001";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly LoyaltyTierThresholds Thresholds = new(Silver: 500, Gold: 2000, Platinum: 5000);

    [Fact]
    public async Task Two_Refund_Clawbacks_At_The_Same_Moment_Both_Take_Their_Points()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 1000m, 1000), (OrderB, 1000m, 1000));

        await RaceAsync(
            service => service.RevokeForRefundAsync(OrderA, 600m, "refund:race-a", ActorId, CancellationToken.None),
            service => service.RevokeForRefundAsync(OrderB, 500m, "refund:race-b", ActorId, CancellationToken.None));

        await AssertAccountAsync(points: 900, completedBookings: 2, LoyaltyTier.SilverMopper);
    }

    [Fact]
    public async Task A_Completion_Grant_And_A_Refund_Clawback_At_The_Same_Moment_Both_Land()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 1000m, 1000), (OrderC, 1500m, 0));

        await RaceAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.RevokeForRefundAsync(OrderA, 400m, "refund:race-a", ActorId, CancellationToken.None));

        await AssertAccountAsync(points: 2100, completedBookings: 2, LoyaltyTier.GoldPolisher);
    }

    /// <summary>
    /// A dispute settled in credit holds the customer's credit lock when its clawback commits. Losing the
    /// race must not release that lock before the replay saves, so the replay runs inside the same
    /// transaction, and the ledger row the failed save sent is not sent twice.
    /// </summary>
    [Fact]
    public async Task A_Clawback_Holding_The_Credit_Lock_Replays_Under_It_After_Losing_To_A_Grant()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 1000m, 1000), (OrderC, 1500m, 0));
        var read = NewSignal();
        var release = NewSignal();

        var clawback = Task.Run(async () =>
        {
            await using var ctx = NewContext();
            await new CreditAccountRepository(ctx).LockForUserAsync(UserId, CancellationToken.None);
            await NewService(ctx, () => Arrive(read, release))
                .RevokeForRefundAsync(OrderA, 400m, "dispute-settlement:race", ActorId, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        });
        await read.Task.WaitAsync(Timeout);

        await using (var ctx = NewContext())
        {
            await NewService(ctx, () => Task.CompletedTask).GrantForCompletedOrderAsync(OrderC, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        release.SetResult();
        await clawback.WaitAsync(Timeout);

        await AssertAccountAsync(points: 2100, completedBookings: 2, LoyaltyTier.GoldPolisher);
    }

    /// <summary>
    /// The grant read the thresholds before an admin lowered Gold, and the edit re-tiered the account and
    /// committed first. The replayed grant lands on the edit's row, so its tier is the one the thresholds
    /// now in force give the new total, not the one the grant's stale read gave.
    /// </summary>
    [Fact]
    public async Task A_Completion_Grant_That_Lost_To_A_Tier_Edit_Keeps_The_Tier_The_Edit_Gave()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 1600m, 1600), (OrderC, 50m, 0));
        var read = NewSignal();
        var release = NewSignal();

        var grant = Task.Run(() => WriteAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            () => Arrive(read, release)));
        await read.Task.WaitAsync(Timeout);

        await using (var ctx = NewContext())
        {
            await EditGoldThresholdAsync(ctx, 1500);
            await ctx.CommitAsync(CancellationToken.None);
        }

        release.SetResult();
        await grant.WaitAsync(Timeout);

        await AssertAccountAsync(points: 1650, completedBookings: 2, LoyaltyTier.GoldPolisher);
    }

    /// <summary>
    /// The grant read 450 points and its own 100 crossed Silver at 500, but a clawback of 200 committed
    /// first, so the grant lands at 250 + 100 = 350. The customer is told of no tier the account never held.
    /// </summary>
    [Fact]
    public async Task A_Completion_Grant_Replayed_Below_The_Threshold_It_Crossed_On_Its_Own_Read_Announces_Nothing()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 450m, 450), (OrderC, 100m, 0));

        await LoseToAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.RevokeForRefundAsync(OrderA, 200m, "refund:race-a", ActorId, CancellationToken.None));

        await AssertAccountAsync(points: 350, completedBookings: 2, LoyaltyTier.BronzeCleaner);
        Assert.Empty(await TiersAnnouncedAsync());
    }

    /// <summary>
    /// Neither grant crosses Silver on its own read of 450: one reaches 490, the other 480. The one that
    /// commits second lands at 490 + 30 = 520, and that replay is what crosses, so it is the one announced.
    /// </summary>
    [Fact]
    public async Task A_Completion_Grant_Whose_Replay_Crosses_A_Threshold_Announces_The_Tier()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 450m, 450), (OrderB, 40m, 0), (OrderC, 30m, 0));

        await LoseToAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.GrantForCompletedOrderAsync(OrderB, CancellationToken.None));

        await AssertAccountAsync(points: 520, completedBookings: 3, LoyaltyTier.SilverMopper);
        Assert.Equal(new[] { (OrderC, "SilverMopper") }, await TiersAnnouncedAsync());
    }

    /// <summary>
    /// The first grant takes 450 to 550 and is announced as Silver. The second read 450 and lands at
    /// 550 + 30 = 580: above the tier it read, but in the Silver the first already announced, so it says nothing.
    /// </summary>
    [Fact]
    public async Task A_Completion_Grant_Replayed_Into_The_Tier_The_Winner_Reached_Announces_Nothing_More()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 450m, 450), (OrderB, 100m, 0), (OrderC, 30m, 0));

        await LoseToAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.GrantForCompletedOrderAsync(OrderB, CancellationToken.None));

        await AssertAccountAsync(points: 580, completedBookings: 3, LoyaltyTier.SilverMopper);
        Assert.Equal(new[] { (OrderB, "SilverMopper") }, await TiersAnnouncedAsync());
    }

    /// <summary>
    /// The first grant takes 450 to 1950 and is announced as Silver. The second read 450 and saw itself reach
    /// Silver at 550, but lands at 1950 + 100 = 2050, past Gold at 2000, and is announced as the Gold it reached.
    /// </summary>
    [Fact]
    public async Task A_Completion_Grant_Replayed_Past_The_Next_Threshold_Announces_The_Tier_It_Landed_On()
    {
        await ResetAsync();
        await SeedAsync((OrderA, 450m, 450), (OrderB, 1500m, 0), (OrderC, 100m, 0));

        await LoseToAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.GrantForCompletedOrderAsync(OrderB, CancellationToken.None));

        await AssertAccountAsync(points: 2050, completedBookings: 3, LoyaltyTier.GoldPolisher);
        Assert.Equal(new[] { (OrderB, "SilverMopper"), (OrderC, "GoldPolisher") }, await TiersAnnouncedAsync());
    }

    /// <summary>
    /// A threshold edit re-tiers every account it moves in one commit, and each account another write
    /// committed to in the meantime conflicts. Every one of them is replayed, however many there are, and
    /// against the thresholds the edit itself is committing.
    /// </summary>
    [Fact]
    public async Task A_Tier_Edit_That_Lost_To_Writes_On_Many_Accounts_Lands_On_Every_One()
    {
        await ResetAsync();
        await SeedAsync();
        var userIds = await SeedAccountsAsync(count: 6, points: 1600);

        await using var edit = NewContext();
        await EditGoldThresholdAsync(edit, 1500);

        await using (var ctx = NewContext())
        {
            var accounts = new LoyaltyAccountRepository(ctx);
            foreach (var userId in userIds)
            {
                var account = await accounts.GetByUserIdAsync(userId, CancellationToken.None);
                account!.GrantPoints(
                    50, LoyaltyEarnSource.ManualGrant, null, ActorId, Thresholds, "race", $"race:{userId}");
            }

            await ctx.CommitAsync(CancellationToken.None);
        }

        await edit.CommitAsync(CancellationToken.None);

        foreach (var userId in userIds)
        {
            await AssertAccountAsync(points: 1650, completedBookings: 0, LoyaltyTier.GoldPolisher, userId);
        }
    }

    /// <summary>
    /// A customer with no account yet: their first order completes while an administrator grants them 50
    /// points. Both miss the account; the grant waits for the completion to commit and lands on the row it
    /// created, where it used to insert a second one and fail the completion on IX_LoyaltyAccounts_UserId.
    /// </summary>
    [Fact]
    public async Task Two_First_Grants_For_A_Customer_With_No_Account_Land_On_One_Account()
    {
        await ResetAsync();
        await SeedWithoutAccountAsync((OrderC, 1500m, 0));

        await SerializedAsync(
            service => service.GrantForCompletedOrderAsync(OrderC, CancellationToken.None),
            service => service.GrantPointsManuallyAsync(
                UserId, 50, LoyaltyEarnSource.ManualGrant, null, ActorId, "goodwill", "grant:first-race",
                CancellationToken.None));

        await AssertAccountAsync(points: 1550, completedBookings: 1, LoyaltyTier.SilverMopper);
    }

    private static async Task EditGoldThresholdAsync(CleansiaDbContext ctx, int threshold)
    {
        var tierConfigs = new LoyaltyTierConfigRepository(ctx);
        var gold = await tierConfigs.GetByTierAsync(LoyaltyTier.GoldPolisher, CancellationToken.None);
        var result = await new UpdateTierConfig.Handler(
                tierConfigs,
                new LoyaltyAccountRepository(ctx),
                new TestUserSessionProvider(ActorId, "system@cleansia.test"))
            .Handle(
                new UpdateTierConfig.Command(
                    gold!.Id, threshold, gold.DiscountPercent, gold.MinimumOrderAmountForDiscount, gold.PerksJson),
                CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private async Task RaceAsync(Func<LoyaltyService, Task> first, Func<LoyaltyService, Task> second)
    {
        var firstRead = NewSignal();
        var secondRead = NewSignal();
        var release = NewSignal();

        var writes = new[]
        {
            Task.Run(() => WriteAsync(first, () => Arrive(firstRead, release))),
            Task.Run(() => WriteAsync(second, () => Arrive(secondRead, release))),
        };
        await Task.WhenAll(firstRead.Task, secondRead.Task).WaitAsync(Timeout);

        release.SetResult();
        await Task.WhenAll(writes).WaitAsync(Timeout);
    }

    // The loser reads the account and waits there while the winner commits, so the loser's save lands on
    // the winner's row.
    private async Task LoseToAsync(Func<LoyaltyService, Task> loser, Func<LoyaltyService, Task> winner)
    {
        var read = NewSignal();
        var release = NewSignal();

        var losing = Task.Run(() => WriteAsync(loser, () => Arrive(read, release)));
        await read.Task.WaitAsync(Timeout);

        await WriteAsync(winner, () => Task.CompletedTask);

        release.SetResult();
        await losing.WaitAsync(Timeout);
    }

    // The first write waits at its tier read holding whatever it has locked. The second starts then, and the
    // first is let go once the second is seen waiting on a lock or has finished without one.
    private async Task SerializedAsync(Func<LoyaltyService, Task> first, Func<LoyaltyService, Task> second)
    {
        var read = NewSignal();
        var release = NewSignal();

        var firstWrite = Task.Run(() => WriteAsync(first, () => Arrive(read, release)));
        await read.Task.WaitAsync(Timeout);

        var secondWrite = Task.Run(() => WriteAsync(second, () => Task.CompletedTask));
        try
        {
            await using var observer = new NpgsqlConnection(Fixture.GetConnectionString() + ";Pooling=false");
            await observer.OpenAsync();
            await using var waiting = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event_type = 'Lock'
                  AND query LIKE '%FOR NO KEY UPDATE%'
                """, observer);
            var deadline = DateTime.UtcNow.Add(Timeout);
            while (!secondWrite.IsCompleted
                && (long)(await waiting.ExecuteScalarAsync())! == 0
                && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
        finally
        {
            release.TrySetResult();
        }

        await Task.WhenAll(firstWrite, secondWrite).WaitAsync(Timeout);
    }

    // The service, then the commit the UnitOfWork pipeline runs after the handler.
    private async Task WriteAsync(Func<LoyaltyService, Task> write, Func<Task> atTierRead)
    {
        await using var ctx = NewContext();
        await write(NewService(ctx, atTierRead));
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static async Task Arrive(TaskCompletionSource read, TaskCompletionSource release)
    {
        read.TrySetResult();
        await release.Task;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static LoyaltyService NewService(CleansiaDbContext ctx, Func<Task> atTierRead)
    {
        var stored = new LoyaltyTierConfigRepository(ctx);
        var tierConfigs = new Mock<ILoyaltyTierConfigRepository>();
        tierConfigs
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken cancellationToken) =>
            {
                var configs = await stored.GetAllForTenantAsync(cancellationToken);
                await atTierRead();
                return configs;
            });

        return new LoyaltyService(
            new OrderRepository(ctx),
            new LoyaltyAccountRepository(ctx),
            tierConfigs.Object,
            new LoyaltyTransactionRepository(ctx),
            new CurrencyRepository(ctx),
            new RefundRepository(ctx),
            new CreditAccountRepository(ctx),
            new NotificationProducer(
                new UserNotificationRepository(ctx),
                new OutboxPendingDispatch(ctx),
                new UserRepository(ctx),
                NullLogger<NotificationProducer>.Instance),
            NullLogger<LoyaltyService>.Instance);
    }

    /// <summary>
    /// The tier each committed promotion notice names, by the order it is about. The push and the feed row
    /// are staged together, so they must name the same tiers.
    /// </summary>
    private async Task<IReadOnlyList<(string OrderId, string Tier)>> TiersAnnouncedAsync()
    {
        await using var ctx = NewContext();
        var pushKeyPrefix = $"push:{UserId}:{NotificationEventCatalog.LoyaltyTierUpgrade}:";
        var pushes = (await ctx.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.MessageKey.StartsWith(pushKeyPrefix))
                .Select(m => new { m.MessageKey, m.Body })
                .ToListAsync())
            .Select(m => (
                OrderId: m.MessageKey[pushKeyPrefix.Length..],
                Tier: JsonDocument.Parse(m.Body).RootElement
                    .GetProperty("payload").GetProperty("args").GetProperty("tier").GetString()!))
            .OrderBy(p => p.OrderId, StringComparer.Ordinal)
            .ToList();
        var feed = (await ctx.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking()
                .Where(n => n.UserId == UserId && n.EventKey == NotificationEventCatalog.LoyaltyTierUpgrade)
                .Select(n => n.ArgsJson)
                .ToListAsync())
            .Select(args => JsonDocument.Parse(args).RootElement.GetProperty("tier").GetString()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(pushes.Select(p => p.Tier).Order(StringComparer.Ordinal), feed);
        return pushes;
    }

    private async Task AssertAccountAsync(int points, int completedBookings, LoyaltyTier tier, string userId = UserId)
    {
        await using var ctx = NewContext();
        var account = await ctx.Set<LoyaltyAccount>().AsNoTracking().SingleAsync(a => a.UserId == userId);
        var ledger = await ctx.Set<LoyaltyTransaction>().AsNoTracking()
            .Where(t => t.LoyaltyAccountId == account.Id)
            .SumAsync(t => t.Points);
        var inForce = LoyaltyTierThresholds.From(await ctx.LoyaltyTierConfigs.AsNoTracking().ToListAsync());

        Assert.Equal(ledger, account.LifetimePoints);
        Assert.Equal(points, account.LifetimePoints);
        Assert.Equal(completedBookings, account.CompletedBookingsCount);
        Assert.Equal(tier, account.CurrentTier);
        Assert.Equal(inForce.ResolveTier(account.LifetimePoints), account.CurrentTier);
    }

    private async Task<IReadOnlyList<string>> SeedAccountsAsync(int count, int points)
    {
        await using var ctx = NewContext();
        var userIds = new List<string>();
        for (var i = 1; i <= count; i++)
        {
            var user = User.CreateWithPassword(
                $"loyalty-race-{i}@cleansia.test", "Seed-Password-123", "Loyalty", $"Race{i}");
            ctx.Users.Add(user);
            var account = LoyaltyAccount.Create(user.Id);
            account.GrantPoints(
                points, LoyaltyEarnSource.ManualGrant, null, ActorId, Thresholds, "seed", $"seed:{user.Id}");
            ctx.Add(account);
            userIds.Add(user.Id);
        }

        await ctx.CommitAsync(CancellationToken.None);
        return userIds;
    }

    private Task SeedAsync(params (string OrderId, decimal TotalPrice, int Earned)[] orders) =>
        SeedAsync(withAccount: true, orders);

    private Task SeedWithoutAccountAsync(params (string OrderId, decimal TotalPrice, int Earned)[] orders) =>
        SeedAsync(withAccount: false, orders);

    private async Task SeedAsync(bool withAccount, (string OrderId, decimal TotalPrice, int Earned)[] orders)
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetLoyaltyPointsDivisor(1m);
        ctx.Currencies.Add(currency);
        var user = User.CreateWithPassword("loyalty-race@cleansia.test", "Seed-Password-123", "Loyalty", "Race");
        user.Id = UserId;
        ctx.Users.Add(user);
        ctx.LoyaltyTierConfigs.AddRange(
            LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, Thresholds.Silver, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, Thresholds.Gold, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, Thresholds.Platinum, 0m, null, "[]"));

        var account = LoyaltyAccount.Create(UserId);
        foreach (var (orderId, totalPrice, earned) in orders)
        {
            var order = Order.Create("Loyalty Race", "loyalty-race@cleansia.test", "+420777123456",
                Address.Create("Testovaci 12", "Praha", "11000", CountryId), 2, 1,
                DateTime.UtcNow.AddDays(-1), PaymentType.Card, totalPrice, CurrencyId, PaymentStatus.Paid,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking, userId: UserId);
            order.Id = orderId;
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            order.CompleteOrder(60);
            ctx.Orders.Add(order);

            if (earned > 0)
            {
                account.GrantPoints(earned, LoyaltyEarnSource.OrderCompleted, orderId, ActorId, Thresholds);
            }
        }

        if (withAccount)
        {
            ctx.Add(account);
        }

        await ctx.CommitAsync(CancellationToken.None);
    }

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(ActorId, "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await SeedTenantRegistryAsync(conn);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
