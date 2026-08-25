using Cleansia.Core.Domain.Documents;

namespace Cleansia.Core.Domain.Repositories;

public interface IDocumentDeletionRequestRepository
    : IRepository<DocumentDeletionRequest, string>
{
    /// <summary>An open request for this document, if the cleaner already has one.</summary>
    Task<DocumentDeletionRequest?> GetOpenForDocumentAsync(
        string documentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every request belonging to an employee, for the GDPR erasure.
    ///
    /// <para>Tenant-ignoring and unfiltered by status, for the same reason the document twin is: the
    /// erasure runs without a tenant claim, and an answered request still carries the reason the
    /// subject wrote. It must also run BEFORE the documents are removed — the FK is
    /// <c>Restrict</c>, so a surviving request would make the erasure throw rather than skip.</para>
    /// </summary>
    Task RemoveForEmployeeAsync(string employeeId, CancellationToken cancellationToken);
}
