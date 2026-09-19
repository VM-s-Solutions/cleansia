using Cleansia.Core.Domain.Configuration;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

public static class TenantSettingReader
{
    /// <summary>
    /// The ambient company's value for <paramref name="setting"/>, or its catalogue default when the
    /// company holds no row or holds one the catalogue no longer accepts.
    /// </summary>
    public static async Task<T> GetAsync<T>(
        this IAppConfigurationProvider provider,
        TenantSettingDefinition<T> setting,
        CancellationToken cancellationToken)
    {
        var stored = await provider.GetTenantSettingAsync(setting.Key, cancellationToken);
        return setting.Resolve(stored);
    }

    /// <summary>
    /// The NAMED company's value for <paramref name="setting"/>, or its catalogue default — the read
    /// for a writer that must not trust the ambient tenant.
    /// </summary>
    public static async Task<T> GetAsync<T>(
        this IAppConfigurationProvider provider,
        string tenantId,
        TenantSettingDefinition<T> setting,
        CancellationToken cancellationToken)
    {
        var stored = await provider.GetTenantSettingAsync(tenantId, setting.Key, cancellationToken);
        return setting.Resolve(stored);
    }
}
