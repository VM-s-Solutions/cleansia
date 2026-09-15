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
}
