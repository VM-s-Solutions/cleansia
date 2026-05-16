using Cleansia.Core.AppServices.Features.Gdpr.DTOs;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Builds the GDPR Article 15 data-export payload for a user. Shared between
/// the self-service <c>ExportUserData</c> handler (caller exports their own
/// data) and the admin <c>AdminExportUserData</c> handler (admin exports a
/// target user's data). Audit row writing is the caller's responsibility —
/// the service is pure read.
/// </summary>
public interface IGdprExportService
{
    /// <summary>
    /// Aggregates profile + address + employee + orders + documents + invoices
    /// + consents into a <see cref="GdprExportDto"/>. <paramref name="exportedBy"/>
    /// is recorded in the export metadata (user's own email for self-export,
    /// <c>admin:&lt;email&gt;</c> for admin export).
    /// </summary>
    Task<GdprExportDto> BuildAsync(string userId, string exportedBy, CancellationToken cancellationToken);
}
