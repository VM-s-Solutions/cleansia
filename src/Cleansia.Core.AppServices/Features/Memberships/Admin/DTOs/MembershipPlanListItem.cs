using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

/// <summary>
/// Row shape for the admin membership-plans table. The two money figures are the platform-default-
/// currency row's and are NULL when the plan has none — rendered as "—", because "0" would read as
/// free, and an unpriced plan in the default currency is a real state.
/// </summary>
public record MembershipPlanListItem(
    string Id,
    string Code,
    string Name,
    BillingInterval BillingInterval,
    decimal? Price,
    decimal? MonthlyEquivalentPrice,
    string CurrencyCode,
    decimal DiscountPercentage,
    int TrialPeriodDays,
    int FreeCancellationWindowHours,
    bool AllowsExpressUpgrade,
    int ExpressUpgradesPerMonth,
    bool IsActive,
    DateTimeOffset CreatedOn);
