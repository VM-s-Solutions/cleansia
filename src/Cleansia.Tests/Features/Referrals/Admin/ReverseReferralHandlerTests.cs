using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// AC1 (clawback) + AC3 (run-twice safety) for the admin referral reversal.
/// Both sides' recorded grants are revoked through the loyalty manual-revoke path
/// with a deterministic per-(referral, side) requestId; the referral moves to the
/// terminal Reversed status. A retry on the already-Reversed row is a guarded
/// no-op — no second clawback. Written TEST-FIRST (RED before the command existed).
/// </summary>
public class ReverseReferralHandlerTests
{
    private const string ReferralId = "ref-1";
    private const string ReferrerUserId = "referrer-1";
    private const string ReferredUserId = "referred-1";
    private const string ActorId = "admin-1";
    private const string Reason = "self-referral ring remediation #88";

    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserSessionProvider> _userSession = new();

    public ReverseReferralHandlerTests()
    {
        _userSession.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private ReverseReferral.Handler CreateHandler() =>
        new(_referralRepository.Object, _loyaltyService.Object, _userSession.Object);

    private static Referral QualifiedReferral(int referrerPts = 150, int referredPts = 150)
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-1", "system");
        referral.MarkQualified("order-1", referrerPts, referredPts, "system");
        referral.Id = ReferralId;
        return referral;
    }

    // AC1 — both ledger revocations land (one per side) and the row is terminal.
    [Fact]
    public async Task Reverse_QualifiedReferral_RevokesBothSides_AndMarksReversed()
    {
        var referral = QualifiedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReferralStatus.Reversed, referral.Status);

        _loyaltyService.Verify(s => s.RevokePointsManuallyAsync(
            ReferrerUserId, 150, LoyaltyEarnSource.Referral, null, ActorId, Reason,
            "referral-reverse:ref-1:referrer", It.IsAny<CancellationToken>()), Times.Once);

        _loyaltyService.Verify(s => s.RevokePointsManuallyAsync(
            ReferredUserId, 150, LoyaltyEarnSource.Referral, null, ActorId, Reason,
            "referral-reverse:ref-1:referred", It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC1 — the admin's reason is threaded into the revoke ledger rows.
    [Fact]
    public async Task Reverse_ThreadsReasonIntoRevoke()
    {
        var referral = QualifiedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        _loyaltyService.Verify(s => s.RevokePointsManuallyAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<string?>(),
            It.IsAny<string>(), Reason, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // AC3 — a retry on an already-Reversed referral is a guarded no-op: NO second clawback.
    [Fact]
    public async Task Reverse_RunTwice_SecondIsGuardedNoOp_NoDoubleClawback()
    {
        var referral = QualifiedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var first = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(ReferralStatus.Reversed, referral.Status);

        var second = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotQualified, second.Error!.Code);

        // Exactly one revoke per side across BOTH invocations — no double clawback.
        _loyaltyService.Verify(s => s.RevokePointsManuallyAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<string?>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // A non-Qualified (e.g. Accepted) referral cannot be reversed.
    [Fact]
    public async Task Reverse_NonQualifiedReferral_IsRejected()
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-1", "system");
        referral.Id = ReferralId;
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotQualified, result.Error!.Code);
        _loyaltyService.VerifyNoOtherCalls();
    }

    // Handler is happy-path only — it never commits (the UoW pipeline owns the commit).
    [Fact]
    public async Task Reverse_Handler_DoesNotCommit()
    {
        var referral = QualifiedReferral();
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
