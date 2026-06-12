using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// AC2 (force-qualify) + AC3 (run-twice safety). An Accepted referral the admin
/// deems legitimate is qualified: symmetric grants land through the loyalty
/// manual-grant path with a deterministic per-(referral, side) requestId and the
/// status becomes Qualified. A retry on the already-Qualified row is a guarded
/// no-op — no second grant. Written TEST-FIRST.
/// </summary>
public class ForceQualifyReferralHandlerTests
{
    private const string ReferralId = "ref-2";
    private const string ReferrerUserId = "referrer-2";
    private const string ReferredUserId = "referred-2";
    private const string ActorId = "admin-1";
    private const string Reason = "qualifying order completed but auto-path missed it";

    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserSessionProvider> _userSession = new();

    public ForceQualifyReferralHandlerTests()
    {
        _userSession.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private ForceQualifyReferral.Handler CreateHandler() =>
        new(_referralRepository.Object, _loyaltyService.Object, _userSession.Object);

    private static Referral AcceptedReferral()
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-2", "system");
        referral.Id = ReferralId;
        return referral;
    }

    // AC2 — both grants land (one per side) and the status becomes Qualified.
    [Fact]
    public async Task ForceQualify_AcceptedReferral_GrantsBothSides_AndMarksQualified()
    {
        var referral = AcceptedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var result = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(ReferralPolicy.PointsPerSide, referral.PointsAwardedToReferrer);
        Assert.Equal(ReferralPolicy.PointsPerSide, referral.PointsAwardedToReferred);
        // No qualifying order is recorded on a manual force-qualify (no Order to FK to).
        Assert.Null(referral.FirstQualifyingOrderId);

        _loyaltyService.Verify(s => s.GrantPointsManuallyAsync(
            ReferrerUserId, ReferralPolicy.PointsPerSide, LoyaltyEarnSource.Referral, null, ActorId, Reason,
            "referral-qualify:ref-2:referrer", It.IsAny<CancellationToken>()), Times.Once);

        _loyaltyService.Verify(s => s.GrantPointsManuallyAsync(
            ReferredUserId, ReferralPolicy.PointsPerSide, LoyaltyEarnSource.Referral, null, ActorId, Reason,
            "referral-qualify:ref-2:referred", It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC3 — a retry on an already-Qualified referral is a guarded no-op: NO second grant.
    [Fact]
    public async Task ForceQualify_RunTwice_SecondIsGuardedNoOp_NoDoubleGrant()
    {
        var referral = AcceptedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var first = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotAccepted, second.Error!.Code);

        _loyaltyService.Verify(s => s.GrantPointsManuallyAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ForceQualify_Handler_DoesNotCommit()
    {
        var referral = AcceptedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        await CreateHandler().Handle(new ForceQualifyReferral.Command(ReferralId, Reason), CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
