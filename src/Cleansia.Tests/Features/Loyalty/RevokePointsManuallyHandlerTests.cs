using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// LG-02 — the manual revoke must record its ledger source as a REVOCATION, not a grant. Pins the
/// source the handler threads into <see cref="ILoyaltyService.RevokePointsManuallyAsync"/>: pre-fix it
/// passed <see cref="LoyaltyEarnSource.ManualGrant"/> (the audit-trail corruption); post-fix it passes
/// the new <see cref="LoyaltyEarnSource.ManualRevoke"/>. Every other argument the handler forwards is
/// pinned unchanged.
/// </summary>
public class RevokePointsManuallyHandlerTests
{
    private const string UserId = "user-1";
    private const string ActorId = "admin-7";
    private const string Reason = "fraud reversal #91";
    private const string RequestId = "req-revoke-abc";
    private const int Points = 320;

    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RevokePointsManuallyHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private RevokePointsManually.Handler CreateHandler() =>
        new(_loyaltyService.Object, _session.Object);

    private static RevokePointsManually.Command ValidCommand() =>
        new(UserId, Points, Reason, RequestId);

    [Fact]
    public async Task Revoke_RecordsSource_AsManualRevoke_NotManualGrant()
    {
        LoyaltyEarnSource? capturedSource = null;
        _loyaltyService
            .Setup(s => s.RevokePointsManuallyAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<LoyaltyEarnSource>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, LoyaltyEarnSource, string?, string, string?, string?, CancellationToken>(
                (_, _2, source, _3, _4, _5, _6, _7) => capturedSource = source)
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LoyaltyEarnSource.ManualRevoke, capturedSource);
        Assert.NotEqual(LoyaltyEarnSource.ManualGrant, capturedSource);
    }

    [Fact]
    public async Task Revoke_ForwardsEveryArgument_Unchanged()
    {
        string? capturedUserId = null;
        int capturedPoints = 0;
        string? capturedOrderId = "sentinel";
        string? capturedActorId = null;
        string? capturedReason = null;
        string? capturedRequestId = null;
        _loyaltyService
            .Setup(s => s.RevokePointsManuallyAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<LoyaltyEarnSource>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, LoyaltyEarnSource, string?, string, string?, string?, CancellationToken>(
                (userId, points, _, orderId, actorId, reason, requestId, _2) =>
                {
                    capturedUserId = userId;
                    capturedPoints = points;
                    capturedOrderId = orderId;
                    capturedActorId = actorId;
                    capturedReason = reason;
                    capturedRequestId = requestId;
                })
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, capturedUserId);
        Assert.Equal(Points, capturedPoints);
        Assert.Null(capturedOrderId);
        Assert.Equal(ActorId, capturedActorId);
        Assert.Equal(Reason, capturedReason);
        Assert.Equal(RequestId, capturedRequestId);
        Assert.Equal(UserId, result.Value.UserId);
        Assert.Equal(Points, result.Value.Points);
    }
}
