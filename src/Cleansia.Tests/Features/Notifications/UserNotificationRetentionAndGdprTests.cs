using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// FD-AC10 — feed-row lifecycle end: the retention sweep hard-deletes rows older than the 90-day
/// window and prunes any user's rows beyond the newest-500 runaway cap; a GDPR erasure deletes all
/// the user's rows, after which the unread count is zero. Real repositories over SQLite; only the
/// external edges (config, blobs, Stripe) are mocked.
/// </summary>
public sealed class UserNotificationRetentionAndGdprTests : IDisposable
{
    private const string UserId = "user-ret-1";
    private const string KeptUserId = "user-ret-2";

    private readonly SqliteConnection _connection;
    private readonly Mock<IAppConfigurationProvider> _configProvider = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public UserNotificationRetentionAndGdprTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _configProvider
            .Setup(c => c.IsFeatureEnabledAsync(
                RetentionDefaults.FeatureFlagName, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _configProvider
            .Setup(c => c.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
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

    private static UserNotification Row(string userId, DateTimeOffset createdOn)
    {
        var row = UserNotification.Create(userId, NotificationEventCatalog.OrderCompleted, "{}", null);
        row.Created("seed", createdOn);
        return row;
    }

    private DataRetentionBackgroundService NewRetentionService(CleansiaDbContext ctx)
    {
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        return new DataRetentionBackgroundService(
            new UserRepository(ctx),
            new DeviceRepository(ctx, session),
            new GdprRequestRepository(ctx),
            new OrderRepository(ctx),
            new UserConsentRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new UserNotificationRepository(ctx),
            _configProvider.Object,
            _blobClientFactory.Object,
            NullLogger<DataRetentionBackgroundService>.Instance);
    }

    private async Task<List<UserNotification>> ReadRowsAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    [Fact]
    public async Task Retention_Deletes_Rows_Older_Than_Ninety_Days_And_Keeps_Younger_Ones()
    {
        await EnsureSchemaAsync();
        var now = DateTimeOffset.UtcNow;

        await using (var seed = NewContext())
        {
            seed.AddRange(
                Row(UserId, now.AddDays(-100)),
                Row(UserId, now.AddDays(-91)),
                Row(UserId, now.AddDays(-10)),
                Row(KeptUserId, now.AddDays(-5)));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            await NewRetentionService(ctx).RunAllRetentionTasksAsync(CancellationToken.None);
        }

        var remaining = await ReadRowsAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, r => Assert.True(r.CreatedOn > now.AddDays(-90)));
    }

    [Fact]
    public async Task Retention_Caps_A_Runaway_User_At_The_Newest_Five_Hundred_Rows()
    {
        await EnsureSchemaAsync();
        var now = DateTimeOffset.UtcNow;

        await using (var seed = NewContext())
        {
            // 510 recent rows (inside the 90-day window) for one user; a small set for another.
            for (var i = 0; i < 510; i++)
            {
                seed.Add(Row(UserId, now.AddMinutes(-i)));
            }

            seed.Add(Row(KeptUserId, now.AddMinutes(-1)));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            await NewRetentionService(ctx).RunAllRetentionTasksAsync(CancellationToken.None);
        }

        var remaining = await ReadRowsAsync();
        Assert.Equal(RetentionDefaults.MaxNotificationsPerUser, remaining.Count(r => r.UserId == UserId));
        Assert.Equal(1, remaining.Count(r => r.UserId == KeptUserId));
        // The overflow removed is the OLDEST tail — the newest row survives.
        Assert.Contains(remaining, r => r.UserId == UserId && r.CreatedOn == now);
        Assert.DoesNotContain(remaining, r => r.UserId == UserId && r.CreatedOn <= now.AddMinutes(-500));
    }

    [Fact]
    public async Task Gdpr_Erasure_Deletes_All_The_Users_Rows_And_The_Unread_Count_Drops_To_Zero()
    {
        await EnsureSchemaAsync();
        var now = DateTimeOffset.UtcNow;

        var user = User.CreateWithPassword(
            "erase-me@cleansia.test", "Test-password-1!", "Erase", "Me", UserProfile.Customer);
        user.Id = UserId;

        await using (var seed = NewContext())
        {
            seed.Add(user);
            seed.AddRange(
                Row(UserId, now.AddDays(-1)),
                Row(UserId, now.AddDays(-2)),
                Row(KeptUserId, now.AddDays(-1)));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var session = new TestUserSessionProvider(UserId, "erase-me@cleansia.test");
            var gdpr = new GdprDeletionService(
                new UserRepository(ctx),
                new OrderRepository(ctx),
                new EmployeeDocumentRepository(ctx),
                new EmployeeInvoiceRepository(ctx),
                new UserMembershipRepository(ctx),
                new OrderPhotoRepository(ctx),
                new DeviceRepository(ctx, session),
                new CartRepository(ctx),
                new UserConsentRepository(ctx),
                new GdprRequestRepository(ctx),
                new DisputeRepository(ctx),
                new SavedAddressRepository(ctx, session),
                new OrderEmployeePayRepository(ctx),
                new RecurringBookingTemplateRepository(ctx),
                new UserNotificationRepository(ctx),
                Mock.Of<IStripeClient>(),
                _blobClientFactory.Object,
                NullLogger<GdprDeletionService>.Instance);

            var result = await gdpr.DeleteUserAccountAsync(
                UserId, "gdpr_erasure_test", _ => ("test-actor", null), CancellationToken.None);

            Assert.True(result.IsSuccess);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var remaining = await ReadRowsAsync();
        Assert.DoesNotContain(remaining, r => r.UserId == UserId);
        Assert.Single(remaining, r => r.UserId == KeptUserId);

        await using (var readCtx = NewContext())
        {
            var count = await new UserNotificationRepository(readCtx).GetUnreadCountAsync(
                UserId, NotificationFeedEventKeys.Customer, CancellationToken.None);
            Assert.Equal(0, count);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
