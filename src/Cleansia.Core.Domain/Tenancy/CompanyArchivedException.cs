namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// A commit tried to change the books of a company frozen for archive (ADR-0064 D3). Thrown by the
/// database seam where every write crosses, mapped by the hosts to <c>409 tenant.archived</c>, and
/// classified by the queue consumers as permanent.
/// </summary>
public sealed class CompanyArchivedException(string tenantId)
    : Exception($"Company {tenantId} is frozen for archive; its books accept no further writes.")
{
    public string TenantId { get; } = tenantId;
}
