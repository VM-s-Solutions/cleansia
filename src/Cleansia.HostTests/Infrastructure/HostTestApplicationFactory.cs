using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Boots one REAL API host (<typeparamref name="TEntryPoint"/> = a host's <c>Program</c>) through
/// <see cref="WebApplicationFactory{TEntryPoint}"/> with the FULL production pipeline — the host's own
/// <c>AddJwt</c> bearer validation + the shared <c>AddCleansiaAuthorization</c> policies + every
/// <c>[Permission]</c> gate — pointed at the shared Testcontainers Postgres.
///
/// Environment is forced to a NON-Development name ("HostTests") on purpose so that:
/// <list type="bullet">
///   <item>the host does NOT auto-migrate/seed in <c>CleansiaStartupBase.Configure</c> (we migrate the
///   schema once in <see cref="HostTestPostgresFixture"/>, like BaseIntegrationTest), and</item>
///   <item>Swagger is fail-closed off (it serves only in Development), so the host runs the same
///   middleware ordering a deployed environment runs.</item>
/// </list>
/// We must therefore satisfy the non-Development boot guards: a narrow <c>ForwardedHeaders</c> trust
/// (ADR-0003 D3 refuses to boot otherwise) and a non-public CORS origin (so the Swagger-exposure guard
/// is a no-op). Both come from appsettings.HostTests.json, layered LAST so they win.
/// </summary>
public sealed class HostTestApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly string _connectionString;

    public HostTestApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("HostTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Layered LAST → these override whatever the host's own appsettings.json carries
            // (connection string, JWT secret used to validate the minted token, narrow proxy trust,
            // non-public CORS so the Swagger-exposure guard stays a no-op).
            var basePath = AppContext.BaseDirectory;
            config.AddJsonFile(Path.Combine(basePath, "appsettings.HostTests.json"), optional: false);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConnectionString"] = _connectionString,
            });
        });
    }
}
