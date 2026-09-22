using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// The operations record of a write a frozen company's books refused where refusing the caller is
/// not an option (ADR-0064 D3): Stripe must always see 2xx, and a queue consumer that can arrive late
/// must ack rather than retry for days. The row is written from a scope of its own — the scope that
/// threw still tracks the refused rows and would throw again — under the frozen company's own
/// tenant, so it is the company's to find. A <c>DeadLetter</c> is an envelope and passes the guard.
/// </summary>
public sealed class ArchivedCompanyDeadLetter(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchivedCompanyDeadLetter> logger)
{
    public static string Describe(CompanyArchivedException exception) =>
        $"{BusinessErrorMessage.TenantArchived}:{exception.TenantId}";

    public async Task RecordAsync(string sourceQueue, string body, CompanyArchivedException exception, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(exception.TenantId);
        await scope.ServiceProvider.GetRequiredService<IDeadLetterStore>()
            .RecordAsync(sourceQueue, body, Describe(exception), cancellationToken);

        logger.LogError(
            exception,
            "A write against the frozen company {TenantId} arriving on {SourceQueue} was refused and dead-lettered ({Bytes} bytes); the row is the operations record.",
            exception.TenantId,
            sourceQueue,
            body.Length);
    }
}
