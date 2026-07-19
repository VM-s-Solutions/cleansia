using Azure.Storage.Queues;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Config.Health;

/// <summary>One dependency probe result. <see cref="Detail"/> is an exception TYPE name only — never a
/// message (S6: connection strings / SAS tokens surface in ADO/Npgsql exception messages).</summary>
public sealed record HealthProbe(string Name, bool Ok, string? Detail);

/// <summary>The whole-host health verdict returned by <see cref="FunctionsHealthCheck"/>.</summary>
public sealed record HealthReport(bool Healthy, IReadOnlyList<HealthProbe> Probes);

/// <summary>
/// The Functions host's dependency health check, surfaced over HTTP by <c>HealthFunction</c> and pinged
/// by App Service's <c>healthCheckPath</c>. Two probes cover the two ways the queue-consumer host fails:
/// the worker is up but its <b>database</b> is unreachable (the outbox drainer + every producer write
/// dies) → this returns 503; the worker <b>process itself</b> is down (the 2026-07-18 outage) → the
/// endpoint is unreachable and App Service's own health monitor trips. The <b>queue-storage</b> probe
/// catches the middle case (host up, storage/identity broken) that would otherwise silently stall every
/// trigger. Every probe is best-effort and self-naming: one failing dependency names itself and flips
/// the verdict, but the check never throws.
/// </summary>
public sealed class FunctionsHealthCheck(
    CleansiaDbContext dbContext,
    QueueServiceClient queueServiceClient,
    ILogger<FunctionsHealthCheck> logger)
{
    public async Task<HealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var probes = new[]
        {
            await ProbeAsync("database", async ct =>
            {
                if (!await dbContext.Database.CanConnectAsync(ct))
                {
                    throw new InvalidOperationException("CanConnect returned false");
                }
            }, cancellationToken),
            await ProbeAsync("queue-storage", ct => queueServiceClient.GetPropertiesAsync(ct),
                cancellationToken),
        };

        var healthy = Array.TrueForAll(probes, p => p.Ok);
        if (!healthy)
        {
            // The alert fires off the HTTP 503 / HealthCheckStatus metric; this line is the WHY for
            // whoever opens the logs. Names only, no exception detail beyond the type (S6).
            logger.LogWarning(
                "Functions health check FAILED — unhealthy dependencies: {Failed}",
                string.Join(", ", probes.Where(p => !p.Ok).Select(p => $"{p.Name}({p.Detail})")));
        }

        return new HealthReport(healthy, probes);
    }

    private static async Task<HealthProbe> ProbeAsync(
        string name,
        Func<CancellationToken, Task> probe,
        CancellationToken cancellationToken)
    {
        try
        {
            await probe(cancellationToken);
            return new HealthProbe(name, Ok: true, Detail: null);
        }
        catch (Exception ex)
        {
            return new HealthProbe(name, Ok: false, Detail: ex.GetType().Name);
        }
    }
}
