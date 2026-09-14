using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The customer legal text in force today for a market — the one a consent stamps and a legal page
/// shows. Null <paramref name="countryId"/> is the default market, the same choice an anonymous
/// request naming no market gets (ADR-0061 D3); a market without its own copy falls back to the
/// platform-wide text. Null when nothing is seeded, which the callers record as "version unknown"
/// rather than refuse: the legal text is not what a booking or a registration is for.
/// </summary>
public interface ILegalDocumentResolver
{
    Task<LegalDocument?> ResolveInForceAsync(LegalDocumentType type, string? countryId, CancellationToken cancellationToken);
}
