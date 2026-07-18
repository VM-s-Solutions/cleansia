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
/// FD-AC5 (backend half) — the badge count: only UNREAD rows, only the calling host's keyset, only
/// the caller's own rows. A dual-role user's customer badge never counts partner digest rows and
/// vice versa.
/// </summary>
public sealed class GetUnreadNotificationCountHandlerTests : IDisposable
{
    private const string UserId = "user-count-1";
    private const string OtherUserId = "user-count-2";

    private readonly SqliteConnection _connection;

    public GetUnreadNotificationCountHandlerTests()
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

    private async Task SeedAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        await using (var seed = NewContext())
        {
            var read = UserNotification.Create(UserId, NotificationEventCatalog.OrderCompleted, "{}", null);
            read.MarkRead(DateTimeOffset.UtcNow);

            seed.AddRange(
                UserNotification.Create(UserId, NotificationEventCatalog.OrderConfirmed, "{}", null),
                UserNotification.Create(UserId, NotificationEventCatalog.DisputeReply, "{}", null),
                UserNotification.Create(UserId, NotificationEventCatalog.LoyaltyTierUpgrade, "{}", null),
                read,
                UserNotification.Create(UserId, NotificationEventCatalog.NewJobsAvailable, "{}", null),
                UserNotification.Create(OtherUserId, NotificationEventCatalog.OrderConfirmed, "{}", null));
            await seed.CommitAsync(CancellationToken.None);
        }
    }

    private async Task<int> CountAsync(NotificationFeedAudience audience)
    {
        await using var ctx = NewContext();
        var handler = new GetUnreadNotificationCount.Handler(
            new UserNotificationRepository(ctx),
            new TestUserSessionProvider(UserId, "caller@cleansia.test"));

        var result = await handler.Handle(
            new GetUnreadNotificationCount.Query(audience), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!.Count;
    }

    [Fact]
    public async Task Customer_Badge_Counts_Only_The_Callers_Unread_Customer_Rows()
    {
        await SeedAsync();
        Assert.Equal(3, await CountAsync(NotificationFeedAudience.Customer));
    }

    [Fact]
    public async Task Partner_Badge_Counts_Only_The_Digest_Rows()
    {
        await SeedAsync();
        Assert.Equal(1, await CountAsync(NotificationFeedAudience.Partner));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
