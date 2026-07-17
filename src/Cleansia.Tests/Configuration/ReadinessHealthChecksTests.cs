using Cleansia.Config.Services;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Pins the readiness/liveness health split: the shared registration must expose a REAL database
/// check plus the light blob probe under the "ready" tag (what the App Service <c>/health</c> probe
/// executes), with the deliberate failure semantics — database failures are Unhealthy (route away /
/// recycle), blob failures only Degraded (an external outage must not recycle the fleet). Without
/// these registrations <c>/health</c> collapses back to liveness-only and a broken instance keeps
/// answering 200 forever — the exact defect this split removes.
/// </summary>
public sealed class ReadinessHealthChecksTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ReadinessHealthChecksTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CleansiaDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<Cleansia.Core.Domain.Repositories.IUserSessionProvider>(
            _ => new TestUserSessionProvider("health-test-user", "health@cleansia.test"));
        services.AddScoped<Cleansia.Core.Domain.Repositories.ITenantProvider, NullTenantProvider>();
        services.AddCleansiaReadinessChecks();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registers_Database_And_Blob_Checks_Under_The_Ready_Tag()
    {
        using var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var byName = options.Registrations.ToDictionary(r => r.Name);
        Assert.Contains("database", byName.Keys);
        Assert.Contains("blob_storage", byName.Keys);
        Assert.Contains("ready", byName["database"].Tags);
        Assert.Contains("ready", byName["blob_storage"].Tags);

        // Blob failures must never take readiness non-200 — Degraded is the ceiling.
        Assert.Equal(HealthStatus.Degraded, byName["blob_storage"].FailureStatus);
        // Database failures MUST take readiness non-200.
        Assert.Equal(HealthStatus.Unhealthy, byName["database"].FailureStatus);
    }

    [Fact]
    public async Task Reports_Healthy_With_A_Reachable_Database_And_No_Blob_Factory()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CleansiaDbContext>().Database.EnsureCreatedAsync();

        var service = provider.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync();

        // A host without the blob factory registered reports Healthy for the blob probe (it does
        // not use storage), and the database check passes against the live connection.
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["database"].Status);
        Assert.Equal(HealthStatus.Healthy, report.Entries["blob_storage"].Status);
    }

    [Fact]
    public async Task A_Dead_Database_Fails_Readiness()
    {
        // SQLite silently reopens a closed connection, so simulate the wedged instance with a
        // database file that cannot exist: read-only mode refuses to create it and every
        // connectivity probe fails — the state the App Service probe exists to catch.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CleansiaDbContext>(o =>
            o.UseSqlite("Data Source=/nonexistent-cleansia-health/probe.db;Mode=ReadOnly"));
        services.AddScoped<Cleansia.Core.Domain.Repositories.IUserSessionProvider>(
            _ => new TestUserSessionProvider("health-test-user", "health@cleansia.test"));
        services.AddScoped<Cleansia.Core.Domain.Repositories.ITenantProvider, NullTenantProvider>();
        services.AddCleansiaReadinessChecks();
        using var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Entries["database"].Status);
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    private sealed class NullTenantProvider : Cleansia.Core.Domain.Repositories.ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
