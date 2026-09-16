using Cleansia.Core.AppServices.Features.Markets;
using Cleansia.Core.AppServices.Features.Markets.DTOs;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Tenancy;

public sealed class OperatorTenantResolver(
    ICountryRepository countryRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    ICurrencyRepository currencyRepository,
    IRequestHandler<GetMarkets.Request, IReadOnlyList<MarketListItem>> markets,
    ILogger<OperatorTenantResolver> logger) : IOperatorTenantResolver
{
    public async Task<OperatorResolution> ResolveAsync(string? countryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(countryId))
        {
            return await ResolveDefaultMarketAsync(cancellationToken);
        }

        if (!await countryRepository.IsServicedAsync(countryId, cancellationToken))
        {
            return OperatorResolution.NotAMarket;
        }

        var country = await countryRepository.GetByIdAsync(countryId, cancellationToken);
        if (country is null)
        {
            return OperatorResolution.NotAMarket;
        }

        var market = await GetMarkets.ResolveMarketAsync(
            country, countryConfigurationRepository, currencyRepository, logger, cancellationToken);
        return market is null
            ? OperatorResolution.NotAMarket
            : new OperatorResolution(true, market.Configuration.OperatorTenantId);
    }

    // The directory handler is called directly, not through the mediator: it is the one code path that
    // chooses the default market, and re-entering the pipeline from inside a pipeline behaviour buys
    // nothing. A platform with no default market is a configuration defect of the same class as a
    // market nobody operates, so it answers "market, no operator" and the caller refuses tenant.not_found.
    private async Task<OperatorResolution> ResolveDefaultMarketAsync(CancellationToken cancellationToken)
    {
        var listed = await markets.Handle(new GetMarkets.Request(), cancellationToken);
        var defaultMarket = listed.FirstOrDefault(m => m.IsDefault);
        if (defaultMarket is null)
        {
            logger.LogError("No default market is listed; an anonymous request naming no market cannot be scoped to an operator");
            return new OperatorResolution(true, null);
        }

        var configuration = await countryConfigurationRepository.GetByCountryIdAsync(defaultMarket.CountryId, cancellationToken);
        return new OperatorResolution(true, configuration?.OperatorTenantId);
    }
}
