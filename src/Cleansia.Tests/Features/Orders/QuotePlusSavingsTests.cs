using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Loyalty;
using Moq;
using Xunit;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// What Cleansia Plus would be worth on a basket the caller has no membership for.
///
/// The three things these pin are the three a browser cannot do: the 12% combined cap
/// on membership + tier, the express gross-up, and the fact that the answer is the
/// DIFFERENCE the plan makes rather than the plan's percentage in isolation.
/// </summary>
public class QuotePlusSavingsTests
{
    private const decimal Subtotal = 1000m;

    [Fact]
    public async Task Reports_The_Plans_Percentage_When_Nothing_Else_Discounts()
    {
        var handler = Build(discountPercent: 5m, tierDiscount: 0m);

        var result = await handler.Handle(QueryFor("PLUS_MONTHLY"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Value!.WouldSaveAmount);
        Assert.Equal(950m, result.Value.WouldPayTotal);
        Assert.Equal(1000m, result.Value.CurrentTotal);
    }

    [Fact]
    public async Task Reports_Only_What_The_Plan_ADDS_When_A_Tier_Discount_Already_Applies()
    {
        // The cap is on the PAIR. A customer already discounted to the ceiling gains
        // nothing from a membership, and telling them they would save 5% would be a
        // number the checkout then refuses to honour.
        var handler = Build(discountPercent: 5m, tierDiscount: 120m);

        var result = await handler.Handle(QueryFor("PLUS_MONTHLY"), CancellationToken.None);

        // 12% of 1000 is the ceiling, and the tier alone already reaches it.
        Assert.Equal(0m, result.Value!.WouldSaveAmount);
    }

    [Fact]
    public async Task Caps_The_Pair_Rather_Than_Adding_The_Membership_On_Top()
    {
        var handler = Build(discountPercent: 5m, tierDiscount: 100m);

        var result = await handler.Handle(QueryFor("PLUS_MONTHLY"), CancellationToken.None);

        // 100 + 50 = 150 would be 15%; the cap holds the pair to 120, so the plan is
        // worth the 20 of headroom left and not its own 50.
        Assert.Equal(20m, result.Value!.WouldSaveAmount);
        Assert.Equal(880m, result.Value.WouldPayTotal);
    }

    [Fact]
    public async Task Refuses_A_Plan_That_Is_Not_On_Offer()
    {
        var handler = Build(discountPercent: 5m, tierDiscount: 0m, planExists: false);

        var result = await handler.Handle(QueryFor("NO_SUCH_PLAN"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Refuses_A_Plan_That_Has_Been_Withdrawn()
    {
        var withdrawn = MakePlan(5m);
        withdrawn.Deactivate();
        var handler = Build(discountPercent: 5m, tierDiscount: 0m, plan: withdrawn);

        var result = await handler.Handle(QueryFor("PLUS_MONTHLY"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Grosses_The_Saving_Up_With_The_Express_Surcharge()
    {
        // The discount is resolved on the raw subtotal and the surcharge applied to
        // what is left — (raw - d) * 1.2, never raw * 1.2 - d. So on an express slot
        // the saving the customer sees is bigger than the plan's flat percentage.
        var handler = Build(discountPercent: 5m, tierDiscount: 0m, expressSurcharge: 200m);

        var result = await handler.Handle(QueryFor("PLUS_MONTHLY"), CancellationToken.None);

        Assert.Equal(60m, result.Value!.WouldSaveAmount);
        Assert.Equal(1200m, result.Value.CurrentTotal);
        Assert.Equal(1140m, result.Value.WouldPayTotal);
    }

    // ── plumbing ────────────────────────────────────────────────────────────────────

    private static QuotePlusSavings.Query QueryFor(string planCode) =>
        new(SelectedServiceIds: ["service-1"], SelectedPackageIds: [], Rooms: 3, Bathrooms: 1,
            PlanCode: planCode);

    private static MembershipPlan MakePlan(decimal discountPercent) =>
        MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_test",
            discountPercentage: discountPercent,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 14,
            expressUpgradesPerMonth: 1);

    private static QuotePlusSavings.Handler Build(
        decimal discountPercent,
        decimal tierDiscount,
        decimal expressSurcharge = 0m,
        MembershipPlan? plan = null,
        bool planExists = true)
    {
        var resolvedPlan = planExists ? plan ?? MakePlan(discountPercent) : null;

        var plans = new Mock<IMembershipPlanRepository>();
        plans.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedPlan);

        var pricing = new Mock<IOrderPricingCalculator>();
        pricing.Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderPricingResult(
                TotalPrice: Subtotal + expressSurcharge,
                CurrencyId: "czk",
                CurrencyCode: "CZK",
                ServicesSubtotal: Subtotal,
                PackagesSubtotal: 0m,
                ExtrasSubtotal: 0m,
                ExpressSurchargeApplied: expressSurcharge > 0m,
                ExpressSurchargeAmount: expressSurcharge));

        var loyalty = new Mock<ILoyaltyService>();
        loyalty.Setup(l => l.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(tierDiscount, null));

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns("user-1");

        return new QuotePlusSavings.Handler(
            pricing.Object, plans.Object, loyalty.Object, session.Object);
    }
}
