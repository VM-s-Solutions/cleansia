using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee, string>
{
    Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-IGNORING lookup of an employee by their user email. For the token-minting
    /// paths (login + refresh) ONLY: those run with no tenant context yet, so the
    /// tenant-scoped <see cref="GetByUserEmailAsync"/> collapses to TenantId == null and
    /// misses a tenant-stamped employee — minting a JWT without the employee_id claim
    /// (T-0361). The caller has already authenticated the user by email (unique across the
    /// platform), so resolving THEIR employee id cross-tenant leaks nothing.
    /// </summary>
    Task<Employee?> GetByUserEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default);

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