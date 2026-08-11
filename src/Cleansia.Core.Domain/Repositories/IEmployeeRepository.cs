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
    /// (T-0361).
    ///
    /// <para>The cell (ADR-0051 D1): the row is WRITTEN under a tenant claim and READ with no claim,
    /// because this read happens BEFORE the JWT that would carry one exists. That asymmetry — not the
    /// endpoint being anonymous, and not any property of the email — is the whole reason a bypass is
    /// owed here; an anonymous read whose rows are also written anonymously stays filtered.</para>
    ///
    /// <para>The re-pin is the caller's, and the signature cannot enforce it: the credential check that
    /// precedes this call has already authenticated the caller as the owner of that address, so the read
    /// resolves the just-authenticated subject's OWN employee row. Callers stay confined to the
    /// token-mint paths for exactly that reason. It is emphatically NOT an appeal to email being unique
    /// across the platform — uniqueness is per-tenant by design, on <c>(TenantId, Email)</c> — and
    /// ADR-0051 D2 forbids re-pinning a bypass on a uniqueness property the schema does not enforce.</para>
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