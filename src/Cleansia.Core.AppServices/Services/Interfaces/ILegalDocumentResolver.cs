using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The customer legal text in force today for a market — the one a consent stamps and a legal page
/// shows. Null <paramref name="countryId"/> is the default market, the same choice an anonymous
/// request naming no market gets (ADR-0061 D3); a market without its own copy falls back to the
/// platform-wide text. Null when nothing is seeded: the page read refuses, and so does the booking's
/// work-contract stamp in <c>OrderFactory</c> (an order no contract can form on is not booked), while a
/// consent or the booking's terms record "version unknown" rather than refuse — the legal text is not
/// what they are for.
/// </summary>
public interface ILegalDocumentResolver
{
    Task<LegalDocument?> ResolveInForceAsync(LegalDocumentType type, string? countryId, CancellationToken cancellationToken);
}
