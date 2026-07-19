using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.LiveActivities;

/// <summary>
/// ADR-0029 D3 (cleanup path 3) — the janitor sweep runs the REAL
/// <see cref="ILiveActivityTokenRepository.GetStaleOrderScopedTokensAsync"/> LINQ against an actual
/// <see cref="CleansiaDbContext"/> (SQLite in-memory), so the push-to-start exclusion is pinned on the
/// query the janitor really executes — not on a pure predicate no production path calls. The WHERE
/// clause <c>OrderId != null &amp;&amp; LastUpdatedAt &lt; cutoff</c> must return ONLY order-scoped rows
/// past the cutoff: a lost <c>OrderId != null</c> guard would sweep every install's push-to-start row.
/// </summary>
public sealed class LiveActivityTokenRepositoryStaleSweepTests : IDisposable
{
    private const string UserId = "user-la-sweep";
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 4, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;

    public LiveActivityTokenRepositoryStaleSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new NullTenantProvider());
    }

    private static LiveActivityToken Token(string? orderId, DateTimeOffset lastUpdatedAt)
    {
        var token = LiveActivityToken.Create(UserId, "DEV-1", orderId, $"TOKEN-{orderId ?? "PTS"}", tenantId: null);
        typeof(LiveActivityToken).GetProperty(nameof(LiveActivityToken.LastUpdatedAt))!
            .SetValue(token, lastUpdatedAt);
        return token;
    }

    [Fact]
    public async Task GetStaleOrderScopedTokensAsync_Returns_Only_The_Order_Scoped_Row_Past_Cutoff()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureCreatedAsync();

            ctx.Add(Language.Create("en", "English")); // the User's PreferredLanguageCode FK target
            var user = User.CreateWithPassword("owner@cleansia.test", "Passw0rd!", "Owner", "User");
            user.Id = UserId;
            ctx.Add(user);

            ctx.Add(Token(orderId: null, Now.AddDays(-30)));            // push-to-start, old — must NOT be swept
            ctx.Add(Token(orderId: "ORDER-OLD", Now.AddHours(-25)));    // order-scoped, past cutoff — the ONLY hit
            ctx.Add(Token(orderId: "ORDER-NEW", Now.AddHours(-1)));     // order-scoped, recent — must NOT be swept
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var readCtx = NewContext();
        var stale = await new LiveActivityTokenRepository(readCtx)
            .GetStaleOrderScopedTokensAsync(Now.AddHours(-24), CancellationToken.None);

        var row = Assert.Single(stale);
        Assert.Equal("ORDER-OLD", row.OrderId);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
