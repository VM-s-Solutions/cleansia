using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cleansia.Config.Database;

/// <summary>
/// Refreshes the Npgsql type catalog at host start. Npgsql caches the Postgres type catalog
/// per data source on the FIRST physical connection; on a freshly-created database that first
/// connection can predate the migration's <c>CREATE EXTENSION citext/pg_trgm</c>, leaving every
/// citext column to read as the unknown type "-.-" (InvalidCastException) for the life of the
/// process. The migrating host reloads right after migrating; this covers the NON-migrating hosts
/// (the Functions worker) whose first connection may race another host's migration.
///
/// <para>CRITICAL: a reload against a PRE-extension database SUCCEEDS — it just caches a catalog
/// that still has no citext OID, which is exactly the life-of-process failure this exists to
/// prevent (bit the local Functions worker: its timer triggers threw "-.-" on every tick while the
/// API host was still migrating the fresh database). So the loop retries not only on connection
/// failure but ALSO while the required extensions are absent, and only reloads once they exist.
/// Best-effort with a bounded window — if the extensions never appear, citext reads keep failing
/// regardless; the log says exactly why.</para>
/// </summary>
public sealed class NpgsqlTypeCatalogInitializer(
    NpgsqlDataSource dataSource,
    ILogger<NpgsqlTypeCatalogInitializer> logger) : IHostedService
{
    // 2s,4s,…,20s capped — ~2 minutes total, comfortably covering a local/CI migration window.
    private const int MaxAttempts = 10;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

                if (!await RequiredExtensionsPresentAsync(connection, cancellationToken))
                {
                    if (attempt < MaxAttempts)
                    {
                        logger.LogInformation(
                            "citext/pg_trgm not installed yet (another host's migration in flight?); " +
                            "retrying the type-catalog reload ({Attempt}/{MaxAttempts}).",
                            attempt, MaxAttempts);
                        await Task.Delay(Delay(attempt), cancellationToken);
                        continue;
                    }

                    logger.LogWarning(
                        "citext/pg_trgm still absent after {MaxAttempts} attempts — citext column reads " +
                        "will throw InvalidCastException until the extensions are created and this host restarts.",
                        MaxAttempts);
                    return;
                }

                await connection.ReloadTypesAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex,
                    "Type-catalog reload attempt {Attempt}/{MaxAttempts} failed (database may not be ready); retrying.",
                    attempt, MaxAttempts);
                await Task.Delay(Delay(attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Type-catalog reload did not complete; the catalog will load on the first successful connection instead.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static TimeSpan Delay(int attempt) => TimeSpan.FromSeconds(Math.Min(2 * attempt, 20));

    private static async Task<bool> RequiredExtensionsPresentAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_extension WHERE extname IN ('citext', 'pg_trgm')";
        var installed = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return installed == 2;
    }
}
