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
    public static IServiceCollection AddDbContextBindings(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddSingleton<IDatabaseConnectionString, DatabaseConnectionString>();

        var connectionString = configuration.GetConnectionString("ConnectionString");

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
        // timer-trigger query), so eagerly seed the catalog HERE, synchronously, before any consumer can
        // open a connection on this singleton data source. Best-effort: if the DB is unreachable at
        // build time the catalog has not been cached yet either, and the hosted service / first
        // connection picks it up post-migration.
        TryEagerlyReloadTypeCatalog(dataSource);

        services.AddSingleton(dataSource);
        services.AddHostedService<NpgsqlTypeCatalogInitializer>();
        services.AddDbContext<CleansiaDbContext>(options => options.UseNpgsql(dataSource));
        services.AddScoped<IUnitOfWork>(provider => provider.GetService<CleansiaDbContext>()!);

        return services;
    }

    private static void TryEagerlyReloadTypeCatalog(NpgsqlDataSource dataSource)
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