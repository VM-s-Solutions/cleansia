using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Database.Gdpr;

/// <summary>
/// The out-of-band <see cref="IGdprDeletionFailureSink"/>. A failed erasure's own transaction never
/// commits, so the request row it staged goes with it; this sink opens its OWN short-lived scope and
/// <see cref="CleansiaDbContext"/> and commits the <c>Failed</c> row independently. The row is found by
/// the id the attempt was working under: absent, it is the first attempt's rolled-back row and is written
/// fresh under that same id; present, it is a retried request and the note is appended to it. The tenant
/// is read from the request-scoped <see cref="ITenantProvider"/> — the ambient claim, or the override a
/// sweep set — because the fresh scope's own provider carries neither.
/// </summary>
public sealed class OutOfBandGdprDeletionFailureSink(
    IServiceScopeFactory serviceScopeFactory,
    ITenantProvider tenantProvider,
    ILogger<OutOfBandGdprDeletionFailureSink> logger) : IGdprDeletionFailureSink
{
    public async Task RecordFailureAsync(
        string subjectUserId,
        string requestId,
        string processedBy,
        string note,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (tenantId is null)
        {
            logger.LogWarning(
                "Failed erasure of request {RequestId} has no ambient tenant and was not recorded.",
                requestId);
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();

        var request = await context.GdprRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            request = GdprRequest.Create(subjectUserId, GdprRequest.DeletionRequestType);
            request.Id = requestId;
            request.TenantId = tenantId;
            context.GdprRequests.Add(request);
        }

        request.MarkFailed(processedBy, note);
        await context.CommitAsync(cancellationToken);
    }
}
