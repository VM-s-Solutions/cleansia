using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Cleansia.Config.Database;

public static class DbContextBindingExtensions
{
    /// <summary>
    /// How long composition will wait for the eager type-catalog probe before giving up on it — the whole
    /// attempt, connect and reload together. Enough headroom for a cold TLS + SCRAM handshake to a managed
    /// Postgres an order of magnitude over, and short enough that a boot where the database is not up yet
    /// costs seconds rather than Npgsql's 15s default. Exceeding it is not a failure: the probe is
    /// best-effort and <see cref="NpgsqlTypeCatalogInitializer"/> is the retrying path behind it.
    /// </summary>
    public static readonly TimeSpan EagerTypeCatalogProbeTimeout = TimeSpan.FromSeconds(3);

    /// <param name="eagerlyReloadNpgsqlTypeCatalog">
    /// Opt in to the SYNCHRONOUS type-catalog probe below. Only the isolated Functions worker needs it
    /// (see <see cref="TryEagerlyReloadTypeCatalog"/>); for an API host it is pure cold-start latency —
    /// a blocking Postgres connect inside ConfigureServices, on a path where
    /// <see cref="NpgsqlTypeCatalogInitializer"/> already covers the same need without blocking startup.
    /// </param>
    public static IServiceCollection AddDbContextBindings(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env,
        bool eagerlyReloadNpgsqlTypeCatalog = false)
    {
        services.AddSingleton<IDatabaseConnectionString, DatabaseConnectionString>();
        services.AddSingleton<IRegionConnectionStringResolver, RegionConnectionStringResolver>();

        // Resolve the DB connection string through the one region seam (ADR-0017 / T-0330). Today every
        // region returns the single shared West-Europe database, so this is behaviour-preserving.
        var connectionString = new RegionConnectionStringResolver(configuration).ResolveDefault();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        // citext columns (Address/Language/Currency/User) report DataTypeName "citext", which Npgsql 7+
        // no longer auto-maps — reading one as string otherwise throws InvalidCastException. Enabling
        // unmapped types lets citext round-trip through its text representation.
        dataSourceBuilder.EnableUnmappedTypes();
        var dataSource = dataSourceBuilder.Build();

        // Npgsql caches the Postgres type catalog per data source on the FIRST physical connection. On a
        // freshly-created database that first connection can predate the migration's CREATE EXTENSION
        // citext/pg_trgm, leaving every citext column to read as the unknown type "-.-"
        // (InvalidCastException) for the life of the process. The hosted-service reload below is not
        // reliable in the isolated Functions worker (its IHostedService start races the first
        // timer-trigger query), so THAT host eagerly seeds the catalog HERE, synchronously, before any
        // consumer can open a connection on this singleton data source. Best-effort: if the DB is
        // unreachable at build time the catalog has not been cached yet either, and the hosted service /
        // first connection picks it up post-migration.
        //
        // Opt-in, because for an API host this is a blocking DB round-trip in ConfigureServices that buys
        // nothing: an API host has no trigger racing IHostedService start, so NpgsqlTypeCatalogInitializer
        // (registered two lines below, async + retrying + extension-aware) already covers it.
        if (eagerlyReloadNpgsqlTypeCatalog)
        {
            TryEagerlyReloadTypeCatalog(dataSource);
        }

        services.AddSingleton(dataSource);
        // Registration order is load-bearing: IHostedService.StartAsync runs sequentially, and
        // NpgsqlTypeCatalogInitializer awaits a retry loop that can span ~2 minutes while a migration is
        // in flight. Registered after it, the warm-up would be queued behind exactly the slow boot it
        // exists to help. It yields immediately, so it delays the initializer by nothing.
        services.AddHostedService<EfModelWarmupService>();
        services.AddHostedService<NpgsqlTypeCatalogInitializer>();
        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<IUnitOfWork>(provider => provider.GetService<CleansiaDbContext>()!);

        return services;
    }

    /// <summary>
    /// Blocking on purpose — the ordering guarantee IS this method (a timer trigger can fire before
    /// IHostedService start, so the catalog has to be seeded before composition returns). What is bounded is
    /// only how long that guarantee is allowed to cost when the database does not answer.
    /// </summary>
    private static void TryEagerlyReloadTypeCatalog(NpgsqlDataSource dataSource)
    {
        // The ceiling has to be applied HERE, over the whole attempt, and the two obvious alternatives were
        // measured and rejected. Npgsql's own connect deadline is a connection-string property, and that
        // string belongs to the data source every runtime query uses — shortening it there shortens those
        // too, while a probe on a SEPARATE data source would seed a different catalog and buy nothing (the
        // cache is per data source, which is the whole premise above). And OpenConnectionAsync's
        // CancellationToken is not a seam either: against a peer that accepts and then never answers — the
        // Aspire cold-start shape, since the proxy binds the port before Postgres is ready — a 2s token did
        // not cut the call, which still returned at the connection string's own 6s.
        //
        // A dedicated background thread rather than the pool: a saturated pool at worker startup could
        // otherwise delay the probe past its own ceiling and silently skip the seeding while the database
        // was fine, and an abandoned probe must never hold the process open.
        var probe = new Thread(() => SeedTypeCatalog(dataSource)) { IsBackground = true };
        probe.Start();
        probe.Join(EagerTypeCatalogProbeTimeout);
    }

    private static void SeedTypeCatalog(NpgsqlDataSource dataSource)
    {
        try
        {
            using var connection = dataSource.OpenConnection();
            connection.ReloadTypes();
        }
        catch
        {
            // DB not reachable at startup (e.g. Aspire is still spinning Postgres up). The catalog is
            // not cached yet, so the eventual first successful connection — or the hosted-service
            // reload — sees the post-migration catalog. Swallow: a startup probe must never crash boot.
        }
    }
}