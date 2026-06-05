using Cleansia.Core.Domain.Notifications;
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
/// PR review #3 (S8) — the sitewide-promo fan-out MUST stay within the campaign's tenant. The old
/// query used IgnoreQueryFilters() on both sides with NO TenantId predicate, so SetTenantOverride was
/// a no-op and one tenant's campaign fanned out to opted-in users of EVERY tenant. Exercised against a
/// REAL <see cref="CleansiaDbContext"/> over SQLite so the join + the new TenantId filter materialize.
///
/// Test-first: RED until the handler filters by campaign.TenantId.
/// </summary>
public sealed class SendSitewidePromoFanoutTenantScopeTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SendSitewidePromoFanoutTenantScopeTests()
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

    private async Task SeedOptedInUserAsync(string userId, string? tenantId)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var prefs = UserNotificationPreferences.CreateDefaults(userId);
        prefs.Set(NotificationCategory.Promo, true);
        // The handler ignores query filters and filters by the row's TenantId explicitly, so we seed
        // the TenantId directly on the row (CommitAsync would stamp the ambient null otherwise).
        prefs.TenantId = tenantId;
        prefs.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(prefs);

        var user = User.CreateWithPassword(
            email: $"{userId}@cleansia.test", password: "Password1", firstName: "F", lastName: "L");
        user.Id = userId;
        user.TenantId = tenantId;
        user.Created("system", DateTimeOffset.UtcNow);
        ctx.Add(user);

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task FanOut_Only_Reaches_The_Campaign_Tenants_Users()
    {
        await SeedOptedInUserAsync("USER-A", "TENANT-A");
        await SeedOptedInUserAsync("USER-B", "TENANT-B");

        var sentUserIds = new List<string>();
        var queueClient = new Mock<IQueueClient>();
        queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendPushNotificationMessage, CancellationToken>((_, msg, _2) => sentUserIds.Add(msg.UserId))
            .Returns(Task.CompletedTask);

        await using var ctx = NewContext();
        var handler = new SendSitewidePromoFanoutHandler(
            new UserNotificationPreferencesRepository(ctx),
            new UserRepository(ctx),
            queueClient.Object,
            new FixedTenantProvider(tenantId: null),
            NullLogger<SendSitewidePromoFanoutHandler>.Instance);

        var campaign = new SendSitewidePromoMessage(
            TitleByLocale: new() { ["en"] = "Promo!" },
            BodyByLocale: new() { ["en"] = "Big sale." },
            TenantId: "TENANT-A");

        await handler.HandleAsync(
            System.Text.Json.JsonSerializer.Serialize(campaign,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
            CancellationToken.None);

        // S8: ONLY tenant A's opted-in user is reached; tenant B is never touched.
        Assert.Equal(new[] { "USER-A" }, sentUserIds);
    }

    private sealed class FixedTenantProvider(string? tenantId) : Cleansia.Core.Domain.Repositories.ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
