using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;

public static class MembershipPlanMapper
{
    public static MembershipPlanListItem MapToListItem(this MembershipPlan plan, decimal? price, string currencyCode) =>
        new(
            Id: plan.Id,
            Code: plan.Code,
            Name: plan.Name,
            BillingInterval: plan.BillingInterval,
            Price: price,
            MonthlyEquivalentPrice: price is { } p ? plan.MonthlyEquivalentOf(p) : null,
            CurrencyCode: currencyCode,
            DiscountPercentage: plan.DiscountPercentage,
            TrialPeriodDays: plan.TrialPeriodDays,
            FreeCancellationWindowHours: plan.FreeCancellationWindowHours,
            AllowsExpressUpgrade: plan.AllowsExpressUpgrade,
            ExpressUpgradesPerMonth: plan.ExpressUpgradesPerMonth,
            IsActive: plan.IsActive,
            CreatedOn: plan.CreatedOn);

    public static MembershipPlanDetailDto MapToDetailDto(this MembershipPlan plan, IEnumerable<MembershipPlanPrice> prices) =>
        new(
            Id: plan.Id,
            Code: plan.Code,
            Name: plan.Name,
            BillingInterval: plan.BillingInterval,
            Prices: prices.ToDictionary(
                p => p.Currency!.Code,
                p => new MembershipPlanPriceDto(p.Price, plan.MonthlyEquivalentOf(p.Price), p.StripePriceId)),
            DiscountPercentage: plan.DiscountPercentage,
            TrialPeriodDays: plan.TrialPeriodDays,
            FreeCancellationWindowHours: plan.FreeCancellationWindowHours,
            AllowsExpressUpgrade: plan.AllowsExpressUpgrade,
            ExpressUpgradesPerMonth: plan.ExpressUpgradesPerMonth,
            IsActive: plan.IsActive,
            CreatedOn: plan.CreatedOn,
            UpdatedOn: plan.UpdatedOn);
}
