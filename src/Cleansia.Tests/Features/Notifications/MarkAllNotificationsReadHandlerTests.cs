using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// FD-AC6 — the watermarked mark-all: rows at or before <c>UpToCreatedOn</c> are marked read; a row
/// created AFTER the watermark (the race the watermark exists to close — a producer firing between
/// the client's fetch and its mark-all call) stays unread; a null watermark marks everything; and
/// the operation is keyset-scoped, so the customer host's mark-all never eats the partner badge.
/// </summary>
public sealed class MarkAllNotificationsReadHandlerTests : IDisposable
{
    private const string UserId = "user-markall-1";

    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public MarkAllNotificationsReadHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

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

    private static UserNotification Row(string eventKey, DateTimeOffset createdOn)
    {
        var row = UserNotification.Create(UserId, eventKey, "{}", null);
        row.Created("seed", createdOn);
        return row;
    }

    /// <summary>Three customer rows at T0, T0+1m, T0+2m; one at T0+10m (post-watermark); one partner digest row.</summary>
    private async Task SeedAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext();
        seed.AddRange(
            Row(NotificationEventCatalog.OrderConfirmed, T0),
            Row(NotificationEventCatalog.OrderCompleted, T0.AddMinutes(1)),
            Row(NotificationEventCatalog.DisputeReply, T0.AddMinutes(2)),
            Row(NotificationEventCatalog.OrderCancelled, T0.AddMinutes(10)),
            Row(NotificationEventCatalog.NewJobsAvailable, T0));
        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task<int> HandleAsync(MarkAllNotificationsRead.Command command)
    {
        await using var ctx = NewContext();
        var handler = new MarkAllNotificationsRead.Handler(
            new UserNotificationRepository(ctx),
            new TestUserSessionProvider(UserId, "caller@cleansia.test"));

        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!.MarkedCount;
    }

    private async Task<List<UserNotification>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    [Fact]
    public async Task Watermarked_MarkAll_Leaves_Rows_Created_After_The_Watermark_Unread()
    {
        await SeedAsync();

        var marked = await HandleAsync(new MarkAllNotificationsRead.Command(
            UpToCreatedOn: T0.AddMinutes(2), Audience: NotificationFeedAudience.Customer));

        Assert.Equal(3, marked);
        var rows = await ReadRowsAsync();
        Assert.All(rows.Where(r => r.CreatedOn <= T0.AddMinutes(2) && r.EventKey != NotificationEventCatalog.NewJobsAvailable),
            r => Assert.NotNull(r.ReadOn));
        // The row that arrived after the client's fetch stays unread — it will be counted on the
        // next badge fetch instead of being silently eaten.
        Assert.Null(rows.Single(r => r.EventKey == NotificationEventCatalog.OrderCancelled).ReadOn);
        // The partner digest row is out of the customer host's reach entirely.
        Assert.Null(rows.Single(r => r.EventKey == NotificationEventCatalog.NewJobsAvailable).ReadOn);
    }

    [Fact]
    public async Task Null_Watermark_Marks_Every_Unread_Row_In_The_Keyset()
    {
        await SeedAsync();

        var marked = await HandleAsync(new MarkAllNotificationsRead.Command(
            UpToCreatedOn: null, Audience: NotificationFeedAudience.Customer));

        Assert.Equal(4, marked);
        var rows = await ReadRowsAsync();
        Assert.All(rows.Where(r => r.EventKey != NotificationEventCatalog.NewJobsAvailable),
            r => Assert.NotNull(r.ReadOn));
        Assert.Null(rows.Single(r => r.EventKey == NotificationEventCatalog.NewJobsAvailable).ReadOn);
    }

    [Fact]
    public async Task Repeat_MarkAll_Is_Idempotent()
    {
        await SeedAsync();

        Assert.Equal(4, await HandleAsync(new MarkAllNotificationsRead.Command(null, NotificationFeedAudience.Customer)));
        Assert.Equal(0, await HandleAsync(new MarkAllNotificationsRead.Command(null, NotificationFeedAudience.Customer)));
    }

    [Fact]
    public async Task Partner_MarkAll_Touches_Only_The_Digest_Row()
    {
        await SeedAsync();

        var marked = await HandleAsync(new MarkAllNotificationsRead.Command(null, NotificationFeedAudience.Partner));

        Assert.Equal(1, marked);
        var rows = await ReadRowsAsync();
        Assert.NotNull(rows.Single(r => r.EventKey == NotificationEventCatalog.NewJobsAvailable).ReadOn);
        Assert.All(rows.Where(r => r.EventKey != NotificationEventCatalog.NewJobsAvailable),
            r => Assert.Null(r.ReadOn));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
