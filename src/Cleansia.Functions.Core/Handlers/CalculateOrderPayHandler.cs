using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes the per-cleaner fan-out from <c>CompleteOrder</c> and runs
/// <c>CalculateOrderPay.Command</c> so the order's <c>OrderEmployeePay</c>
/// row exists before the next nightly invoice rollup.
///
/// <para><b>Queue trigger — no tenant context.</b> The envelope carries the order's tenant and it is
/// set as the override BEFORE the open-period read and the command: both go through the tenant filter,
/// and the pay row is stamped from the ambient tenant at commit. An envelope with no tenant is logged
/// and acked — every stamped table is NOT NULL, so under no tenant the open-period bootstrap would fail
/// the insert on every redelivery and poison the queue for nothing. A body that is not an envelope
/// with a payload throws instead: CompleteOrder is the only producer and always envelopes, so there
/// is no bare shape to fall back to, and the poison consumer's stored, alerted dead-letter is the
/// right place for a pay row that will never be created — quieter than that is a lost row.</para>
///
/// Calls <c>IPayPeriodBackgroundService.EnsureOpenPeriodAsync</c> first so
/// pay-calc never fails with <c>NoActivePeriod</c> on fresh environments —
/// the rest of the validator chain (assignment, pay config, duplicate
/// guard) still applies and rejects cleanly when violated.
///
/// Validator rejections are logged at warning and the message is acked —
/// the request was malformed (e.g. no pay config for that cleaner+service)
/// and retrying won't fix it. Infra failures throw so the queue retries up
/// to <c>maxDequeueCount</c> (host.json).
/// </summary>
public class CalculateOrderPayHandler(
    IMediator mediator,
    IPayPeriodBackgroundService payPeriodService,
    ITenantProvider tenantProvider,
    ArchivedCompanyDeadLetter archivedCompanyDeadLetter,
    ILogger<CalculateOrderPayHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        var (message, tenantId) = ReadPayload(messageText);
        if (message is null)
        {
            throw new InvalidOperationException($"Failed to deserialize CalculateOrderPayMessage envelope: {messageText}");
        }

        if (string.IsNullOrEmpty(message.OrderId) || string.IsNullOrEmpty(message.EmployeeId))
        {
            // Genuinely empty identifiers — permanent, ack (do not poison).
            logger.LogWarning(
                "Discarding CalculateOrderPay message with missing OrderId/EmployeeId: {Message}", messageText);
            return;
        }

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning(
                "Discarding CalculateOrderPay message with no tenant for order {OrderId} / employee {EmployeeId}: the pay row cannot be stamped",
                message.OrderId,
                message.EmployeeId);
            return;
        }

        tenantProvider.SetTenantOverride(tenantId);

        BusinessResult<CalculateOrderPay.Response> result;
        try
        {
            await payPeriodService.EnsureOpenPeriodAsync(ct);

            result = await mediator.Send(
                new CalculateOrderPay.Command(message.OrderId, message.EmployeeId),
                ct);
        }
        catch (CompanyArchivedException ex)
        {
            // Permanent: the company's books are frozen for archive, and a redelivery cannot thaw
            // them. The dead-letter row is the operations record of the pay row that was never written.
            await archivedCompanyDeadLetter.RecordAsync(QueueNames.CalculateOrderPay, messageText, ex, ct);
            return;
        }

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "CalculateOrderPay succeeded for order {OrderId} / employee {EmployeeId} → pay row {PayId}",
                message.OrderId,
                message.EmployeeId,
                result.Value?.EmployeePayrollId);
        }
        else
        {
            // Validator rejected (already-calculated, missing config, etc.).
            // Don't throw — retrying won't change the validator's verdict and
            // we don't want to poison-queue a permanent business-rule miss.
            logger.LogWarning(
                "CalculateOrderPay rejected for order {OrderId} / employee {EmployeeId}: {Error}",
                message.OrderId,
                message.EmployeeId,
                result.Error?.Message ?? "unknown");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static (CalculateOrderPayMessage? Message, string? TenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<CalculateOrderPayMessage>>(messageText, JsonOptions);
            return (envelope?.Payload, envelope?.TenantId);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
