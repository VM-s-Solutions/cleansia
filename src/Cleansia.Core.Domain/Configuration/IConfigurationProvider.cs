namespace Cleansia.Core.Domain.Configuration;

public interface IAppConfigurationProvider
{
    /// <summary>
    /// The AMBIENT operating company's stored value under <paramref name="key"/>, or null when it holds
    /// none. The company is the request's tenant claim, or the override a job set for the company it is
    /// processing; with neither there is no company and the read answers null, so a job reads a setting
    /// only inside its per-company loop.
    /// </summary>
    Task<string?> GetTenantSettingAsync(string key, CancellationToken cancellationToken = default);

    Task<CountryConfiguration?> GetCountryConfigurationAsync(string countryId, CancellationToken cancellationToken = default);
}
