using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Referrals;

/// <summary>
/// The referral-expiry sweep flips Accepted referrals past the 90-day qualifying window to Expired
/// via the rich-domain <see cref="Referral.MarkExpired"/>; the cutoff is honoured exactly by the
/// repository's <see cref="IReferralRepository.GetExpirableAsync"/> filter (the handler does not
/// re-evaluate dates). Re-running the sweep is a no-op because already-terminal rows never appear in
/// the expirable set (idempotency — S7 / testing.md must-cover #6). The handler is happy-path only and
/// never commits — the UoW pipeline commits the command.
/// </summary>
public class ExpireStaleReferralsHandlerTests
{
    private const string SystemActor = "system";

    private readonly Mock<IReferralRepository> _referralRepository = new();

    private ExpireStaleReferrals.Handler CreateHandler() =>
        new(_referralRepository.Object, NullLogger<ExpireStaleReferrals.Handler>.Instance);

    private static Referral AcceptedReferral(int acceptedDaysAgo)
    {
        var referral = Referral.CreateAccepted(
            referrerUserId: "referrer-1",
            referredUserId: $"referred-{acceptedDaysAgo}",
            referralCodeId: "code-1",
            actorId: SystemActor);
        // AcceptedOn is set to UtcNow by the factory; the cutoff is enforced by the repository query,
        // which is mocked here, so the in-window vs out-of-window distinction is expressed by what the
        // repository returns (AC2 below), not by mutating AcceptedOn.
        return referral;
    }

    private void ArrangeExpirable(params Referral[] expirable) =>
        _referralRepository
            .Setup(r => r.GetExpirableAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expirable);

    // AC1 — a stale Accepted referral (returned by GetExpirableAsync) is transitioned to Expired.
    [Fact]
    public async Task Sweep_StaleAcceptedReferral_TransitionsToExpired()
    {
        var stale = AcceptedReferral(acceptedDaysAgo: 100);
        ArrangeExpirable(stale);

        var result = await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.ExpiredCount);
        Assert.Equal(ReferralStatus.Expired, stale.Status);
    }

    // AC1 — the cutoff handed to the repository is exactly UtcNow - QualifyingWindowDays.
    [Fact]
    public async Task Sweep_UsesQualifyingWindowCutoff()
    {
        ArrangeExpirable();
        var before = DateTimeOffset.UtcNow.AddDays(-ReferralPolicy.QualifyingWindowDays);

        await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);

        var after = DateTimeOffset.UtcNow.AddDays(-ReferralPolicy.QualifyingWindowDays);
        _referralRepository.Verify(r => r.GetExpirableAsync(
            It.Is<DateTimeOffset>(c => c >= before && c <= after),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC2 — an in-window Accepted referral is NOT returned by GetExpirableAsync, so it is left untouched.
    [Fact]
    public async Task Sweep_InWindowReferral_LeftUntouched()
    {
        var fresh = AcceptedReferral(acceptedDaysAgo: 1);
        ArrangeExpirable(); // GetExpirableAsync honours the cutoff and returns nothing.

        var result = await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ExpiredCount);
        Assert.Equal(ReferralStatus.Accepted, fresh.Status);
    }

    // AC3 — a second run over already-terminal rows is a no-op (the expirable set is empty), proving the
    // sweep is safe to run twice.
    [Fact]
    public async Task Sweep_SecondRun_AlreadyTerminal_NoFurtherEffect()
    {
        var stale = AcceptedReferral(acceptedDaysAgo: 120);

        // First run: the row is expirable and gets flipped.
        ArrangeExpirable(stale);
        var first = await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);
        Assert.Equal(1, first.Value!.ExpiredCount);
        Assert.Equal(ReferralStatus.Expired, stale.Status);

        // Second run: GetExpirableAsync's Status==Accepted filter no longer returns it.
        ArrangeExpirable();
        var second = await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value!.ExpiredCount);
        Assert.Equal(ReferralStatus.Expired, stale.Status);
    }

    // The handler must not commit — the UoW command pipeline owns the commit.
    [Fact]
    public async Task Handler_DoesNotCommit()
    {
        ArrangeExpirable(AcceptedReferral(acceptedDaysAgo: 100));

        await CreateHandler().Handle(new ExpireStaleReferrals.Command(), CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
