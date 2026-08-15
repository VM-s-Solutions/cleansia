using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cleansia.Config.Services;

/// <summary>
/// The readiness half of the health split: <c>/alive</c> is liveness only, <c>/health</c> additionally
/// runs dependency checks. **The App Service probe polls <c>/alive</c>, not this** — see below.
///
/// <para><b>Database is still Unhealthy and blob still Degraded-but-200.</b> That distinction is right:
/// every request on an instance with no database fails anyway, while storage is shared by the whole
/// fleet. What changed on 2026-08-15 is who acts on it.</para>
///
/// <para><b>These checks are BOUNDED, and that is not a detail.</b> Unbounded, the database check was
/// measured at <b>95 seconds</b> and blob at <b>56</b> against a saturated dev Postgres. Nothing can use
/// an answer that slow: the deploy warm loop gives each probe 15 seconds, and App Service's probe gives
/// less. A health check that cannot answer within its caller's patience is indistinguishable from one
/// that answers "down" — so it must answer, quickly, and say what it found.</para>
///
/// <para><b>Why App Service no longer restarts on this.</b> Its probe used to poll <c>/health</c>, so a
/// slow shared database recycled the instance — which cold-started onto the same contended plan,
/// rebuilt a 70-entity model and a fresh pool against the same saturated server, and failed the next
/// probe. On 2026-08-15 that loop made both mobile APIs unusable. The blob check's own reasoning —
/// "recycling everything during an outage only amplifies it" — turned out to apply to the database too:
/// the recycle rebuilds a WEDGED pool, but this was never a wedged pool. It was a shared server with no
/// capacity left, and restarting every instance took more of it.
/// → /architecture/infrastructure#health-probes</para>
/// </summary>
public static class ReadinessHealthChecks
{
    /// <summary>
    /// Well under the 15 seconds the deploy warm loop allows, so a bounded check still produces an
    /// answer the caller can act on rather than a timeout it has to interpret.
    /// </summary>
    public static readonly TimeSpan ReadinessCheckTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddCleansiaReadinessChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<CleansiaDbContext>("database", tags: ["ready"])
            .AddCheck<BlobStorageHealthCheck>("blob_storage", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

        // Bound every readiness check. The registrations above take no timeout, so this rewrites them
        // in place rather than duplicating the roster — a second list would drift from the first.
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            var unbounded = options.Registrations
                .Where(r => r.Timeout == Timeout.InfiniteTimeSpan || r.Timeout > ReadinessCheckTimeout)
                .ToList();

            foreach (var existing in unbounded)
            {
                options.Registrations.Remove(existing);
                options.Registrations.Add(new HealthCheckRegistration(
                    existing.Name,
                    existing.Factory,
                    existing.FailureStatus,
                    existing.Tags,
                    ReadinessCheckTimeout));
            }
        });

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
