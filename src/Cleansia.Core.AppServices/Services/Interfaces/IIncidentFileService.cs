using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Assembles what the customer incident file prints (owner ruling on Q-AUD-L6: a PDF, built once from
/// the database). Pure read; the audit row and the render are the caller's. The subject's orders are
/// the ones that name them now OR the ones their own successful acts named — after an erasure the
/// order no longer carries its customer, the trail still does, and a refused act proves nothing.
/// </summary>
public interface IIncidentFileService
{
    Task<IncidentFilePdfData> BuildAsync(string userId, string? orderId, string generatedBy, CancellationToken cancellationToken);

    Task<bool> IsSubjectOrderAsync(string userId, string orderId, CancellationToken cancellationToken);
}
