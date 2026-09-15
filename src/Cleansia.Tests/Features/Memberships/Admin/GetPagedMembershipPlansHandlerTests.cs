using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// The admin plan list returns ALL plans (active and inactive) with the platform-default-currency
/// row's figures — null, not zero, when a plan has none — plus the page metadata, and the active
/// filter + case-insensitive code/name search reach the specification.
/// </summary>
public class GetPagedMembershipPlansHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    public GetPagedMembershipPlansHandlerTests()
    {
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MembershipPricingMockFactory.Czk());
        _priceRepository
            .Setup(r => r.GetForPlansAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private Task<PagedData<MembershipPlanListItem>> Handle(GetPagedMembershipPlans.Request request)
    {
        var handlerType = typeof(GetPagedMembershipPlans).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _planRepository.Object, _priceRepository.Object, _currencyRepository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<MembershipPlanListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static MembershipPlan Plan(string code, BillingInterval interval, bool active)
    {
        var plan = MembershipPlan.Create(
            code: code,
            name: code,
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: interval,
            trialPeriodDays: 0);
        plan.Id = $"plan-{code}";
        if (!active)
        {
            plan.Deactivate();
        }
        return plan;
    }

    [Fact]
    public async Task Returns_Active_And_Inactive_Plans_WithTheDefaultCurrencysRow_OrNullWhenAbsent()
    {
        var plans = new[]
        {
            Plan("PLUS_MONTHLY", BillingInterval.Monthly, active: true),
            Plan("PLUS_YEARLY", BillingInterval.Yearly, active: false),
        };
        _planRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _planRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.MembershipPlanSort>(
                0, 20, It.IsAny<Expression<Func<MembershipPlan, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(plans.AsQueryable().BuildMock());
        _priceRepository
            .Setup(r => r.GetForPlansAsync(It.IsAny<IReadOnlyCollection<string>>(), MembershipPricingMockFactory.CzkCurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, MembershipPlanPrice>
            {
                [plans[1].Id] = MembershipPlanPrice.Create(plans[1].Id, MembershipPricingMockFactory.CzkCurrencyId, 1990m, "price_y"),
            });

        var result = await Handle(new GetPagedMembershipPlans.Request { Offset = 0, Limit = 20 });

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        var data = result.Data.ToList();
        Assert.Equal(2, data.Count);
        Assert.All(data, d => Assert.Equal("CZK", d.CurrencyCode));
        var monthly = data.Single(d => d.Code == "PLUS_MONTHLY");
        Assert.True(monthly.IsActive);
        Assert.Null(monthly.Price);
        Assert.Null(monthly.MonthlyEquivalentPrice);
        var yearly = data.Single(d => d.Code == "PLUS_YEARLY");
        Assert.False(yearly.IsActive);
        Assert.Equal(1990m, yearly.Price);
        Assert.Equal(Math.Round(1990m / 12m, 2), yearly.MonthlyEquivalentPrice);
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

        Assert.True(predicate(Plan("PLUS_MONTHLY", BillingInterval.Monthly, active: true)));
        Assert.False(predicate(Plan("PLUS_YEARLY", BillingInterval.Yearly, active: false)));
        Assert.False(predicate(Plan("BASIC", BillingInterval.Monthly, active: true)));
    }
}
