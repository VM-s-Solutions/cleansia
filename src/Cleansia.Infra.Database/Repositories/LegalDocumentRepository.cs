using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LegalDocumentRepository(CleansiaDbContext context) : BaseRepository<LegalDocument>(context), ILegalDocumentRepository
{
    public Task<LegalDocument?> GetInForceAsync(
        LegalDocumentAudience audience,
        LegalDocumentType type,
        string? countryId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Texts)
            .AsNoTracking()
            .Where(d => d.Audience == audience
                        && d.Type == type
                        && d.EffectiveFrom <= today
                        && (d.CountryId == null || d.CountryId == countryId))
            .OrderByDescending(d => d.CountryId != null)
            .ThenByDescending(d => d.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegalDocument>> GetVersionsAsync(
        LegalDocumentAudience? audience,
        LegalDocumentType? type,
        string? countryId,
        CancellationToken cancellationToken)
    {
        var query = GetDbSet()
            .Include(d => d.Country)
            .Include(d => d.Texts)
            .AsNoTracking()
            .AsQueryable();

        if (audience is not null)
        {
            query = query.Where(d => d.Audience == audience);
        }

        if (type is not null)
        {
            query = query.Where(d => d.Type == type);
        }

        if (countryId is not null)
        {
            query = query.Where(d => d.CountryId == countryId);
        }

        return await query
            .OrderBy(d => d.Audience)
            .ThenBy(d => d.Type)
            .ThenBy(d => d.CountryId)
            .ThenByDescending(d => d.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public Task<LegalDocument?> GetWithTextsAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Country)
            .Include(d => d.Texts)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}
