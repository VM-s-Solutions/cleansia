namespace Cleansia.Core.Domain.Configuration;

public interface IAppConfigurationProvider
{
    Task<string?> GetTenantSettingAsync(string key, CancellationToken cancellationToken = default);

    Task<CountryConfiguration?> GetCountryConfigurationAsync(string countryId, CancellationToken cancellationToken = default);

    Task<bool> IsFeatureEnabledAsync(string featureName, string? countryId = null, string? tenantId = null, CancellationToken cancellationToken = default);
}
