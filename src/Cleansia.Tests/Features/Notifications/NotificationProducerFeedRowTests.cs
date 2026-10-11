using System.Data.Common;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// The shared notify seam writes BOTH halves of a notification into the caller's scoped
/// <see cref="CleansiaDbContext"/> — the <see cref="UserNotification"/> feed row and the outbox push
/// row — so both commit atomically with the producing transaction, and neither exists on a rollback.
/// The feed row is delivery-independent (written whether or not any device/FCM/mute would let the
/// push out); the new-jobs digest collapses onto the user's single unread digest row; a non-feed
/// event (sitewide promo) writes no row. Exercised against a real DbContext over SQLite with the
/// real <see cref="OutboxPendingDispatch"/> backing.
/// </summary>
public sealed class NotificationProducerFeedRowTests : IDisposable
{
    private const string UserId = "user-feed-1";
    private const string TenantId = "tenant-1";
    private const string SecondUserId = "user-feed-2";
    private const string SecondTenantId = "tenant-2";
    private const string CompanylessUserId = "user-feed-3";

    private readonly SqliteConnection _connection;

    public NotificationProducerFeedRowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Bare UserNotification rows are seeded without their User graph; FK enforcement is not
        // what this suite exercises.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
        var user = Cleansia.Core.Domain.Users.User.CreateWithPassword("feed@test.local", "Password123!", "Feed", "Recipient");
        user.Id = UserId;
        user.TenantId = TenantId;
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task SeedUserAsync(string userId, string tenantId)
    {
        await using var ctx = NewContext();
        var user = Cleansia.Core.Domain.Users.User.CreateWithPassword($"{userId}@test.local", "Password123!", "Feed", "Recipient");
        user.Id = userId;
        user.TenantId = tenantId;
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static NotificationProducer NewProducer(CleansiaDbContext ctx, ILogger<NotificationProducer>? logger = null) =>
        new(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationProducer>.Instance);

    private static Dictionary<string, string> OrderArgs(string orderId) => new()
    {
        ["orderId"] = orderId,
        ["orderNumber"] = "A-1042",
    };

    private async Task<List<UserNotification>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task<List<Cleansia.Core.Domain.Outbox.OutboxMessage>> ReadOutboxAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<Cleansia.Core.Domain.Outbox.OutboxMessage>().IgnoreQueryFilters().ToListAsync();
    }

    [Fact]
    public async Task A_Missing_Recipient_Does_Not_Use_The_Caller_Supplied_Tenant()
    {
        await EnsureSchemaAsync();
        await using var ctx = NewContext();
        await NewProducer(ctx).NotifyAsync("missing-user", NotificationEventCatalog.OrderCleanerAssigned,
            OrderArgs("missing-order"), TenantId, "missing-order", CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
        Assert.Empty(await ReadRowsAsync());
        Assert.Empty(await ReadOutboxAsync());
    }

    // ── FD-AC1: one row per send, atomic with the outbox row ─────────────────────────────────

    [Fact]
    public async Task Notify_With_A_Committed_Transaction_Persists_The_Feed_Row_And_The_Outbox_Row_Together()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.OrderCleanerAssigned, OrderArgs("order-1"),
                TenantId, "order-1", CancellationToken.None);
            ctx.Languages.Add(Language.Create("xx", "X-Language"));
            await ctx.CommitAsync(CancellationToken.None);
        }

        var row = Assert.Single(await ReadRowsAsync());
        Assert.Equal(UserId, row.UserId);
        Assert.Equal(NotificationEventCatalog.OrderCleanerAssigned, row.EventKey);
        Assert.Equal(TenantId, row.TenantId);
        Assert.Null(row.ReadOn);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson);
        Assert.Equal("order-1", args!["orderId"]);
        Assert.Equal("A-1042", args["orderNumber"]);

        var outbox = Assert.Single(await ReadOutboxAsync());
        Assert.Equal(QueueNames.NotificationsDispatch, outbox.QueueName);
        Assert.Equal(
            MessageKeys.Push(UserId, NotificationEventCatalog.OrderCleanerAssigned, "order-1"),
            outbox.MessageKey);
    }

    [Fact]
    public async Task Notify_Without_A_Commit_Persists_Neither_The_Feed_Row_Nor_The_Outbox_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.OrderCompleted, OrderArgs("order-2"),
                TenantId, "order-2", CancellationToken.None);
            // The producing handler fails: the scope is discarded without CommitAsync.
        }

        Assert.Empty(await ReadRowsAsync());
        Assert.Empty(await ReadOutboxAsync());
    }

    // ── FD-AC2: the feed row is delivery-independent (mute gates the push, never the row) ────

    [Fact]
    public async Task Muted_Category_And_Zero_Devices_Still_Get_The_Feed_Row()
    {
        await EnsureSchemaAsync();

        await using (var seed = NewContext())
        {
            // The user muted the category the event maps to; they also have zero Device rows.
            var prefs = UserNotificationPreferences.CreateDefaults(UserId);
            prefs.Set(NotificationCategory.OrderCompleted, false);
            seed.Add(prefs);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.OrderCompleted, OrderArgs("order-3"),
                TenantId, "order-3", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        // The mute is the dispatch consumer's concern; the record exists regardless.
        var row = Assert.Single(await ReadRowsAsync());
        Assert.Equal(NotificationEventCatalog.OrderCompleted, row.EventKey);
        Assert.Null(row.ReadOn);
    }

    // ── FD-AC3: digest collapse ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Digest_Send_While_An_Unread_Digest_Row_Exists_Updates_It_In_Place()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.NewJobsAvailable,
                new Dictionary<string, string> { ["count"] = "3" },
                TenantId, "sweep-1", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var first = Assert.Single(await ReadRowsAsync());
        var firstCreatedOn = first.CreatedOn;

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.NewJobsAvailable,
                new Dictionary<string, string> { ["count"] = "5" },
                TenantId, "sweep-2", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var collapsed = Assert.Single(await ReadRowsAsync());
        Assert.Equal(first.Id, collapsed.Id);
        Assert.Null(collapsed.ReadOn);
        Assert.True(collapsed.CreatedOn >= firstCreatedOn);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(collapsed.ArgsJson);
        Assert.Equal("5", args!["count"]);
    }

    [Fact]
    public async Task Digest_Send_After_The_Last_Digest_Row_Was_Read_Inserts_A_Fresh_Unread_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.NewJobsAvailable,
                new Dictionary<string, string> { ["count"] = "3" },
                TenantId, "sweep-1", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var row = await ctx.Set<UserNotification>().IgnoreQueryFilters().SingleAsync();
            row.MarkRead(DateTimeOffset.UtcNow);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.NewJobsAvailable,
                new Dictionary<string, string> { ["count"] = "2" },
                TenantId, "sweep-2", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var rows = await ReadRowsAsync();
        Assert.Equal(2, rows.Count);
        var unread = Assert.Single(rows, r => r.ReadOn == null);
        Assert.Equal("2", JsonSerializer.Deserialize<Dictionary<string, string>>(unread.ArgsJson)!["count"]);
    }

    // ── Q-FEED-01: promo is not feed-scoped — push only, no row ──────────────────────────────

    [Fact]
    public async Task Non_Feed_Event_Enqueues_The_Push_But_Writes_No_Feed_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.PromoNewSitewide,
                new Dictionary<string, string>(), TenantId, "campaign-1", CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Empty(await ReadRowsAsync());
        Assert.Single(await ReadOutboxAsync());
    }

    /// <summary>
    /// A sweep tells many customers at once. The batch reads every recipient's company in one go, then
    /// records each notice exactly as its own <c>NotifyAsync</c> would: under the account's company,
    /// with the same feed rows, the same push keys and bodies collapsed the same way, and the same
    /// warning for a recipient with no account or no company.
    /// </summary>
    [Fact]
    public async Task A_Batch_Records_Exactly_What_Single_Calls_Would_After_One_Recipient_Read()
    {
        await EnsureSchemaAsync();
        await SeedUserAsync(SecondUserId, SecondTenantId);
        await SeedUserAsync(CompanylessUserId, TenantId);
        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlAsync($"UPDATE \"Users\" SET \"TenantId\" = '' WHERE \"Id\" = {CompanylessUserId}");
        }

        (string UserId, string EventKey, Dictionary<string, string> Args, string? Subject)[] notices =
        [
            (UserId, NotificationEventCatalog.OrderCancelled, OrderArgs("order-1"), "order-1"),
            (SecondUserId, NotificationEventCatalog.OrderCancelled, OrderArgs("order-2"), "order-2"),
            (UserId, NotificationEventCatalog.OrderCompleted, OrderArgs("order-3"), "order-3"),
            (UserId, NotificationEventCatalog.OrderCancelled, OrderArgs("order-1"), "order-1"),
            ("missing-user", NotificationEventCatalog.OrderCancelled, OrderArgs("order-4"), "order-4"),
            (CompanylessUserId, NotificationEventCatalog.OrderCancelled, OrderArgs("order-5"), "order-5"),
        ];

        var singleLog = new List<(LogLevel Level, string Message)>();
        var singleReads = new UsersReadCounter();
        await using (var ctx = NewContext(singleReads))
        {
            var producer = NewProducer(ctx, new CapturingLogger<NotificationProducer>(singleLog));
            foreach (var (userId, eventKey, args, subject) in notices)
            {
                await producer.NotifyAsync(userId, eventKey, args, TenantId, subject, CancellationToken.None);
            }

            await ctx.CommitAsync(CancellationToken.None);
        }

        var singleFeed = await ReadFeedShapeAsync();
        var singlePushes = await ReadPushShapeAsync();
        await using (var ctx = NewContext())
        {
            await ctx.Set<UserNotification>().IgnoreQueryFilters().ExecuteDeleteAsync();
            await ctx.Set<Cleansia.Core.Domain.Outbox.OutboxMessage>().IgnoreQueryFilters().ExecuteDeleteAsync();
        }

        var batchLog = new List<(LogLevel Level, string Message)>();
        var batchReads = new UsersReadCounter();
        await using (var ctx = NewContext(batchReads))
        {
            await NewProducer(ctx, new CapturingLogger<NotificationProducer>(batchLog))
                .NotifyEachAsync(notices, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(6, singleReads.Count);
        Assert.Equal(1, batchReads.Count);
        Assert.Equal(singleFeed, await ReadFeedShapeAsync());
        Assert.Equal(singlePushes, await ReadPushShapeAsync());
        Assert.Equal(singleLog, batchLog);

        Assert.Equal(
            new[] { TenantId, TenantId, TenantId, SecondTenantId },
            singleFeed.Select(row => row.TenantId));
        Assert.Equal(3, singlePushes.Count);
        Assert.Equal(2, singleLog.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task An_Empty_Batch_Reads_No_Recipient_And_Records_Nothing()
    {
        await EnsureSchemaAsync();
        var reads = new UsersReadCounter();

        await using (var ctx = NewContext(reads))
        {
            await NewProducer(ctx).NotifyEachAsync([], CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(0, reads.Count);
        Assert.Empty(await ReadRowsAsync());
        Assert.Empty(await ReadOutboxAsync());
    }

    private async Task<List<(string UserId, string EventKey, string ArgsJson, string? TenantId)>> ReadFeedShapeAsync() =>
        (await ReadRowsAsync())
            .Select(row => (row.UserId, row.EventKey, row.ArgsJson, row.TenantId))
            .OrderBy(row => row.UserId, StringComparer.Ordinal)
            .ThenBy(row => row.EventKey, StringComparer.Ordinal)
            .ThenBy(row => row.ArgsJson, StringComparer.Ordinal)
            .ToList();

    private async Task<List<(string QueueName, string MessageKey, string? TenantId, string Body)>> ReadPushShapeAsync() =>
        (await ReadOutboxAsync())
            .Select(row => (row.QueueName, row.MessageKey, row.TenantId, row.Body))
            .OrderBy(row => row.MessageKey, StringComparer.Ordinal)
            .ToList();

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

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
