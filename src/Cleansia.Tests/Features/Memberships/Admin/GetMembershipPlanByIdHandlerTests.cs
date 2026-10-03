using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// The detail tells the edit form whether the discount and the express quota are still editable: they
/// lock once anyone has subscribed to the plan, which is the rule <c>UpdateMembershipPlan</c> enforces.
/// </summary>
public class GetMembershipPlanByIdHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly MembershipPlan _plan;

    public GetMembershipPlanByIdHandlerTests()
    {
        _plan = MembershipPlan.Create("PLUS_MONTHLY", "Plus Monthly", 5m, true, expressUpgradesPerMonth: 2);
        _plan.Id = "plan-1";
        _planRepository.Setup(r => r.GetByIdAsync(_plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _priceRepository
            .Setup(r => r.GetAllForPlanAsync(_plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MembershipPlanPrice>());
    }

    private GetMembershipPlanById.Handler Handler() =>
        new(_planRepository.Object, _priceRepository.Object, _userMembershipRepository.Object);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BenefitsLocked_IsWhetherAnyoneHasSubscribed(bool subscribed)
    {
        _userMembershipRepository
            .Setup(r => r.HasAnyForPlanAsync(_plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscribed);

        var result = await Handler().Handle(new GetMembershipPlanById.Query(_plan.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(subscribed, result.Value.BenefitsLocked);
    }
}
