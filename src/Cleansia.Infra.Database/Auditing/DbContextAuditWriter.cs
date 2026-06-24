using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Auditing;

/// <summary>
/// ADR-0012 D2 — the success-path <see cref="IAuditWriter"/>. Adds the row to the SAME scoped
/// <see cref="CleansiaDbContext"/> the pipeline's <c>UnitOfWorkPipelineBehavior</c> commits, so the row
/// flushes in that single <c>SaveChangesAsync</c> — atomic with the action. It does NOT save. Because
/// <see cref="AdminActionAudit"/> is <c>BaseEntity + ITenantEntity</c> (not <c>Auditable</c>),
/// <c>CommitAsync</c> does not stamp its <c>TenantId</c> (its loop is over <c>Auditable</c> only); this
/// writer stamps it here from the same <see cref="ITenantProvider"/> the rest of the pipeline uses.
/// </summary>
public sealed class DbContextAuditWriter(CleansiaDbContext context, ITenantProvider tenantProvider) : IAuditWriter
{
    public void Add(AdminActionAudit entry)
    {
        entry.TenantId ??= tenantProvider.GetCurrentTenantId();
        context.AdminActionAudits.Add(entry);
    }
}
