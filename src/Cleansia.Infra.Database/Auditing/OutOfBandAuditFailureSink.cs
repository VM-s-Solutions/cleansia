using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Database.Auditing;

/// <summary>
/// ADR-0012 D2.2 — the out-of-band <see cref="IAuditFailureSink"/>. On a business-failure or a thrown
/// exception the action's scoped transaction never commits (and on the exception path is doomed), so the
/// failure row must NOT ride it. This sink opens its OWN short-lived scope + <see cref="CleansiaDbContext"/>
/// and commits the row independently, so the failed-action record survives the rolled-back action. The
/// tenant is read from the request-scoped <see cref="ITenantProvider"/> (the ambient JWT/HttpContext is
/// still in scope) and stamped onto the row — neither audit row is <c>Auditable</c>, so
/// <c>CommitAsync</c> would not stamp it. The behavior wraps this call and swallows: a failure here never
/// changes the error returned to the caller (D2.2).
///
/// <para>A customer order or dispute failure uses the resource's operator only after proving that
/// the resource belongs to the named customer. Other failures use the subject account's company
/// (ADR-0062 D7), then the ambient tenant when no account resolves. Anonymous account refusals can
/// therefore resolve the subject past the tenant filter without attributing foreign-resource probes
/// to the probed operator.</para>
///
/// <para>A row with no tenant from either source is not written. The one request shape that reaches
/// here without one is an anonymous market-scoped act refused BEFORE <c>OperatorTenantScopeBehavior</c>
/// resolved its operator — <c>country.not_serviced</c> / <c>tenant.not_found</c> — and there is no tenant
/// that row could truthfully carry: the column is NOT NULL, so the insert would fail and the refusal
/// would surface as one error log with a stack trace per probe. It is skipped with one Warning instead.</para>
/// </summary>
public sealed class OutOfBandAuditFailureSink(
    IServiceScopeFactory serviceScopeFactory,
    ITenantProvider tenantProvider,
    ILogger<OutOfBandAuditFailureSink> logger) : IAuditFailureSink
{
    public Task RecordFailureAsync(AdminActionAudit entry, CancellationToken cancellationToken) =>
        WriteAsync(entry, entry.Action, entry.ErrorCode, entry.ActorId, cancellationToken);

    public Task RecordFailureAsync(CustomerActionAudit entry, CancellationToken cancellationToken) =>
        WriteAsync(entry, entry.Action, entry.ErrorCode, entry.UserId, cancellationToken);

    private async Task WriteAsync<TEntry>(
        TEntry entry,
        string action,
        string? errorCode,
        string? subjectUserId,
        CancellationToken cancellationToken)
        where TEntry : BaseEntity, ITenantEntity
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();

        entry.TenantId ??= entry is CustomerActionAudit customer
            ? await OrderOperatorAsync(context, customer, cancellationToken)
            : null;
        entry.TenantId ??= await SubjectTenantAsync(context, subjectUserId, cancellationToken) ?? tenantProvider.GetCurrentTenantId();
        if (entry.TenantId is null)
        {
            logger.LogWarning(
                "Out-of-band audit-failure row for action {Action} ({ErrorCode}) has neither a subject operator nor an ambient tenant and was not recorded.",
                action,
                errorCode);
            return;
        }

        context.Set<TEntry>().Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string?> OrderOperatorAsync(CleansiaDbContext context, CustomerActionAudit entry, CancellationToken cancellationToken)
    {
        if (entry.UserId is null || entry.ResourceId is null
            || !(entry.Action.StartsWith("customer.order.", StringComparison.Ordinal)
                 || entry.Action.StartsWith("customer.dispute.", StringComparison.Ordinal))) return null;

        // A refused probe proves no ownership. Only the subject's own resource can select an operator.
        return entry.ResourceType switch
        {
            "Order" => await context.Orders.IgnoreQueryFilters()
                .Where(o => o.Id == entry.ResourceId && o.UserId == entry.UserId)
                .Select(o => o.TenantId).FirstOrDefaultAsync(cancellationToken),
            "Dispute" => await context.Disputes.IgnoreQueryFilters()
                .Where(d => d.Id == entry.ResourceId && d.UserId == entry.UserId)
                .Select(d => d.TenantId).FirstOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    private static async Task<string?> SubjectTenantAsync(CleansiaDbContext context, string? subjectUserId, CancellationToken cancellationToken) =>
        subjectUserId is null
            ? null
            : await context.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == subjectUserId)
                .Select(u => u.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
}
