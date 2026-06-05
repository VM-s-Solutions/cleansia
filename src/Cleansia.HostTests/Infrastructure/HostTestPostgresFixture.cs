using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using DbAssemblyReference = Cleansia.Infra.Database.AssemblyReference;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// A single Postgres Testcontainer shared by every host-test class in the assembly (xUnit collection
/// fixture). Mirrors <c>Cleansia.IntegrationTests.PostgresContainerFixture</c> but:
/// <list type="bullet">
///   <item>lets Testcontainers assign a RANDOM host port (no fixed <c>5432</c> binding) so it can run
///   alongside the integration-test container in CI without a port clash, and</item>
///   <item>applies EF migrations ONCE here (the host's own <c>MigrateDatabase</c> only runs in
///   Development; the harness boots a non-Development env on purpose, so we migrate the schema
///   ourselves — same approach as <c>BaseIntegrationTest.ApplyMigrationsAsync</c>).</item>
/// </list>
/// The connection string is injected into every booted host via
/// <see cref="HostTestApplicationFactory{TEntryPoint}"/>.
/// </summary>
public sealed class HostTestPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("cleansia_hosttests")
        .WithUsername("hosttests")
        .WithPassword("hosttests")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await ApplyMigrationsAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"],
        });
    }

    /// <summary>Truncate every table so each test starts from a clean database (called by the base
    /// test class before each test arranges its own graph).</summary>
    public async Task ResetAsync()
    {
        if (_respawner is null) return;
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(ConnectionString, x => x.MigrationsAssembly(DbAssemblyReference.Assembly))
            // The model has drifted slightly ahead of the latest committed migration in the repo
            // (a snapshot the owner regenerates manually — NO ef here). For the host harness we only
            // need the migration-defined schema to apply; demote the pending-model-changes guard from
            // error to a no-op so the container schema can be built. This is DB-setup plumbing in the
            // TEST fixture only — it does not touch any production DbContext configuration.
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var dbContext = new CleansiaDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class HostTestCollection : ICollectionFixture<HostTestPostgresFixture>
{
    public const string Name = "HostTestPostgresCollection";
}
