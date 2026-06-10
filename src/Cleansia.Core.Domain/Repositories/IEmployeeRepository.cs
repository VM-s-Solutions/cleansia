using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee, string>
{
    Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithUserEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<Employee>> GetAllActiveWithUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cross-tenant lookup by employee id. ONLY for system-level triggers that
    /// have no JWT/tenant context (Azure Functions, background jobs) and must
    /// resolve a tenant-scoped employee from a trusted message payload. Caller
    /// MUST call ITenantProvider.SetTenantOverride(employee.TenantId) before any
    /// subsequent mutation so child rows inherit the right tenant.
    /// </summary>
    Task<Employee?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);
}