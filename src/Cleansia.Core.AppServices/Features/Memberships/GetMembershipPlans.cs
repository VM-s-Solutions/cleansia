using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// The active membership plans on sale in ONE market, priced in that market's currency. A plan with
/// no price row in the currency is not listed, so an empty list means Plus is not on sale there yet.
///
/// Anonymous-friendly — the marketing page renders plan pricing before the user signs in. The
/// subscribe flow resolves the currency the same way, so the figure shown is the figure charged.
/// </summary>
public class GetMembershipPlans
{
    /// <param name="CountryId">The market; null is the platform default market.</param>
    public record Query(string? CountryId = null) : IQuery<IReadOnlyList<Response>>;

    public record Response(
        string Code,
        string Name,
        decimal Price,
        decimal MonthlyEquivalentPrice,
        int BillingInterval,
        decimal DiscountPercentage,
        int FreeCancellationWindowHours,
        bool AllowsExpressUpgrade,
        /// <summary>
        /// How many express surcharges the plan waives per calendar month. On the
        /// DTO because a pre-subscribe surface has to be able to SAY the number —
        /// "a free express clean each month" is a promise with a quantity in it,
        /// and the alternative was hardcoding one in five locales on three
        /// platforms. 0 when <see cref="AllowsExpressUpgrade"/> is false.
        /// </summary>
        int ExpressUpgradesPerMonth,
        int TrialPeriodDays,
        /// <summary>
        /// Percentage saved per month when this plan is compared to the cheapest monthly plan in the
        /// same currency. 0 for monthly plans themselves. Drives the "Save 15%" badge on the yearly toggle.
        /// </summary>
        decimal SavingsPercentVsMonthly,
        /// <summary>The currency every money figure on this row is in — the market's, never the platform default's.</summary>
        string CurrencyCode);

    public class Handler(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyResolutionService currencyResolutionService,
        ICountryRepository countryRepository)
        : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<BusinessResult<IReadOnlyList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            // An unserviced or unknown country has no Plus on sale: an empty answer, never the default
            // market's prices under a foreign address and never the resolver's throw as a 500.
            if (query.CountryId is not null
                && !await countryRepository.IsServicedAsync(query.CountryId, cancellationToken))
            {
                return BusinessResult.Success<IReadOnlyList<Response>>([]);
            }

            var currency = await currencyResolutionService.ResolveCurrencyForCountryAsync(query.CountryId, cancellationToken);
            var plans = await membershipPlanRepository.GetActivePlansAsync(cancellationToken);
            var prices = await membershipPlanPriceRepository.GetForPlansAsync(
                plans.Select(p => p.Id).ToList(), currency.Id, cancellationToken);

            var priced = plans
                .Where(p => prices.ContainsKey(p.Id))
                .Select(p => (Plan: p, Price: prices[p.Id].Price))
                .ToList();

            var monthlyBaseline = priced
                .Where(x => x.Plan.BillingInterval == BillingInterval.Monthly)
                .Select(x => x.Price)
                .DefaultIfEmpty(0m)
                .Min();

            var responses = priced.Select(x =>
            {
                var monthlyEquivalent = x.Plan.MonthlyEquivalentOf(x.Price);
                var savings = monthlyBaseline > 0m && x.Plan.BillingInterval == BillingInterval.Yearly
                    ? Math.Round((1m - monthlyEquivalent / monthlyBaseline) * 100m, 0)
                    : 0m;

                return new Response(
                    Code: x.Plan.Code,
                    Name: x.Plan.Name,
                    Price: x.Price,
                    MonthlyEquivalentPrice: monthlyEquivalent,
                    BillingInterval: (int)x.Plan.BillingInterval,
                    DiscountPercentage: x.Plan.DiscountPercentage,
                    FreeCancellationWindowHours: x.Plan.FreeCancellationWindowHours,
                    AllowsExpressUpgrade: x.Plan.AllowsExpressUpgrade,
                    ExpressUpgradesPerMonth: x.Plan.AllowsExpressUpgrade ? x.Plan.ExpressUpgradesPerMonth : 0,
                    TrialPeriodDays: x.Plan.TrialPeriodDays,
                    SavingsPercentVsMonthly: savings,
                    CurrencyCode: currency.Code);
            }).ToList();

            return BusinessResult.Success<IReadOnlyList<Response>>(responses);
        }
    }
}
