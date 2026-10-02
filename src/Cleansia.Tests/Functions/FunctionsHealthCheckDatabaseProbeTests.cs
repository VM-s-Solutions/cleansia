using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Cleansia.Config.Health;
using Cleansia.Config.Services;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The database probe against a Postgres that accepts the connection and never answers, the shape a
/// saturated server presents during a deploy. Npgsql's open does not honour its cancellation token at that
/// stage, so the connection string here carries a connect timeout far above the readiness bound: the
/// elapsed time shows which of the two ended the probe, and the lower bound reddens a fixture that fails fast.
/// The check runs in a DI scope the way the host runs it, so a stalled open left on the scope's own context
/// fails the scope's dispose.
/// </summary>
public sealed class FunctionsHealthCheckDatabaseProbeTests
{
    private static readonly TimeSpan StalledConnectTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan TimerTolerance = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task Answers_unhealthy_within_the_bound_when_the_database_accepts_and_never_answers()
    {
        // Listening but never accepting: the kernel completes the handshake from its backlog, so the client
        // is connected and its startup packet is never answered.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var queues = new Mock<QueueServiceClient>(MockBehavior.Strict);
        queues.Setup(q => q.GetQueuesAsync(It.IsAny<QueueTraits>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<QueueItem>.FromPages([Page<QueueItem>.FromValues([], null, Mock.Of<Response>())]));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(
            $"Host=127.0.0.1;Port={port};Database=cleansia;Username=probe;Password=probe;" +
            $"Timeout={StalledConnectTimeout.TotalSeconds:0}"));
        services.AddSingleton(queues.Object);
        services.AddScoped<FunctionsHealthCheck>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var stopwatch = Stopwatch.StartNew();
        HealthReport report;
        await using (var scope = provider.CreateAsyncScope())
        {
            report = await scope.ServiceProvider.GetRequiredService<FunctionsHealthCheck>()
                .CheckAsync(CancellationToken.None);
        }
        var elapsed = stopwatch.Elapsed;

        var probe = Assert.Single(report.Probes, p => p.Name == "database");
        Assert.False(probe.Ok);
        Assert.False(report.Healthy);
        Assert.True(elapsed >= ReadinessHealthChecks.ReadinessCheckTimeout - TimerTolerance,
            $"The check answered after {elapsed.TotalSeconds:0.00}s, before the readiness bound, so the database " +
            "never stalled it and the upper bound below proves nothing.");
        Assert.True(elapsed < ReadinessHealthChecks.ReadinessCheckTimeout * 2,
            $"The check answered after {elapsed.TotalSeconds:0.00}s: the connection string's own " +
            $"{StalledConnectTimeout.TotalSeconds:0}s connect timeout ended the database probe, not the " +
            $"{ReadinessHealthChecks.ReadinessCheckTimeout.TotalSeconds:0}s readiness bound.");
    }
}
