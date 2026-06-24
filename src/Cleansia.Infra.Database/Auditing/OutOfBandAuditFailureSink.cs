using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Database.Auditing;

/// <summary>
/// ADR-0012 D2.2 — the out-of-band <see cref="IAuditFailureSink"/>. On a business-failure or a thrown
/// exception the action's scoped transaction never commits (and on the exception path is doomed), so the
/// failure row must NOT ride it. This sink opens its OWN short-lived scope + <see cref="CleansiaDbContext"/>
/// and commits the row independently, so the failed-action record survives the rolled-back action. The
/// tenant is read from the request-scoped <see cref="ITenantProvider"/> (the ambient JWT/HttpContext is
/// still in scope) and stamped onto the row — <see cref="AdminActionAudit"/> is not <c>Auditable</c>, so
/// <c>CommitAsync</c> would not stamp it. The behavior wraps this call and swallows: a failure here never
/// changes the error returned to the admin (D2.2).
/// </summary>
public sealed class OutOfBandAuditFailureSink(
    IServiceScopeFactory serviceScopeFactory,
    ITenantProvider tenantProvider) : IAuditFailureSink
{
    public async Task RecordFailureAsync(AdminActionAudit entry, CancellationToken cancellationToken)
    {
        entry.TenantId ??= tenantProvider.GetCurrentTenantId();

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();

        context.AdminActionAudits.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
