using Cleansia.Core.Domain.Legal;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Seed.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.IntegrationTests.Features.Legal;

/// <summary>The real seed, run the way a host start runs it, so the tests stamp the version the seed files carry.</summary>
public static class LegalSeed
{
    public static Task<LegalSeedOutcome> SeedAsync(CleansiaDbContext context) =>
        new LegalDocumentSeeder(context, NullLogger<LegalDocumentSeeder>.Instance)
            .SeedAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

    public static Task<LegalDocument> PlatformWideAsync(CleansiaDbContext context, LegalDocumentType type) =>
        context.LegalDocuments
            .Include(d => d.Texts)
            .AsNoTracking()
            .SingleAsync(d => d.Audience == LegalDocumentAudience.Customer && d.Type == type && d.CountryId == null);
}
