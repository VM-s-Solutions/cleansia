using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;

public static class MembershipPlanMapper
{
    public static MembershipPlanListItem MapToListItem(this MembershipPlan plan) =>
        new(
            Id: plan.Id,
            Code: plan.Code,
            Name: plan.Name,
            BillingInterval: plan.BillingInterval,
            MonthlyPriceCzk: plan.MonthlyPriceCzk,
            MonthlyEquivalentPriceCzk: plan.MonthlyEquivalentPriceCzk,
            DiscountPercentage: plan.DiscountPercentage,
            TrialPeriodDays: plan.TrialPeriodDays,
            FreeCancellationWindowHours: plan.FreeCancellationWindowHours,
            AllowsExpressUpgrade: plan.AllowsExpressUpgrade,
            IsActive: plan.IsActive,
            CreatedOn: plan.CreatedOn);

    public static MembershipPlanDetailDto MapToDetailDto(this MembershipPlan plan) =>
        new(
            Id: plan.Id,
            Code: plan.Code,
            Name: plan.Name,
            BillingInterval: plan.BillingInterval,
            MonthlyPriceCzk: plan.MonthlyPriceCzk,
            MonthlyEquivalentPriceCzk: plan.MonthlyEquivalentPriceCzk,
            StripePriceId: plan.StripePriceId,
            DiscountPercentage: plan.DiscountPercentage,
            TrialPeriodDays: plan.TrialPeriodDays,
            FreeCancellationWindowHours: plan.FreeCancellationWindowHours,
            AllowsExpressUpgrade: plan.AllowsExpressUpgrade,
            IsActive: plan.IsActive,
            CreatedOn: plan.CreatedOn,
            UpdatedOn: plan.UpdatedOn);
}
