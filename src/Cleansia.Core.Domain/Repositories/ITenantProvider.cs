namespace Cleansia.Core.Domain.Repositories;

public interface ITenantProvider
{
    string? GetCurrentTenantId();

    void SetTenantOverride(string tenantId);

    /// <summary>
    /// Drops a previously set override. Background-job loops MUST call this
    /// before each iteration so the tenant of one iteration never outlives it:
    /// what the next commit stamps is decided by that iteration's own override,
    /// not by whatever ran last.
    /// </summary>
    void ClearTenantOverride();
}
