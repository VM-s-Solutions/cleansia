using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database;

public class AppConfigurationProvider(CleansiaDbContext dbContext, ITenantProvider tenantProvider) : IAppConfigurationProvider
{
    public async Task<string?> GetTenantSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var config = await dbContext.TenantConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        return config?.Value;
    }

    public async Task<CountryConfiguration?> GetCountryConfigurationAsync(string countryId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CountryConfigurations
            .AsNoTracking()
            .Include(c => c.Country)
            .FirstOrDefaultAsync(c => c.CountryId == countryId, cancellationToken);
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureName, string? countryId = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var currentTenantId = tenantId ?? tenantProvider.GetCurrentTenantId();

        // Check tenant-level flag first (most specific)
        if (!string.IsNullOrEmpty(currentTenantId))
        {
            var tenantFlag = await dbContext.FeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Name == featureName && f.Scope == "tenant" && f.ScopeValue == currentTenantId, cancellationToken);

            if (tenantFlag != null)
                return tenantFlag.IsEnabled;
        }

        // Check country-level flag
        if (!string.IsNullOrEmpty(countryId))
        {
            var countryFlag = await dbContext.FeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Name == featureName && f.Scope == "country" && f.ScopeValue == countryId, cancellationToken);

            if (countryFlag != null)
                return countryFlag.IsEnabled;
        }

        // Fall back to global flag
        var globalFlag = await dbContext.FeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name == featureName && f.Scope == "global", cancellationToken);

        return globalFlag?.IsEnabled ?? false;
    }
}
