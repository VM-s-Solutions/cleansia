namespace Cleansia.Core.Domain.Common;

/// <summary>
/// An audited row that belongs to one operating company. The tenant is stamped at commit time from the
/// ambient provider and the column is a foreign key into <c>Tenants</c>; a catalogue or per-country type
/// stays plain <see cref="Auditable"/> and carries no tenant column at all.
/// </summary>
public class TenantAuditable : Auditable, ITenantEntity
{
    public string? TenantId { get; set; }
}
