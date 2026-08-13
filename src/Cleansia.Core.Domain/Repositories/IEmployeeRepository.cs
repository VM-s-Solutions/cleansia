using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeRepository : IRepository<Employee, string>
{
    Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-IGNORING lookup by user email, <b>for the token-minting paths (login + refresh) ONLY</b>:
    /// those run with no tenant context yet, so the tenant-scoped variant collapses to
    /// <c>TenantId == null</c>, misses a tenant-stamped employee, and mints a JWT with no employee claim.
    ///
    /// <para>The row is WRITTEN under a tenant claim and READ with none, because this read happens before
    /// the JWT that would carry one exists. <b>That asymmetry — not the endpoint being anonymous — is why
    /// a bypass is owed here.</b> → /flows/cross-cutting#tenancy</para>
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