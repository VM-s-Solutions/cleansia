using System.Diagnostics;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleansia.Config.Database;

/// <summary>
/// Builds the EF Core model once at boot so the first request after a cold start does not.
/// EF compiles the model — 68 entity types, their configurations and the multi-tenancy query filters —
/// lazily on first use and caches it per data source, so without this every deploy, scale event and
/// platform restart hands one user a request that includes the whole build (measured at ~1.7–3.0s in a
/// Debug unit-test process; see BootDatabaseIoTests).
///
/// <para>Three properties, each deliberate:</para>
/// <list type="bullet">
/// <item><description><b>It performs no I/O.</b> Reading <see cref="DbContext.Model"/> is pure metadata —
/// no connection is opened — which is what lets it run to completion while the database is down and keeps
/// it off the readiness path entirely.</description></item>
/// <item><description><b>The host does not wait for it.</b> That comes from <see cref="BackgroundService"/>,
/// which starts <c>ExecuteAsync</c> on the thread pool rather than inline — so converting this to a plain
/// <see cref="IHostedService"/> would silently turn a slow first request into a slow start, the same cost
/// moved rather than removed.</description></item>
/// <item><description><b>Losing it is free.</b> On any failure the first request builds the model exactly
/// as it does today, which is why this one may be best-effort where the type-catalog reload beside it may
/// not — that one's result is a correctness precondition, this one's is an optimisation.</description></item>
/// </list>
/// </summary>
public sealed class EfModelWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<EfModelWarmupService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            using var scope = scopeFactory.CreateScope();
            var entityTypeCount = scope.ServiceProvider
                .GetRequiredService<CleansiaDbContext>()
                .Model.GetEntityTypes().Count();

            logger.LogInformation(
                "EF model warmed at boot: {EntityTypeCount} entity types in {ElapsedMilliseconds} ms.",
                entityTypeCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "EF model warm-up did not complete; the first request builds the model instead.");
        }

        return Task.CompletedTask;
    }
}
