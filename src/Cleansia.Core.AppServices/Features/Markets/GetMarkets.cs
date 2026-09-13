using Cleansia.Core.AppServices.Features.Markets.DTOs;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Markets;

/// <summary>
/// The market directory behind every pre-address customer surface (ADR-0058 D1–D2): each serviced
/// country whose configuration names an ACTIVE currency, with the one whose configuration carries
/// <c>IsDefaultMarket</c> flagged <c>IsDefault</c> (owner ruling 2026-09-13, Q-MARKET-01); when
/// nothing listed is flagged, the one on the platform default currency.
///
/// <para>This is the anonymous read behind the landing page, so it never throws on a configuration
/// state: a serviced country that fails the join is omitted and logged, and the default-market
/// fallback below is deterministic rather than an exception.</para>
/// </summary>
public class GetMarkets
{
    public record Request : IRequest<IReadOnlyList<MarketListItem>>;

    public class Handler(
        ICountryRepository countryRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        ICurrencyRepository currencyRepository,
        ILogger<Handler> logger) : IRequestHandler<Request, IReadOnlyList<MarketListItem>>
    {
        public async Task<IReadOnlyList<MarketListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var countries = await countryRepository.GetServicedAsync(cancellationToken);
            var markets = new List<MarketListItem>(countries.Count);
            var onDefaultCurrency = new List<MarketListItem>();

            foreach (var country in countries)
            {
                var configuration = await countryConfigurationRepository.GetByCountryIdAsync(country.Id, cancellationToken);
                if (string.IsNullOrWhiteSpace(configuration?.DefaultCurrencyCode))
                {
                    logger.LogWarning(
                        "Market {IsoCode} is serviced but has no configuration naming a currency; it is not listed",
                        country.IsoCode);
                    continue;
                }

                var currency = await currencyRepository.GetByCodeAsync(configuration.DefaultCurrencyCode, cancellationToken);
                if (currency is null || !currency.IsActive)
                {
                    logger.LogWarning(
                        "Market {IsoCode} is serviced but its configured currency {CurrencyCode} is {State}; it is not listed",
                        country.IsoCode, configuration.DefaultCurrencyCode, currency is null ? "unknown" : "inactive");
                    continue;
                }

                var market = new MarketListItem(
                    CountryId: country.Id,
                    IsoCode: country.IsoCode,
                    IsoAlpha2: country.IsoAlpha2,
                    Name: country.Name,
                    Translations: country.Translations.ToDictionary(),
                    CurrencyId: currency.Id,
                    CurrencyCode: currency.Code,
                    CurrencySymbol: currency.Symbol,
                    IsDefault: false,
                    NoShowCredit: currency.NoShowCredit,
                    InsuranceCoverageAmount: configuration.InsuranceCoverageAmount);

                markets.Add(market);
                if (currency.IsDefault)
                {
                    onDefaultCurrency.Add(market);
                }
            }

            var defaultMarket = await ChooseFlaggedDefaultAsync(markets, cancellationToken)
                ?? await ChooseDefaultByCurrencyAsync(onDefaultCurrency, cancellationToken);
            if (defaultMarket is not null)
            {
                markets[markets.IndexOf(defaultMarket)] = defaultMarket with { IsDefault = true };
            }

            return markets;
        }

        /// <summary>
        /// The explicit flag wins whenever the flagged country is listed. A flag on a country that is
        /// not listed, or no flag at all, is a configuration state the owner should see — logged, then
        /// the currency rule decides.
        /// </summary>
        private async Task<MarketListItem?> ChooseFlaggedDefaultAsync(List<MarketListItem> markets, CancellationToken cancellationToken)
        {
            var flagged = await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken);
            if (flagged is null)
            {
                logger.LogError(
                    "No country configuration is flagged as the default market; falling back to the default-currency rule");
                return null;
            }

            var market = markets.FirstOrDefault(m => m.CountryId == flagged.CountryId);
            if (market is null)
            {
                logger.LogError(
                    "Country {IsoCode} is flagged as the default market but is not a listed market; falling back to the default-currency rule",
                    flagged.Country?.IsoCode ?? flagged.CountryId);
            }

            return market;
        }

        /// <summary>
        /// The default market is a PRE-SELECTION, not a pricing invariant, so neither of the odd
        /// states throws (ADR-0058 D2). Several markets on the default currency is the ordinary state
        /// once EUR is the default and two EUR countries are serviced: the lowest ISO code wins and
        /// the log names every candidate. None is a serviced-country gap: nothing is flagged and the
        /// clients fall to the first listed market.
        /// </summary>
        private async Task<MarketListItem?> ChooseDefaultByCurrencyAsync(List<MarketListItem> onDefaultCurrency, CancellationToken cancellationToken)
        {
            if (onDefaultCurrency.Count == 0)
            {
                var defaultCurrency = await GetDefaultCurrencyOrNullAsync(cancellationToken);
                if (defaultCurrency is null)
                {
                    logger.LogError("The platform has no default currency; no market is flagged as the default");
                }
                else
                {
                    logger.LogError(
                        "No listed market is on the platform default currency {CurrencyCode}; no market is flagged as the default",
                        defaultCurrency.Code);
                }

                return null;
            }

            if (onDefaultCurrency.Count == 1)
            {
                return onDefaultCurrency[0];
            }

            var ordered = onDefaultCurrency.OrderBy(m => m.IsoCode, StringComparer.Ordinal).ToList();
            logger.LogError(
                "{Count} listed markets share the platform default currency {CurrencyCode} ({Candidates}); {Chosen} is flagged as the default by lowest ISO code",
                ordered.Count, ordered[0].CurrencyCode, string.Join(", ", ordered.Select(m => m.IsoCode)), ordered[0].IsoCode);
            return ordered[0];
        }

        /// <summary>
        /// <see cref="ICurrencyRepository.GetDefaultAsync"/> throws for its pricing callers, where a
        /// missing default is fatal; here it is one more configuration state to log.
        /// </summary>
        private async Task<Currency?> GetDefaultCurrencyOrNullAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await currencyRepository.GetDefaultAsync(cancellationToken);
            }
            catch (EntityNotFoundException)
            {
                return null;
            }
        }
    }
}
