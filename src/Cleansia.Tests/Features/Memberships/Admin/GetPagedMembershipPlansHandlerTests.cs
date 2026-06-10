using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// AC1 — the admin paged list returns ALL plans (active and inactive), mapped to
/// the list-item shape with the computed monthly-equivalent price, and the
/// PagedData page metadata reflects the repository total. Written TEST-FIRST.
/// </summary>
public class GetPagedMembershipPlansHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();

    private GetPagedMembershipPlans.Handler CreateHandler() => new(_planRepository.Object);

    private static MembershipPlan Plan(string code, BillingInterval interval, decimal price, bool active)
    {
        var plan = MembershipPlan.Create(
            code: code,
            name: code,
            monthlyPriceCzk: price,
            stripePriceId: $"price_{code}",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: interval,
            trialPeriodDays: 0);
        if (!active)
        {
            plan.Deactivate();
        }
        return plan;
    }

    [Fact]
    public async Task Returns_Active_And_Inactive_Plans_Mapped()
    {
        var plans = new List<MembershipPlan>
        {
            Plan("PLUS_MONTHLY", BillingInterval.Monthly, 199m, active: true),
            Plan("PLUS_YEARLY", BillingInterval.Yearly, 1990m, active: false),
        };
        _planRepository
            .Setup(r => r.GetPagedAdminAsync(null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((plans, 2));

        var result = await CreateHandler().Handle(new GetPagedMembershipPlans.Query(), CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        var data = result.Data.ToList();
        Assert.Equal(2, data.Count);
        Assert.Contains(data, d => d.Code == "PLUS_MONTHLY" && d.IsActive);
        Assert.Contains(data, d => d.Code == "PLUS_YEARLY" && !d.IsActive);
        // Yearly plan's monthly-equivalent is annual / 12.
        var yearly = data.Single(d => d.Code == "PLUS_YEARLY");
        Assert.Equal(Math.Round(1990m / 12m, 2), yearly.MonthlyEquivalentPriceCzk);
    }

    [Fact]
    public async Task PageNumber_Derived_From_Offset_And_Limit()
    {
        _planRepository
            .Setup(r => r.GetPagedAdminAsync(It.IsAny<bool?>(), It.IsAny<string?>(), 40, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<MembershipPlan>(), 100));

        var result = await CreateHandler().Handle(
            new GetPagedMembershipPlans.Query(Offset: 40, Limit: 20), CancellationToken.None);

        Assert.Equal(3, result.PageNumber);
        Assert.Equal(100, result.Total);
    }
}
