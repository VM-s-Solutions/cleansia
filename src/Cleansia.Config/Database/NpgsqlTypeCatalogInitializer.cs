using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cleansia.Config.Database;

/// <summary>
/// Refreshes the Npgsql type catalog once at host start. Npgsql caches the Postgres type catalog
/// per data source on the FIRST physical connection; on a freshly-created database that first
/// connection can predate the migration's <c>CREATE EXTENSION citext/pg_trgm</c>, leaving every
/// citext column to read as the unknown type "-.-" (InvalidCastException) for the life of the
/// process. The migrating host reloads right after migrating; this covers the NON-migrating hosts
/// (the Functions worker) whose first connection may race another host's migration. Best-effort
/// with retries — if the database is not reachable yet, the data source has not type-loaded either,
/// and the eventual first connection will see the post-migration catalog.
/// </summary>
public sealed class NpgsqlTypeCatalogInitializer(
    NpgsqlDataSource dataSource,
    ILogger<NpgsqlTypeCatalogInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await connection.ReloadTypesAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Type-catalog reload attempt {Attempt}/{MaxAttempts} failed (database may not be ready); retrying.",
                    attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Type-catalog reload did not complete; the catalog will load on the first successful connection instead.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
