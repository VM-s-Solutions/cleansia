using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Locks the B5 contract (consistency.md §B5) on the remaining membership handlers: the
/// <see cref="BusinessErrorMessage.MembershipNotFound"/> failure names the OFFENDING field (the
/// session-derived user whose active membership was not found), never <c>nameof(Command)</c>. The
/// returned <see cref="BusinessErrorMessage"/> value is unchanged.
/// </summary>
public class MembershipB5ContractLockTests
{
    private const string UserId = "user-1";
    private const string NewPlanCode = "PLUS_YEARLY";

    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public MembershipB5ContractLockTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private CancelMembershipSubscription.Handler CancelHandler() =>
        new(
            _membershipRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CancelMembershipSubscription.Handler>.Instance);

    private SwapMembershipPlan.Handler SwapHandler() =>
        new(
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<SwapMembershipPlan.Handler>.Instance);

    [Fact]
    public async Task Cancel_MembershipNotFound_NamesOffendingField_NotCommand()
    {
        var result = await CancelHandler().Handle(
            new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipNotFound, result.Error!.Message);
        Assert.Equal("userId", result.Error.Code);
        Assert.NotEqual(nameof(CancelMembershipSubscription.Command), result.Error.Code);
    }

    [Fact]
    public async Task Swap_MembershipNotFound_NamesOffendingField_NotCommand()
    {
        var result = await SwapHandler().Handle(
            new SwapMembershipPlan.Command(NewPlanCode), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipNotFound, result.Error!.Message);
        Assert.Equal("userId", result.Error.Code);
        Assert.NotEqual(nameof(SwapMembershipPlan.Command), result.Error.Code);
    }
}
