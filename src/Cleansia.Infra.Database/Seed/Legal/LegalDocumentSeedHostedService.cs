using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Database.Seed.Legal;

/// <summary>
/// Runs the legal-document seed at every host start, in every environment: the seed files are the
/// source of the texts and a deploy is how a new version reaches the database. Awaited before the
/// host serves, so the first legal-page read after a deploy already finds the new version; bounded
/// retries because a host may come up a moment before its database does, and never a boot failure —
/// a customer can still book while the legal page reads the previous version. The one database this
/// cannot seed is a fresh Development one: hosted services start before the pipeline whose
/// <c>MigrateDatabase</c> creates the schema, so that path seeds the texts itself after migrating.
/// </summary>
public sealed class LegalDocumentSeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<LegalDocumentSeedHostedService> logger) : IHostedService
{
    private const int MaxAttempts = 5;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<LegalDocumentSeeder>();
                await seeder.SeedAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "Legal document seed attempt {Attempt}/{MaxAttempts} failed (database may not be ready); retrying",
                    attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Legal documents were not seeded; the legal pages serve whatever version the database already holds until the next start");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
