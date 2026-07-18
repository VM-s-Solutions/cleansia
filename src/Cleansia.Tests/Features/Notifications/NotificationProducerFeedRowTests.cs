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

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static NotificationProducer NewProducer(CleansiaDbContext ctx) =>
        new(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx));

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

    // ── FD-AC1: one row per send, atomic with the outbox row ─────────────────────────────────

    [Fact]
    public async Task Notify_With_A_Committed_Transaction_Persists_The_Feed_Row_And_The_Outbox_Row_Together()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext())
        {
            await NewProducer(ctx).NotifyAsync(
                UserId, NotificationEventCatalog.OrderConfirmed, OrderArgs("order-1"),
                TenantId, "order-1", CancellationToken.None);
            ctx.Languages.Add(Language.Create("xx", "X-Language"));
            await ctx.CommitAsync(CancellationToken.None);
        }

        var row = Assert.Single(await ReadRowsAsync());
        Assert.Equal(UserId, row.UserId);
        Assert.Equal(NotificationEventCatalog.OrderConfirmed, row.EventKey);
        Assert.Equal(TenantId, row.TenantId);
        Assert.Null(row.ReadOn);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson);
        Assert.Equal("order-1", args!["orderId"]);
        Assert.Equal("A-1042", args["orderNumber"]);

        var outbox = Assert.Single(await ReadOutboxAsync());
        Assert.Equal(QueueNames.NotificationsDispatch, outbox.QueueName);
        Assert.Equal(
            MessageKeys.Push(UserId, NotificationEventCatalog.OrderConfirmed, "order-1"),
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

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
