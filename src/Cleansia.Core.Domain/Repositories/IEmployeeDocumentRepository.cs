using Cleansia.Core.Domain.Documents;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeDocumentRepository : IRepository<EmployeeDocument, string>
{
    Task<EmployeeDocument?> GetByIdWithVersionHistoryAsync(string id, CancellationToken cancellationToken = default);
    Task<List<EmployeeDocument>> GetByEmployeeIdAsync(string employeeId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<EmployeeDocument>> GetVersionHistoryAsync(string documentId, CancellationToken cancellationToken = default);
    Task<EmployeeDocument?> GetLatestVersionAsync(string documentId, CancellationToken cancellationToken = default);
    Task<EmployeeDocument?> GetLatestByFileNameAsync(string employeeId, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// GDPR erasure. Deletes the subject's document rows once their blobs have been deleted.
    ///
    /// <para>The erasure already deletes every one of these blobs, so the platform has decided the document
    /// does not survive an erasure; what was left behind was its metadata — <c>FileName</c> (routinely the
    /// person's own, "Novotna_ID.pdf"), <c>FilePath</c>, <c>Description</c> and the reviewer's free-text
    /// <c>ReviewNotes</c> — pointing at bytes that no longer exist. No sweep reached it either: the
    /// superseded-document purge takes only rows already deactivated (<c>!IsActive &amp;&amp; DeactivatedOn
    /// &lt; cutoff</c>), and an erasure deactivates the employee, never their documents.</para>
    /// </summary>
    Task RemoveForEmployeeAsync(string employeeId, CancellationToken cancellationToken);
}
