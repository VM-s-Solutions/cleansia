using Cleansia.Core.AppServices.Services.Interfaces;
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
        if (employee?.WorkCountryId is { } workCountryId)
        {
            var countryConfig = await countryConfigurationRepository
                .GetByCountryIdAsync(workCountryId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(countryConfig?.DefaultCurrencyCode))
            {
                return countryConfig.DefaultCurrencyCode;
            }
        }

        var defaultCurrency = await currencyRepository.GetDefaultAsync(cancellationToken);
        return defaultCurrency?.Code;
    }
}
