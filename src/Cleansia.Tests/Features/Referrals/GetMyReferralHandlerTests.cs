using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Referrals;

/// <summary>
/// LG-PERF-02: GetMyReferral used to materialise EVERY referral (with the invitee row included) just
/// to count Qualified/Accepted in memory; it now reads a single grouped count over the indexed
/// ReferrerUserId. This pins the observable response (the same Qualified/Accepted/TimesUsed numbers)
/// and that the handler drives the grouped-count method, not the page-everything path.
/// </summary>
public class GetMyReferralHandlerTests
{
    private const string UserId = "referrer-1";

    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private GetMyReferral.Handler CreateHandler() =>
        new(_referralService.Object, _referralRepository.Object, _session.Object);

    [Fact]
    public async Task Counts_QualifiedAndAccepted_FromGroupedCount_AndDoesNotPageEveryRow()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var code = ReferralCode.Generate(UserId, "X3K9P2", "system");
        code.RecordUse("system");
        code.RecordUse("system");
        code.RecordUse("system");
        _referralService
            .Setup(s => s.EnsureCodeForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);

        _referralRepository
            .Setup(r => r.GetStatusCountsByReferrerAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ReferralStatus, int>
            {
                [ReferralStatus.Qualified] = 2,
                [ReferralStatus.Accepted] = 3,
                [ReferralStatus.Expired] = 1,
            });

        var result = await CreateHandler().Handle(new GetMyReferral.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("X3K9P2", result.Value!.Code);
        Assert.Equal(3, result.Value.TimesUsed);
        Assert.Equal(2, result.Value.QualifiedCount);
        Assert.Equal(3, result.Value.AcceptedCount);
        Assert.Equal(ReferralPolicy.PointsPerSide, result.Value.PointsPerReferral);

        // The over-fetch path is gone: the summary reads the grouped count only.
        _referralRepository.Verify(
            r => r.GetStatusCountsByReferrerAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AbsentStatuses_DefaultToZero()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var code = ReferralCode.Generate(UserId, "AAAA11", "system");
        _referralService
            .Setup(s => s.EnsureCodeForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        _referralRepository
            .Setup(r => r.GetStatusCountsByReferrerAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ReferralStatus, int>());

        var result = await CreateHandler().Handle(new GetMyReferral.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.QualifiedCount);
        Assert.Equal(0, result.Value.AcceptedCount);
        Assert.Equal(0, result.Value.TimesUsed);
    }
}
