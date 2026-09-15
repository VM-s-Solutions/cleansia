using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

/// <summary>
/// Membership-plan detail view for the admin edit form. <see cref="Prices"/> is keyed by currency code;
/// an absent key is a currency the plan is not priced in (Plus not on sale there), never a zero.
/// </summary>
public record MembershipPlanDetailDto(
    string Id,
    string Code,
    string Name,
    BillingInterval BillingInterval,
    Dictionary<string, MembershipPlanPriceDto> Prices,
    decimal DiscountPercentage,
    int TrialPeriodDays,
    int FreeCancellationWindowHours,
    bool AllowsExpressUpgrade,
    int ExpressUpgradesPerMonth,
    bool IsActive,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
