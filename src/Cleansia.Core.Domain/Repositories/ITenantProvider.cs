namespace Cleansia.Core.Domain.Repositories;

public interface ITenantProvider
{
    string? GetCurrentTenantId();

    void SetTenantOverride(string tenantId);

    /// <summary>
    /// Drops a previously set override. Background-job loops MUST call this
    /// before each iteration so the tenant of one iteration doesn't leak into
    /// the next when a later iteration has no tenant context (single-tenant /
    /// null TenantId rows).
    /// </summary>
    void ClearTenantOverride();
}
