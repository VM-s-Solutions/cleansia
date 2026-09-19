using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.Domain.Repositories;

public interface ILegalDocumentRepository : IRepository<LegalDocument, string>
{
    /// <summary>
    /// The document in force on <paramref name="today"/> for a market, with its texts: the market's own
    /// row when it has one, else the platform-wide row; the latest effective date within that set.
    /// Null when nothing is seeded.
    /// </summary>
    Task<LegalDocument?> GetInForceAsync(
        LegalDocumentAudience audience,
        LegalDocumentType type,
        string? countryId,
        DateOnly today,
        CancellationToken cancellationToken);

    /// <summary>Every version, with its texts, newest effective date first; each filter narrows when given.</summary>
    Task<IReadOnlyList<LegalDocument>> GetVersionsAsync(
        LegalDocumentAudience? audience,
        LegalDocumentType? type,
        string? countryId,
        CancellationToken cancellationToken);

    Task<LegalDocument?> GetWithTextsAsync(string id, CancellationToken cancellationToken);
}
