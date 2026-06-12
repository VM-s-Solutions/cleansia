using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// AC5 — deactivate is a soft delete (IsActive=false via Deactivate()) and is
/// idempotent: a second call on an already-inactive plan succeeds without error.
/// Written TEST-FIRST.
/// </summary>
public class DeactivateMembershipPlanHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();

    private DeactivateMembershipPlan.Handler CreateHandler() => new(_planRepository.Object);

    private static MembershipPlan ActivePlan() =>
        MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus_monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);

    [Fact]
    public async Task Deactivate_ActivePlan_SetsInactive()
    {
        var plan = ActivePlan();
        _planRepository
            .Setup(r => r.GetByIdAsync("plan-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await CreateHandler().Handle(new DeactivateMembershipPlan.Command("plan-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(plan.IsActive);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactive_IsIdempotentNoError()
    {
        var plan = ActivePlan();
        plan.Deactivate();
        _planRepository
            .Setup(r => r.GetByIdAsync("plan-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await CreateHandler().Handle(new DeactivateMembershipPlan.Command("plan-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(plan.IsActive);
    }
}
