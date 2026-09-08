using Cleansia.Core.Domain.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database;

public class AppConfigurationProvider(CleansiaDbContext dbContext) : IAppConfigurationProvider
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
}
