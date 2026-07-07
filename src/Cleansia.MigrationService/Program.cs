using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;

// One-shot Aspire resource: apply the EF migrations, then EXIT. The AppHost starts every
// API host and the Functions worker with WaitForCompletion on this process (exit code 0),
// so nothing that runs background jobs (outbox drainer, fiscal sweep, Hangfire) can touch
// the schema before it exists — WaitFor(postgres) alone only proves the CONTAINER is up.
// Aspire injects the connection string via WithReference(cleansiaDb); the resource is named
// "ConnectionString", so it lands under the same key the app hosts already bind.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "MigrationService: ConnectionStrings__ConnectionString is not set. " +
        "Run under the AppHost, or set the variable to migrate a database manually.");
    return 2;
}

var options = new DbContextOptionsBuilder<CleansiaDbContext>()
    .UseNpgsql(connectionString)
    .Options;

// WaitFor(postgres) already held us until the container reports healthy, but the first
// connection can still race the server's accept loop — retry with backoff instead of dying.
const int maxAttempts = 10;
for (var attempt = 1; attempt <= maxAttempts; attempt++)
{
    try
    {
        await using var db = new CleansiaDbContext(options);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine("MigrationService: database is up to date — nothing to apply.");
            return 0;
        }

        Console.WriteLine($"MigrationService: applying {pending.Count} migration(s): {string.Join(", ", pending)}");
        await db.Database.MigrateAsync();
        Console.WriteLine("MigrationService: migrations applied.");
        return 0;
    }
    catch (Exception ex) when (attempt < maxAttempts)
    {
        Console.WriteLine(
            $"MigrationService: attempt {attempt}/{maxAttempts} failed ({ex.GetType().Name}: {ex.Message}); " +
            $"retrying in {2 * attempt}s.");
        await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
    }
    catch (Exception ex)
    {
        // Non-zero exit -> Aspire marks the dependents FailedToStart instead of letting them
        // run against a half-migrated schema.
        Console.Error.WriteLine($"MigrationService: FAILED after {maxAttempts} attempts: {ex}");
        return 1;
    }
}

return 1;
