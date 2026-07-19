using System.Text.Json;
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
/// FD-AC4 — the per-host paged feed: CreatedOn-desc ordering, the 50-item pageSize cap, and the
/// audience keyset scoping that keeps a dual-role user's partner digest rows out of the customer
/// feed (and vice versa). Runs the real handler over a real <see cref="CleansiaDbContext"/> on
/// SQLite so the specification + sort + paging SQL actually translates.
/// </summary>
public sealed class GetPagedUserNotificationsHandlerTests : IDisposable
{
    private const string UserId = "user-paged-1";
    private const string OtherUserId = "user-paged-2";

    private readonly SqliteConnection _connection;

    public GetPagedUserNotificationsHandlerTests()
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

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static UserNotification Row(string userId, string eventKey, DateTimeOffset createdOn, string orderId = "o")
    {
        var row = UserNotification.Create(
            userId,
            eventKey,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["orderId"] = orderId }),
            tenantId: null);
        row.Created("seed", createdOn);
        return row;
    }

    private async Task SeedAsync(params UserNotification[] rows)
    {
        await using var ctx = NewContext();
        ctx.AddRange(rows.Cast<object>());
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<Cleansia.Core.AppServices.Shared.DTOs.ResponseModels.PagedData<Cleansia.Core.AppServices.Features.Notifications.DTOs.UserNotificationDto>>
        HandleAsync(GetPagedUserNotifications.Request request, string callerUserId = UserId)
    {
        await using var ctx = NewContext();
        // The paged Handler is internal (the A2 canonical form); construct via reflection, the
        // established pattern for internal handlers (see RegisterDeviceHandlerTests).
        var handlerType = typeof(GetPagedUserNotifications).GetNestedType(
            "Handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!,
            new UserNotificationRepository(ctx),
            new TestUserSessionProvider(callerUserId, "caller@cleansia.test"))!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        return await (Task<Cleansia.Core.AppServices.Shared.DTOs.ResponseModels.PagedData<Cleansia.Core.AppServices.Features.Notifications.DTOs.UserNotificationDto>>)
            handleMethod!.Invoke(handler, [request, CancellationToken.None])!;
    }

    [Fact]
    public async Task Customer_Feed_Pages_CreatedOn_Desc_And_Excludes_Partner_And_Foreign_Rows()
    {
        await EnsureSchemaAsync();
        var t0 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

        // 25 customer-keyset rows for the caller, plus partner digest rows (dual-role) and another
        // user's rows — neither of which may surface.
        var rows = Enumerable.Range(0, 25)
            .Select(i => Row(UserId, NotificationEventCatalog.OrderCompleted, t0.AddMinutes(i), $"order-{i}"))
            .ToList();
        rows.Add(Row(UserId, NotificationEventCatalog.NewJobsAvailable, t0.AddHours(5)));
        rows.Add(Row(OtherUserId, NotificationEventCatalog.OrderCompleted, t0.AddHours(6)));
        await SeedAsync(rows.ToArray());

        var page = await HandleAsync(new GetPagedUserNotifications.Request
        {
            Offset = 0,
            Limit = 20,
            Audience = NotificationFeedAudience.Customer,
        });

        Assert.Equal(25, page.Total);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(1, page.PageNumber);
        var items = page.Data.ToList();
        Assert.Equal(20, items.Count);
        // Newest first: the newest customer row is order-24 at t0+24min.
        Assert.Equal("order-24", items[0].Args["orderId"]);
        Assert.True(items.SequenceEqual(items.OrderByDescending(i => i.CreatedOn)));
        Assert.All(items, i => Assert.Equal(NotificationEventCatalog.OrderCompleted, i.EventKey));
        Assert.All(items, i => Assert.Null(i.ReadOn));
    }

    [Fact]
    public async Task Partner_Feed_Sees_Only_The_Digest_Rows_Of_The_Same_User()
    {
        await EnsureSchemaAsync();
        var t0 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedAsync(
            Row(UserId, NotificationEventCatalog.OrderCompleted, t0),
            Row(UserId, NotificationEventCatalog.NewJobsAvailable, t0.AddMinutes(1)));

        var page = await HandleAsync(new GetPagedUserNotifications.Request
        {
            Audience = NotificationFeedAudience.Partner,
        });

        Assert.Equal(1, page.Total);
        var item = Assert.Single(page.Data);
        Assert.Equal(NotificationEventCatalog.NewJobsAvailable, item.EventKey);
    }

    [Fact]
    public async Task PageSize_Caps_At_Fifty()
    {
        await EnsureSchemaAsync();
        var t0 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var rows = Enumerable.Range(0, 60)
            .Select(i => Row(UserId, NotificationEventCatalog.OrderConfirmed, t0.AddMinutes(i)))
            .ToArray();
        await SeedAsync(rows);

        var page = await HandleAsync(new GetPagedUserNotifications.Request
        {
            Offset = 0,
            Limit = 200,
            Audience = NotificationFeedAudience.Customer,
        });

        Assert.Equal(60, page.Total);
        Assert.Equal(GetPagedUserNotifications.MaxPageSize, page.PageSize);
        Assert.Equal(GetPagedUserNotifications.MaxPageSize, page.Data.Count());
    }

    [Fact]
    public void Default_Page_Size_Is_Twenty()
    {
        Assert.Equal(GetPagedUserNotifications.DefaultPageSize, new GetPagedUserNotifications.Request().Limit);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
