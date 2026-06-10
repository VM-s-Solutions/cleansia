using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

/// <summary>
/// Membership-plan detail view for the admin edit form. Includes the
/// <see cref="MembershipPlan.StripePriceId"/> (admin-entered, fixable here) and
/// the audit timestamp so the form can show "last updated".
/// </summary>
public record MembershipPlanDetailDto(
    string Id,
    string Code,
    string Name,
    BillingInterval BillingInterval,
    decimal MonthlyPriceCzk,
    decimal MonthlyEquivalentPriceCzk,
    string StripePriceId,
    decimal DiscountPercentage,
    int TrialPeriodDays,
    int FreeCancellationWindowHours,
    bool AllowsExpressUpgrade,
    bool IsActive,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
