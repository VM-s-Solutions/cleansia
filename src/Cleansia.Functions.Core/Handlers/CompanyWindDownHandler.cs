using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes one <c>company-wind-down</c> message and runs the sweep for the company it names
/// (ADR-0064 D2). The tenant comes from the envelope and is set as the override before the first
/// read — every read below is filtered to that company and every commit stamps it. A body that is
/// not an envelope naming a company is permanent: logged and acked, because a redelivery cannot make
/// it name one. What the sweep itself discards as permanent — a missing company, no date, a frozen
/// company — it logs and returns; anything else throws so the runtime redelivers and, after
/// <c>maxDequeueCount</c>, the poison twin dead-letters it. Every step commits alone, so a redelivery
/// resumes past what is done.
/// </summary>
public class CompanyWindDownHandler(
    ICompanyWindDownService companyWindDownService,
    ITenantProvider tenantProvider,
    ILogger<CompanyWindDownHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        var (message, envelopeTenantId) = ReadPayload(messageText);
        var tenantId = !string.IsNullOrEmpty(envelopeTenantId) ? envelopeTenantId : message?.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning(
                "Discarding company wind-down message naming no company ({Bytes} bytes, permanent)", messageText.Length);
            return;
        }

        tenantProvider.SetTenantOverride(tenantId);

        var summary = await companyWindDownService.RunAsync(tenantId, ct);
        if (!summary.Ran)
        {
            logger.LogWarning(
                "Company wind-down message for {TenantId} discarded as permanent: {Reason}", tenantId, summary.SkippedBecause);
        }
    }

    private static (CompanyWindDownMessage? Message, string? EnvelopeTenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<CompanyWindDownMessage>>(messageText, JsonOptions);
            return (envelope?.Payload, envelope?.TenantId);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
