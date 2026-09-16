using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes one <c>company-archive</c> message and builds the bundle for the frozen company it names
/// (ADR-0064 D3). The tenant comes from the envelope and is set as the override before the first
/// read. A body that is not an envelope naming a company and a request instant is permanent:
/// logged and acked. What the build itself discards as permanent — a missing, unfrozen or already
/// archived company, or a request the row no longer carries — it logs and returns; anything else
/// throws so the runtime redelivers into the same folder and, after <c>maxDequeueCount</c>, the
/// poison twin dead-letters it and the admin page offers "Build archive again".
/// </summary>
public class CompanyArchiveHandler(
    ICompanyArchiveService companyArchiveService,
    ITenantProvider tenantProvider,
    ILogger<CompanyArchiveHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        var (message, envelopeTenantId) = ReadPayload(messageText);
        var tenantId = !string.IsNullOrEmpty(envelopeTenantId) ? envelopeTenantId : message?.TenantId;
        if (message is null || string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning(
                "Discarding company archive message naming no company ({Bytes} bytes, permanent)", messageText.Length);
            return;
        }

        tenantProvider.SetTenantOverride(tenantId);

        var summary = await companyArchiveService.RunAsync(tenantId, message.RequestedOn, ct);
        if (!summary.Ran)
        {
            logger.LogWarning(
                "Company archive message for {TenantId} discarded as permanent: {Reason}", tenantId, summary.SkippedBecause);
        }
    }

    private static (CompanyArchiveMessage? Message, string? EnvelopeTenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<CompanyArchiveMessage>>(messageText, JsonOptions);
            return (envelope?.Payload, envelope?.TenantId);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
