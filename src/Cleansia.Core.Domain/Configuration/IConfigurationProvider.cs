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

    /// <summary>
    /// The NAMED company's stored value under <paramref name="key"/>, or null when it holds none —
    /// read past the tenant filter with the company pinned by argument, for a writer whose ambient
    /// tenant may be another company's at the moment it reads (an admin event names the order's
    /// company while the caller's override may name the webhook's).
    /// </summary>
    Task<string?> GetTenantSettingAsync(string tenantId, string key, CancellationToken cancellationToken = default);

    Task<CountryConfiguration?> GetCountryConfigurationAsync(string countryId, CancellationToken cancellationToken = default);
}
