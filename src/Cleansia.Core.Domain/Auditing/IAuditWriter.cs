namespace Cleansia.Core.Domain.Auditing;

/// <summary>
/// ADR-0012 D2 — the success-path seam. <c>AuditLogBehavior</c> hands the row to the writer, which adds
/// it to the <b>same scoped DbContext</b> the outer <c>UnitOfWorkPipelineBehavior</c> commits, so the
/// row rides that single <c>SaveChangesAsync</c> and is atomic with the action. The impl lives in
/// Infra.Database (it needs the scoped <c>CleansiaDbContext</c>); the behavior depends only on this
/// abstraction so it never references the DbContext. It does NOT save — saving is the UoW's job.
/// </summary>
public interface IAuditWriter
{
    void Add(AdminActionAudit entry);
}
