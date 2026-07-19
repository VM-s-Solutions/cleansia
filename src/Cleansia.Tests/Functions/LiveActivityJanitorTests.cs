using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0029 D3 (cleanup path 3) — the 24h stale-activity janitor. TC-LA-4 (janitor scope): only
/// order-scoped rows older than the max-lifetime are reclaimed; push-to-start rows are never swept.
/// </summary>
public class LiveActivityJanitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 4, 0, 0, TimeSpan.Zero);

    private static LiveActivityToken Row(string? orderId, DateTimeOffset lastUpdatedAt)
    {
        var token = LiveActivityToken.Create("USER-1", "DEV-1", orderId, "TOKEN", "TENANT-A");
        typeof(LiveActivityToken).GetProperty(nameof(LiveActivityToken.LastUpdatedAt))!
            .SetValue(token, lastUpdatedAt);
        return token;
    }

    // ── the cutoff window ──────────────────────────────────────────────────────────

    [Fact]
    public void StaleCutoff_Is_24h_Before_Now()
    {
        Assert.Equal(Now.AddHours(-24), LiveActivityJanitorPolicy.StaleCutoff(Now));
    }

    // ── the handler ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handler_Reclaims_The_Stale_Rows_The_Repo_Returns()
    {
        var repo = new Mock<ILiveActivityTokenRepository>();
        var stale = new List<LiveActivityToken> { Row("ORDER-1", Now.AddHours(-25)), Row("ORDER-2", Now.AddHours(-40)) };
        repo.Setup(r => r.GetStaleOrderScopedTokensAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);

        var handler = new LiveActivityJanitorTimerHandler(repo.Object, NullLogger<LiveActivityJanitorTimerHandler>.Instance);
        await handler.HandleAsync(CancellationToken.None);

        repo.Verify(r => r.RemoveRange(stale), Times.Once);
        repo.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handler_Queries_With_A_24h_Cutoff()
    {
        var repo = new Mock<ILiveActivityTokenRepository>();
        DateTimeOffset captured = default;
        repo.Setup(r => r.GetStaleOrderScopedTokensAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((cutoff, _) => captured = cutoff)
            .ReturnsAsync(new List<LiveActivityToken>());

        var handler = new LiveActivityJanitorTimerHandler(repo.Object, NullLogger<LiveActivityJanitorTimerHandler>.Instance);
        await handler.HandleAsync(CancellationToken.None);

        // The cutoff is ~24h before "now"; assert it lands inside a tight window around now-24h.
        var expected = DateTimeOffset.UtcNow.AddHours(-24);
        Assert.True(Math.Abs((expected - captured).TotalMinutes) < 5, $"cutoff {captured} was not ~24h before now");
    }

    [Fact]
    public async Task Handler_With_No_Stale_Rows_Does_Not_Commit()
    {
        var repo = new Mock<ILiveActivityTokenRepository>();
        repo.Setup(r => r.GetStaleOrderScopedTokensAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveActivityToken>());

        var handler = new LiveActivityJanitorTimerHandler(repo.Object, NullLogger<LiveActivityJanitorTimerHandler>.Instance);
        await handler.HandleAsync(CancellationToken.None);

        repo.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<LiveActivityToken>>()), Times.Never);
        repo.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
