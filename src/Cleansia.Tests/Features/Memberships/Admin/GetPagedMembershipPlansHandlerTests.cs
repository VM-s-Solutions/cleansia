using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// Characterization of the admin membership-plans paged list across the §A
/// canonicalization (record Query + bespoke GetPagedAdminAsync -> Request : DataRangeRequest +
/// MembershipPlanSpecification + GetPagedSort + MapToDto). Pins that the list returns ALL plans
/// (active and inactive) mapped with the computed monthly-equivalent price, the page metadata,
/// and that the active filter + case-insensitive code/name search reach the spec. Default order
/// (BillingInterval, then MonthlyPriceCzk) preserved.
/// </summary>
public class GetPagedMembershipPlansHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();

    private Task<PagedData<MembershipPlanListItem>> Handle(GetPagedMembershipPlans.Request request)
    {
        var handlerType = typeof(GetPagedMembershipPlans).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _planRepository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<MembershipPlanListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

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
        var plans = new[]
        {
            Plan("PLUS_MONTHLY", BillingInterval.Monthly, 199m, active: true),
            Plan("PLUS_YEARLY", BillingInterval.Yearly, 1990m, active: false),
        };
        _planRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _planRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.MembershipPlanSort>(
                0, 20, It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(plans.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedMembershipPlans.Request { Offset = 0, Limit = 20 });

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        var data = result.Data.ToList();
        Assert.Equal(2, data.Count);
        Assert.Contains(data, d => d.Code == "PLUS_MONTHLY" && d.IsActive);
        Assert.Contains(data, d => d.Code == "PLUS_YEARLY" && !d.IsActive);
        var yearly = data.Single(d => d.Code == "PLUS_YEARLY");
        Assert.Equal(Math.Round(1990m / 12m, 2), yearly.MonthlyEquivalentPriceCzk);
    }

    [Fact]
    public async Task PageNumber_Derived_From_Offset_And_Limit()
    {
        _planRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);
        _planRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.MembershipPlanSort>(
                40, 20, It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<MembershipPlan>().AsQueryable().BuildMock());

        var result = await Handle(new GetPagedMembershipPlans.Request { Offset = 40, Limit = 20 });

        Assert.Equal(3, result.PageNumber);
        Assert.Equal(100, result.Total);
    }

    [Fact]
    public async Task Active_Filter_And_Search_Reach_Specification()
    {
        Expression<Func<MembershipPlan, bool>>? captured = null;
        _planRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<MembershipPlan, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _planRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.MembershipPlanSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<MembershipPlan>().AsQueryable().BuildMock());

        await Handle(new GetPagedMembershipPlans.Request
        {
            Active = true,
            Search = "plus",
        });

        Assert.NotNull(captured);
        var predicate = captured!.Compile();

        var activeMatch = Plan("PLUS_MONTHLY", BillingInterval.Monthly, 199m, active: true);
        var inactiveMatch = Plan("PLUS_YEARLY", BillingInterval.Yearly, 1990m, active: false);
        var activeNoMatch = Plan("BASIC", BillingInterval.Monthly, 99m, active: true);

        Assert.True(predicate(activeMatch));
        Assert.False(predicate(inactiveMatch));
        Assert.False(predicate(activeNoMatch));
    }
}
