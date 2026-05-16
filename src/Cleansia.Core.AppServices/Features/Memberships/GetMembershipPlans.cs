using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Returns all active membership plans the customer can subscribe to.
/// Drives the monthly/yearly switcher on the subscribe screen and is also
/// reusable by future "compare plans" surfaces.
///
/// Anonymous-friendly — the marketing page can render plan pricing before
/// the user signs in. The actual subscribe flow remains authenticated.
/// </summary>
public class GetMembershipPlans
{
    public record Query : IQuery<IReadOnlyList<Response>>;

    public record Response(
        string Code,
        string Name,
        decimal Price,
        decimal MonthlyEquivalentPrice,
        int BillingInterval,
        decimal DiscountPercentage,
        int FreeCancellationWindowHours,
        bool AllowsExpressUpgrade,
        int TrialPeriodDays,
        /// <summary>
        /// Percentage saved per month when this plan is compared to the
        /// cheapest monthly plan in the catalog. 0 for monthly plans
        /// themselves. Drives the "Save 15%" badge on the yearly toggle.
        /// </summary>
        decimal SavingsPercentVsMonthly);

    public class Handler(IMembershipPlanRepository membershipPlanRepository)
        : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<BusinessResult<IReadOnlyList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var plans = await membershipPlanRepository.GetActivePlansAsync(cancellationToken);

            // Resolve the baseline (cheapest monthly) once; yearly plans compare
            // their per-month equivalent against it. This way the savings number
            // updates automatically if the monthly price changes — no hardcoded
            // "15%" string anywhere in the UI.
            var monthlyBaseline = plans
                .Where(p => p.BillingInterval == BillingInterval.Monthly)
                .Select(p => p.MonthlyPriceCzk)
                .DefaultIfEmpty(0m)
                .Min();

            var responses = plans.Select(p =>
            {
                var savings = monthlyBaseline > 0m && p.BillingInterval == BillingInterval.Yearly
                    ? Math.Round((1m - p.MonthlyEquivalentPriceCzk / monthlyBaseline) * 100m, 0)
                    : 0m;

                return new Response(
                    Code: p.Code,
                    Name: p.Name,
                    Price: p.MonthlyPriceCzk,
                    MonthlyEquivalentPrice: p.MonthlyEquivalentPriceCzk,
                    BillingInterval: (int)p.BillingInterval,
                    DiscountPercentage: p.DiscountPercentage,
                    FreeCancellationWindowHours: p.FreeCancellationWindowHours,
                    AllowsExpressUpgrade: p.AllowsExpressUpgrade,
                    TrialPeriodDays: p.TrialPeriodDays,
                    SavingsPercentVsMonthly: savings);
            }).ToList();

            return BusinessResult.Success<IReadOnlyList<Response>>(responses);
        }
    }
}
