using System.Text.Json;
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

/// <summary>
/// F8 -- the sitewide-promo fan-out must RESUME on redelivery, not RESTART from offset 0. A page read
/// throwing on a later page (outside the per-recipient try/catch) re-enqueues the whole opted-in base
/// 1..N times (up to maxDequeueCount) and re-costs the dispatch. This drives the real
/// SendSitewidePromoFanoutHandler over a real CleansiaDbContext to prove the per-campaign cursor: a
/// redelivery with a persisted cursor resumes past the already-processed recipients (AC3), the cursor
/// advances across page boundaries and the campaign is marked complete (AC4), a completed-campaign
/// redelivery is a no-op (AC5), and the per-recipient continue-on-failure path still holds (AC7).
///
/// The downstream push:{UserId}:{EventKey} dedup (ADR-0002 D2.2) stays the load-bearing EFFECT guard and
/// is untouched; this cursor is the cost/spam layer on top of it.
///
/// Test-first: RED until the handler reads/persists a per-campaign cursor via ICampaignProgressStore.
/// </summary>
public sealed class SendSitewidePromoFanoutResumeTests : IDisposable
{
    private const string CampaignId = "promo::resume-test";
    private readonly SqliteConnection _connection;
    private readonly InMemoryCampaignProgressStore _progress = new();

    public SendSitewidePromoFanoutResumeTests()
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
            new FixedTenantProvider(tenantId: null));

    private async Task SeedOptedInUserAsync(string userId)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var prefs = UserNotificationPreferences.CreateDefaults(userId);
        prefs.Set(NotificationCategory.Promo, true);
        prefs.TenantId = null;
        prefs.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(prefs);

        var user = User.CreateWithPassword(
            email: $"{userId}@cleansia.test", password: "Password1", firstName: "F", lastName: "L");
        user.Id = userId;
        user.TenantId = null;
        user.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(user);

        await ctx.SaveChangesAsync();
    }

    private SendSitewidePromoFanoutHandler CreateHandler(IQueueClient queueClient, int pageSize = 200)
    {
        // ONE shared context for both repos: the handler joins the two queryables, and EF rejects a
        // join spanning two context instances.
        var ctx = NewContext();
        return new SendSitewidePromoFanoutHandler(
            new UserNotificationPreferencesRepository(ctx),
            new UserRepository(ctx),
            queueClient,
            _progress,
            new FixedTenantProvider(tenantId: null),
            NullLogger<SendSitewidePromoFanoutHandler>.Instance,
            pageSize);
    }

    private static SendSitewidePromoMessage Campaign() => new(
        TitleByLocale: new() { ["en"] = "Promo!" },
        BodyByLocale: new() { ["en"] = "Big sale." },
        TenantId: null,
        CampaignId: CampaignId);

    private static string Serialize(SendSitewidePromoMessage campaign) =>
        JsonSerializer.Serialize(campaign, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task A_Redelivery_With_A_Persisted_Cursor_Resumes_Past_The_Already_Processed_Recipients()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedOptedInUserAsync($"USER-{i}");
        }

        // A prior attempt fully processed pages up to and including USER-1, then a later page read threw
        // (F8) so the message is redelivered. The cursor reflects the last fully-processed recipient.
        await _progress.AdvanceAsync(CampaignId, "USER-1");

        var sentUserIds = new List<string>();
        var okQueue = new Mock<IQueueClient>();
        okQueue
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendPushNotificationMessage, CancellationToken>((_, msg, _2) => sentUserIds.Add(msg.UserId))
            .Returns(Task.CompletedTask);

        // The redelivery must SEEK past USER-1 (the cursor), NOT restart at offset 0.
        await CreateHandler(okQueue.Object).HandleAsync(Serialize(Campaign()), CancellationToken.None);

        // Only the not-yet-processed tail is enqueued; USER-0/USER-1 are not re-pushed (re-cost) on retry.
        Assert.Equal(new[] { "USER-2", "USER-3", "USER-4" }, sentUserIds.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task A_Full_Run_Advances_The_Cursor_To_The_Last_Recipient_And_Marks_The_Campaign_Complete()
    {
        await SeedOptedInUserAsync("USER-0");
        await SeedOptedInUserAsync("USER-1");
        await SeedOptedInUserAsync("USER-2");

        var queue = new Mock<IQueueClient>();
        queue
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Page size 2 forces a real second page, proving the cursor advances across page boundaries (AC4).
        await CreateHandler(queue.Object, pageSize: 2).HandleAsync(Serialize(Campaign()), CancellationToken.None);

        var progress = await _progress.GetAsync(CampaignId);
        Assert.Equal("USER-2", progress.LastProcessedUserId);
        Assert.True(progress.IsComplete);
    }

    [Fact]
    public async Task A_Completed_Campaign_Redelivery_Enqueues_Nothing()
    {
        await SeedOptedInUserAsync("USER-0");
        await SeedOptedInUserAsync("USER-1");

        var firstRun = new List<string>();
        var firstQueue = new Mock<IQueueClient>();
        firstQueue
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendPushNotificationMessage, CancellationToken>((_, msg, _2) => firstRun.Add(msg.UserId))
            .Returns(Task.CompletedTask);

        await CreateHandler(firstQueue.Object).HandleAsync(Serialize(Campaign()), CancellationToken.None);
        Assert.Equal(2, firstRun.Count);

        // Redelivery of the SAME campaign after it ran to completion: recognized complete, no re-paging.
        var secondQueue = new Mock<IQueueClient>();
        await CreateHandler(secondQueue.Object).HandleAsync(Serialize(Campaign()), CancellationToken.None);

        secondQueue.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Single_Bad_Per_User_Enqueue_Does_Not_Poison_The_Campaign()
    {
        await SeedOptedInUserAsync("USER-0");
        await SeedOptedInUserAsync("USER-1");
        await SeedOptedInUserAsync("USER-2");

        var sent = new List<string>();
        var queue = new Mock<IQueueClient>();
        queue
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendPushNotificationMessage, CancellationToken>((_, msg, _2) =>
            {
                if (msg.UserId == "USER-1")
                {
                    throw new InvalidOperationException("one bad enqueue");
                }
                sent.Add(msg.UserId);
            })
            .Returns(Task.CompletedTask);

        // The per-recipient try/catch-and-continue swallows the single failure; the campaign completes.
        await CreateHandler(queue.Object).HandleAsync(Serialize(Campaign()), CancellationToken.None);

        Assert.Equal(new[] { "USER-0", "USER-2" }, sent.OrderBy(x => x).ToArray());
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

    private sealed class InMemoryCampaignProgressStore : ICampaignProgressStore
    {
        private readonly Dictionary<string, string?> _cursor = new();
        private readonly HashSet<string> _complete = [];

        public Task<CampaignProgress> GetAsync(string campaignId, CancellationToken ct = default) =>
            Task.FromResult(new CampaignProgress(
                _cursor.TryGetValue(campaignId, out var c) ? c : null,
                _complete.Contains(campaignId)));

        public Task AdvanceAsync(string campaignId, string lastUserId, CancellationToken ct = default)
        {
            _cursor[campaignId] = lastUserId;
            return Task.CompletedTask;
        }

        public Task MarkCompleteAsync(string campaignId, CancellationToken ct = default)
        {
            _complete.Add(campaignId);
            return Task.CompletedTask;
        }
    }
}
