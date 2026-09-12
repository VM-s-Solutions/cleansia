using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

public sealed class CurrencyResolutionService(
    IEmployeeRepository employeeRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    ICurrencyRepository currencyRepository) : ICurrencyResolutionService
{
    public async Task<string?> ResolveCurrencyCodeForEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        var currency = await ResolveCurrencyForWorkCountryAsync(employee?.WorkCountryId, cancellationToken);
        return currency.Code;
    }

    public async Task<Currency> ResolveCurrencyForWorkCountryAsync(
        string? workCountryId,
        CancellationToken cancellationToken)
    {
        if (workCountryId is not null)
        {
            var countryConfig = await countryConfigurationRepository
                .GetByCountryIdAsync(workCountryId, cancellationToken);
            // THE CODE HAS TO NAME A REAL CURRENCY. `CountryConfiguration.DefaultCurrencyCode` is
            // free text with no FK -- three characters an admin types -- and this is its only reader,
            // so an unrecognised value used to travel straight out to a DTO and label money in a
            // currency the platform does not have. Resolving it here turns a typo, or a currency that
            // was deleted after the country was configured, into the platform default rather than a
            // dangling label.
            //
            // Deliberately NOT filtered on IsActive: EUR is seeded real-but-inactive, and a country
            // configured for it should resolve to EUR the moment it is switched on rather than read
            // as broken until then.
            //
            // The alternative -- a real FK on CountryConfiguration -- was weighed and is the wrong
            // shape today: nothing in the platform WRITES this column (no admin command carries it,
            // the seed authors it), so a schema constraint would guard a path that does not exist
            // while the one path that does exist would still hand out whatever it read.
            if (!string.IsNullOrWhiteSpace(countryConfig?.DefaultCurrencyCode))
            {
                var configured = await currencyRepository.GetByCodeAsync(
                    countryConfig.DefaultCurrencyCode, cancellationToken);
                if (configured is not null)
                {
                    return configured;
                }
            }
        }

        return await currencyRepository.GetDefaultAsync(cancellationToken);
    }
}
