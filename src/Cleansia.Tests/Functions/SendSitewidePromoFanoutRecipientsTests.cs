using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public sealed class SendSitewidePromoFanoutRecipientsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<QueueEnvelope<SendPushNotificationMessage>> _sent = [];
    private readonly Mock<IQueueClient> _queue = new();

    public SendSitewidePromoFanoutRecipientsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _queue
            .Setup(q => q.SendAsync(
                QueueNames.NotificationsDispatch,
                It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, QueueEnvelope<SendPushNotificationMessage>, CancellationToken>((_, envelope, _2) => _sent.Add(envelope))
            .Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private async Task SeedOptedInAsync(User user)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var prefs = UserNotificationPreferences.CreateDefaults(user.Id);
        prefs.Set(NotificationCategory.Promo, true);
        prefs.TenantId = TestTenants.Default;
        prefs.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(prefs);

        user.TenantId = TestTenants.Default;
        user.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(user);

        await ctx.SaveChangesAsync();
    }

    private static User NewUser(string id, UserProfile profile = UserProfile.Customer, AdminRole? adminRole = null)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Password1", "F", "L", profile, adminRole: adminRole);
        user.Id = id;
        return user;
    }

    private async Task RunAsync(string campaignId)
    {
        var ctx = NewContext();
        var handler = new SendSitewidePromoFanoutHandler(
            new UserNotificationPreferencesRepository(ctx),
            new UserRepository(ctx),
            _queue.Object,
            new NoOpCampaignProgressStore(),
            new FixedTenantProvider(TestTenants.Default),
            NullLogger<SendSitewidePromoFanoutHandler>.Instance);

        var campaign = new SendSitewidePromoMessage(
            TitleByLocale: new() { ["en"] = "Promo!" },
            BodyByLocale: new() { ["en"] = "Big sale." },
            TenantId: TestTenants.Default,
            CampaignId: campaignId);

        await handler.HandleAsync(
            JsonSerializer.Serialize(campaign, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            CancellationToken.None);
    }

    [Fact]
    public async Task Only_Active_Customers_That_Are_Not_Erased_Receive_The_Promo()
    {
        await SeedOptedInAsync(NewUser("CUSTOMER"));
        await SeedOptedInAsync(NewUser("CLEANER", UserProfile.Employee));
        await SeedOptedInAsync(NewUser("ADMIN", UserProfile.Administrator, AdminRole.Administrator));
        await SeedOptedInAsync(NewUser("ERASED").Anonymize());
        var inactive = NewUser("INACTIVE");
        inactive.IsActive = false;
        await SeedOptedInAsync(inactive);

        await RunAsync("promo:a");

        Assert.Equal(new[] { "CUSTOMER" }, _sent.Select(e => e.Payload.UserId).ToArray());
    }

    [Fact]
    public async Task Each_Campaign_Gets_Its_Own_Push_Key_Per_User()
    {
        await SeedOptedInAsync(NewUser("CUSTOMER"));

        await RunAsync("promo:a");
        await RunAsync("promo:b");

        Assert.Equal(
            new[]
            {
                MessageKeys.Push("CUSTOMER", NotificationEventCatalog.PromoNewSitewide, "promo:a"),
                MessageKeys.Push("CUSTOMER", NotificationEventCatalog.PromoNewSitewide, "promo:b"),
            },
            _sent.Select(e => e.MessageKey).ToArray());
        Assert.All(_sent, e => Assert.Equal(TestTenants.Default, e.TenantId));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

    private sealed class NoOpCampaignProgressStore : ICampaignProgressStore
    {
        public Task<CampaignProgress> GetAsync(string campaignId, CancellationToken ct = default) =>
            Task.FromResult(new CampaignProgress(null, false));
        public Task AdvanceAsync(string campaignId, string lastUserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkCompleteAsync(string campaignId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
