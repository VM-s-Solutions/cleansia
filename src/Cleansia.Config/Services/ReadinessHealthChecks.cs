using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cleansia.Config.Services;

/// <summary>
/// The READINESS half of the health split. <c>/alive</c> keeps only the trivial "self" check
/// (liveness: the process serves requests); <c>/health</c> — the path every API site's App Service
/// probe polls (the appService Bicep module's default) — additionally runs these dependency checks,
/// so an instance whose Postgres connection is broken stops answering 200 and gets routed around /
/// recycled instead of serving "no data" screens indefinitely.
///
/// Failure semantics are deliberate: the DATABASE check reports Unhealthy (non-200 — every request
/// on this instance would fail anyway, and a recycle can rebuild a wedged connection pool), while
/// the BLOB check reports Degraded (still 200 — storage is an external dependency shared by all
/// instances, and recycling the whole fleet during a storage outage only amplifies it; Degraded
/// keeps the signal visible in the health payload without triggering the probe).
/// </summary>
public static class ReadinessHealthChecks
{
    public static IServiceCollection AddCleansiaReadinessChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<CleansiaDbContext>("database", tags: ["ready"])
            .AddCheck<BlobStorageHealthCheck>("blob_storage", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

        return services;
    }
}

/// <summary>
/// One HEAD round trip to the storage account: an <c>ExistsAsync</c> on a reserved, never-written
/// blob name in the receipts container. The blob never existing is fine — a clean "false" proves
/// auth + network + account are reachable; only a thrown exception marks the check down. Hosts
/// without the blob factory registered (none today, defensively) report Healthy rather than
/// failing readiness over a dependency they do not use.
/// </summary>
internal sealed class BlobStorageHealthCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    private const string ProbeBlobName = "health-probe-does-not-exist";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var factory = serviceProvider.GetService<IBlobContainerClientFactory>();
        if (factory is null)
        {
            return HealthCheckResult.Healthy("blob storage is not configured on this host");
        }

        try
        {
            var client = factory.GetBlobContainerClient(Constants.BlobContainers.GeneratedReceipts);
            _ = await client.ExistsAsync(ProbeBlobName, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("blob storage is unreachable", ex);
        }
    }
}
