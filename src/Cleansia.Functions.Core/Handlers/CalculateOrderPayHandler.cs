using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
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
/// and the pay row is stamped from the ambient tenant at commit. A body with no tenant is logged and
/// acked — every stamped table is NOT NULL, so under no tenant the open-period bootstrap would fail
/// the insert on every redelivery and poison the queue for nothing.</para>
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
    ILogger<CalculateOrderPayHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ADR-0002 D2.1a — DUAL-READ at the deploy boundary. CompleteOrder wraps the payload in a
        // QueueEnvelope<T>; a bare pre-envelope body still parses so it is acked below as a message
        // with no tenant rather than thrown as malformed. (Deserializing the bare type against an
        // envelope yields a NON-null message with empty OrderId/EmployeeId — it passes a null check but
        // the validator then silently rejects, so no pay row is ever created. The dual-read prevents that.)
        var (message, tenantId) = ReadPayload(messageText);
        if (message is null)
        {
            throw new InvalidOperationException($"Failed to deserialize CalculateOrderPayMessage: {messageText}");
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

        await payPeriodService.EnsureOpenPeriodAsync(ct);

        var result = await mediator.Send(
            new CalculateOrderPay.Command(message.OrderId, message.EmployeeId),
            ct);

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

    /// <summary>
    /// ADR-0002 D2.1a dual-read. Returns the <see cref="CalculateOrderPayMessage"/> payload and the
    /// envelope's tenant from the <see cref="QueueEnvelope{T}"/> wire shape (discriminated by a non-empty
    /// OrderId) or the bare (pre-envelope) message, which carries no tenant; a <c>null</c> message only
    /// when neither shape is parseable.
    /// </summary>
    private static (CalculateOrderPayMessage? Message, string? TenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<CalculateOrderPayMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { OrderId: { Length: > 0 } } payload)
            {
                return (payload, envelope.TenantId);
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        try
        {
            return (JsonSerializer.Deserialize<CalculateOrderPayMessage>(messageText, JsonOptions), null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
