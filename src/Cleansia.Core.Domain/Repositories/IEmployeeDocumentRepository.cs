using Cleansia.Core.Domain.Documents;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeDocumentRepository : IRepository<EmployeeDocument, string>
{
    Task<EmployeeDocument?> GetByIdWithVersionHistoryAsync(string id, CancellationToken cancellationToken = default);
    Task<List<EmployeeDocument>> GetByEmployeeIdAsync(string employeeId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<EmployeeDocument>> GetVersionHistoryAsync(string documentId, CancellationToken cancellationToken = default);
    Task<EmployeeDocument?> GetLatestVersionAsync(string documentId, CancellationToken cancellationToken = default);
    Task<EmployeeDocument?> GetLatestByFileNameAsync(string employeeId, string fileName, CancellationToken cancellationToken = default);
}
