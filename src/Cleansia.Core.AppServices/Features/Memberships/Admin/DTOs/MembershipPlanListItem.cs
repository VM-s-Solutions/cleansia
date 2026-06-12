using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

/// <summary>
/// Row shape for the admin membership-plans table. Mirrors the persisted plan
/// plus the computed <see cref="MembershipPlan.MonthlyEquivalentPriceCzk"/> so
/// the table can show the per-month figure for yearly plans without recomputing.
/// </summary>
public record MembershipPlanListItem(
    string Id,
    string Code,
    string Name,
    BillingInterval BillingInterval,
    decimal MonthlyPriceCzk,
    decimal MonthlyEquivalentPriceCzk,
    decimal DiscountPercentage,
    int TrialPeriodDays,
    int FreeCancellationWindowHours,
    bool AllowsExpressUpgrade,
    bool IsActive,
    DateTimeOffset CreatedOn);
