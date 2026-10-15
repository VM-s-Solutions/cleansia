using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Cleansia.Config.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.ServiceDefaults;
using Cleansia.TestUtilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit.Abstractions;

namespace Cleansia.Tests.Configuration;

public sealed class ReadinessDatabaseDeadlineTests(ITestOutputHelper output)
{
    private static readonly TimeSpan StalledConnectTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan TimerTolerance = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task The_registered_service_bounds_repeated_stalled_probes_including_scope_disposal()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var disposal = new ProbeDisposalObserver();
        var services = new ServiceCollection();
        services.AddLogging();
        ConfigureReadiness(services, listener, disposal);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await WarmModelAsync(provider);
        disposal.Reset();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            HealthReport report;
            await using (var scope = provider.CreateAsyncScope())
            {
                report = await scope.ServiceProvider.GetRequiredService<HealthCheckService>()
                    .CheckHealthAsync(registration => registration.Tags.Contains("ready"));
            }

            output.WriteLine($"Probe {attempt}: {stopwatch.Elapsed.TotalSeconds:0.000}s, {report.Status}, disposed={disposal.Count}");
            AssertBound(stopwatch.Elapsed);
            Assert.Equal(HealthStatus.Unhealthy, report.Status);
            Assert.Equal(HealthStatus.Unhealthy, report.Entries["database"].Status);
            Assert.Equal(HealthStatus.Healthy, report.Entries["blob_storage"].Status);
        }

        await disposal.WaitForAsync(2, StalledConnectTimeout + ReadinessHealthChecks.ReadinessCheckTimeout);
        Assert.Equal(2, disposal.Count);
        output.WriteLine("Both probe connections were disposed after their outstanding opens completed.");
    }

    [Fact]
    public async Task The_real_health_endpoint_returns_503_within_the_bound_and_stops_cleanly()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var disposal = new ProbeDisposalObserver();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ConfigureReadiness(builder.Services, listener, disposal);
        var app = builder.Build();
        app.MapDefaultEndpoints();
        await WarmModelAsync(app.Services);
        disposal.Reset();
        await app.StartAsync();

        try
        {
            using var client = app.GetTestClient();
            var stopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();
            output.WriteLine($"/health: {stopwatch.Elapsed.TotalSeconds:0.000}s, {(int)response.StatusCode}, {body}");
            AssertBound(stopwatch.Elapsed);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("Unhealthy", body);

            using var alive = await client.GetAsync("/alive");
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        }
        finally
        {
            var stopwatch = Stopwatch.StartNew();
            await app.StopAsync();
            await app.DisposeAsync();
            output.WriteLine($"Host stop and disposal: {stopwatch.Elapsed.TotalSeconds:0.000}s");
            Assert.True(stopwatch.Elapsed < ReadinessHealthChecks.ReadinessCheckTimeout);
        }

        await disposal.WaitForAsync(1, StalledConnectTimeout + ReadinessHealthChecks.ReadinessCheckTimeout);
        Assert.Equal(1, disposal.Count);
    }

    private static void ConfigureReadiness(IServiceCollection services, TcpListener listener, ProbeDisposalObserver disposal)
    {
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(
                $"Host=127.0.0.1;Port={port};Database=cleansia;Username=probe;Password=probe;" +
                $"Timeout={StalledConnectTimeout.TotalSeconds:0};Pooling=false")
            .AddInterceptors(disposal));
        services.AddScoped<IUserSessionProvider>(_ => new TestUserSessionProvider("health-test-user", "health@cleansia.test"));
        services.AddScoped<ITenantProvider>(_ => Mock.Of<ITenantProvider>());
        services.AddCleansiaReadinessChecks();
    }

    private static async Task WarmModelAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().Model;
    }

    private static void AssertBound(TimeSpan elapsed)
    {
        Assert.True(elapsed >= ReadinessHealthChecks.ReadinessCheckTimeout - TimerTolerance,
            $"The probe returned after {elapsed.TotalSeconds:0.000}s without reaching the stalled-read deadline.");
        Assert.True(elapsed < ReadinessHealthChecks.ReadinessCheckTimeout * 2,
            $"The probe took {elapsed.TotalSeconds:0.000}s with a {StalledConnectTimeout.TotalSeconds:0}s Npgsql timeout; " +
            "the readiness deadline did not bound the actual check and scope disposal.");
    }

    private sealed class ProbeDisposalObserver : DbConnectionInterceptor
    {
        private int _count;
        private readonly SemaphoreSlim _disposed = new(0);
        public int Count => Volatile.Read(ref _count);

        public void Reset()
        {
            Interlocked.Exchange(ref _count, 0);
            while (_disposed.Wait(0)) { }
        }

        public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData eventData)
        {
            Interlocked.Increment(ref _count);
            _disposed.Release();
        }

        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            ConnectionDisposed(connection, eventData);
            return Task.CompletedTask;
        }

        public async Task WaitForAsync(int count, TimeSpan timeout)
        {
            using var deadline = new CancellationTokenSource(timeout);
            while (Count < count)
            {
                await _disposed.WaitAsync(deadline.Token);
            }
        }
    }
}
