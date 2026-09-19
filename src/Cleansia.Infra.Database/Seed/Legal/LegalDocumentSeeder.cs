using Cleansia.Core.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cleansia.Infra.Database.Seed.Legal;

/// <summary>
/// Upserts the embedded legal texts into <c>LegalDocuments</c>. Idempotent by
/// (audience, type, country, effective date, language) plus the content hash, and it never edits a
/// document already in force: what a customer accepted must read the same forever, so a wording change
/// is a new folder with a new effective date, and a changed file under an old date is logged and skipped.
/// </summary>
public sealed class LegalDocumentSeeder(CleansiaDbContext context, ILogger<LegalDocumentSeeder> logger)
{
    public async Task<LegalSeedOutcome> SeedAsync(DateOnly today, CancellationToken cancellationToken)
    {
        return await SeedAsync(LegalSeedResource.ReadAll(), today, cancellationToken);
    }

    public async Task<LegalSeedOutcome> SeedAsync(
        IReadOnlyList<LegalSeedResource> resources,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var existing = await context.LegalDocuments
            .Include(d => d.Texts)
            .ToListAsync(cancellationToken);
        var countryIdsByIso = await context.Countries
            .Select(c => new { c.IsoCode, c.Id })
            .ToDictionaryAsync(c => c.IsoCode.ToUpperInvariant(), c => c.Id, cancellationToken);

        var outcome = new LegalSeedOutcome();
        foreach (var group in resources.GroupBy(r => (r.Audience, r.Type, r.CountryIsoCode, r.EffectiveFrom)))
        {
            var (audience, type, countryIso, effectiveFrom) = group.Key;

            string? countryId = null;
            if (countryIso is not null && !countryIdsByIso.TryGetValue(countryIso, out countryId))
            {
                logger.LogWarning(
                    "Legal seed {Audience}/{Type}/{Country}/{EffectiveFrom} names a country that is not in the catalogue; skipped",
                    audience, type, countryIso, effectiveFrom);
                outcome.SkippedUnknownCountry++;
                continue;
            }

            var document = existing.FirstOrDefault(d =>
                d.Audience == audience && d.Type == type && d.CountryId == countryId && d.EffectiveFrom == effectiveFrom);

            if (document is null)
            {
                document = LegalDocument.Create(audience, type, countryId, effectiveFrom);
                foreach (var resource in group)
                {
                    document.AddText(resource.Language, resource.Title, resource.ContentMarkdown);
                }

                context.LegalDocuments.Add(document);
                outcome.AddedDocuments++;
                continue;
            }

            foreach (var resource in group)
            {
                var text = document.TextFor(resource.Language);
                if (text is not null && text.Matches(resource.Title, resource.ContentMarkdown))
                {
                    continue;
                }

                if (document.IsInForceOn(today))
                {
                    logger.LogWarning(
                        "Legal document {Audience}/{Type}/{Country} version {Version} is in force and its {Language} text differs from the seed file; a wording change needs a new effective date. Skipped",
                        audience, type, countryIso ?? LegalSeedResource.AnyCountry, document.Version, resource.Language);
                    outcome.SkippedImmutable++;
                    continue;
                }

                if (text is null)
                {
                    document.AddText(resource.Language, resource.Title, resource.ContentMarkdown);
                    outcome.AddedTexts++;
                }
                else
                {
                    text.Replace(resource.Title, resource.ContentMarkdown);
                    outcome.UpdatedTexts++;
                }
            }
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another host booting at the same moment won the insert; its rows are the same files.
            logger.LogInformation(ex, "Legal documents were seeded concurrently by another host; nothing to do");
            return new LegalSeedOutcome();
        }

        if (outcome.Changed)
        {
            logger.LogInformation(
                "Legal documents seeded: {AddedDocuments} document(s), {AddedTexts} text(s) added, {UpdatedTexts} text(s) updated, {SkippedImmutable} in-force text(s) skipped",
                outcome.AddedDocuments, outcome.AddedTexts, outcome.UpdatedTexts, outcome.SkippedImmutable);
        }

        return outcome;
    }
}

public sealed class LegalSeedOutcome
{
    public int AddedDocuments { get; internal set; }
    public int AddedTexts { get; internal set; }
    public int UpdatedTexts { get; internal set; }
    public int SkippedImmutable { get; internal set; }
    public int SkippedUnknownCountry { get; internal set; }

    public bool Changed => AddedDocuments + AddedTexts + UpdatedTexts + SkippedImmutable + SkippedUnknownCountry > 0;
}
