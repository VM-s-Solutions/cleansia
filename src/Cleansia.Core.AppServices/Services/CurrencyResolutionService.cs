using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

public sealed class CurrencyResolutionService(
    IEmployeeRepository employeeRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    ICurrencyRepository currencyRepository) : ICurrencyResolutionService
{
    public async Task<Currency> ResolveCurrencyForEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Employee '{employeeId}' does not exist; no currency can be resolved for them.");

        // A cleaner is paid in the currency of the country they work in, and ApproveEmployee is the
        // only writer of Approved: it refuses without a serviced work country. So a cleaner with none
        // has never worked and has no pay to label -- there is nothing to guess, and guessing the
        // platform default would label money in a currency the cleaner is not paid in.
        if (string.IsNullOrEmpty(employee.WorkCountryId))
        {
            throw new InvalidOperationException(
                $"Employee '{employeeId}' has no work country; a cleaner is paid in the currency of the country they work in and there is nothing to fall back to.");
        }

        return await ResolveCurrencyForCountryAsync(employee.WorkCountryId, cancellationToken);
    }

    public async Task<Currency> ResolveCurrencyForCountryAsync(
        string? countryId,
        CancellationToken cancellationToken)
    {
        if (countryId is null)
        {
            return await currencyRepository.GetDefaultAsync(cancellationToken);
        }

        var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);

        // A named country resolves to ITS currency or to nothing (owner ruling 2026-09-12, "throw
        // instead 100%"): a country with no configuration, a blank code, or a code that names no
        // Currency row is a configuration defect, and a defect that resolves to the platform default
        // prices a booking or labels a cleaner's pay in the wrong unit while looking healthy.
        // `DefaultCurrencyCode` is free text with no FK -- three characters the seed authors -- which
        // is why the code is looked up here rather than trusted.
        //
        // Deliberately NOT filtered on IsActive: EUR is seeded real-but-inactive, and a country
        // configured for it resolves to EUR; the offerability gate is a separate question.
        if (string.IsNullOrWhiteSpace(countryConfig?.DefaultCurrencyCode))
        {
            throw new InvalidOperationException(
                $"Country '{countryId}' has no default currency configured (DefaultCurrencyCode '{countryConfig?.DefaultCurrencyCode}').");
        }

        return await currencyRepository.GetByCodeAsync(countryConfig.DefaultCurrencyCode, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Country '{countryId}' is configured for currency code '{countryConfig.DefaultCurrencyCode}', which names no Currency.");
    }
}
