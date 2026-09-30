using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// A logout leaves the device row behind as a tombstone — handset id and push token included — so a later
/// login can reclaim it. The stale-device sweep used to take only active rows, so a logged-out handset was
/// kept for the life of the account. A tombstone now goes once it was deactivated more than the 90-day
/// window ago (or, with no deactivation date, last seen that long ago); a recent logout is kept.
/// </summary>
[Collection("PostgresCollection")]
public class DeviceTombstoneRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string UserId = "user-device-tombstones";

    private const string StaleActive = "device-stale-active";
    private const string FreshActive = "device-fresh-active";
    private const string OldTombstone = "device-old-tombstone";
    private const string RecentTombstone = "device-recent-tombstone";
    private const string UndatedOldTombstone = "device-undated-old-tombstone";

    [Fact]
    public async Task A_Logged_Out_Device_Goes_Ninety_Days_After_The_Logout()
    {
        await TestMethod(
            setup: services =>
            {
                var factory = new Mock<IBlobContainerClientFactory>();
                factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(Mock.Of<IBlobContainerClient>());
                services.Replace(ServiceDescriptor.Singleton(_ => factory.Object));
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var user = User.CreateWithPassword("device.tombstones@cleansia.test", "Seed-Password-123", "Device", "Owner");
                user.Id = UserId;
                context.Users.Add(user);

                var oldTombstone = NewDevice(OldTombstone);
                oldTombstone.Deactivated(UserId, DateTimeOffset.UtcNow.AddDays(-100));
                var recentTombstone = NewDevice(RecentTombstone);
                recentTombstone.Deactivated(UserId, DateTimeOffset.UtcNow.AddDays(-10));
                var undatedTombstone = NewDevice(UndatedOldTombstone);
                undatedTombstone.IsActive = false;

                context.Devices.AddRange(NewDevice(StaleActive), NewDevice(FreshActive), oldTombstone, recentTombstone, undatedTombstone);
                await context.CommitAsync(CancellationToken.None);

                var longAgo = DateTimeOffset.UtcNow.AddDays(-120);
                string[] lastSeenLongAgo = [StaleActive, OldTombstone, RecentTombstone, UndatedOldTombstone];
                await context.Devices.IgnoreQueryFilters()
                    .Where(d => lastSeenLongAgo.Contains(d.DeviceId))
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastActiveAt, longAgo));
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var remaining = await context.Devices.IgnoreQueryFilters().Select(d => d.DeviceId).ToListAsync();

                Assert.DoesNotContain(StaleActive, remaining);
                Assert.DoesNotContain(OldTombstone, remaining);
                Assert.DoesNotContain(UndatedOldTombstone, remaining);
                Assert.Contains(FreshActive, remaining);
                Assert.Contains(RecentTombstone, remaining);
            });
    }

    private static Device NewDevice(string deviceId) =>
        Device.Create(UserId, "android", $"fcm-{deviceId}", deviceId);
}
