using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class RegionConnectionStringResolver(IConfiguration configuration) : IRegionConnectionStringResolver
{
    private const string DefaultRegion = "weu";

    public string Resolve(string region)
    {
        // Single shared West-Europe database today: every region resolves to the one connection string.
        // A per-region DB later is a map lookup added here, not a change in any host or handler.
        var connectionString = configuration.GetConnectionString("ConnectionString");

        return connectionString
            ?? throw new InvalidOperationException(
                $"No database connection string is configured for region '{region}'.");
    }

    public string ResolveDefault() => Resolve(DefaultRegion);
}
