using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Consumes the per-employee <c>generate-invoice</c> fan-out and runs
/// <c>GenerateInvoice.Command</c> via MediatR so an <c>EmployeeInvoice</c> is created and the
/// employee's unpaid <c>OrderEmployeePay</c> rows are assigned to it.
///
/// Queue trigger — no JWT/tenant context. The employee is looked up cross-tenant by the trusted
/// payload id and its TenantId is set as the override BEFORE the command runs, so the new invoice and
/// the order-pay assignments are stamped with the right tenant (no cross-tenant leak).
///
/// Validator rejections (invoice already exists, no unpaid pays, employee/period gone) are logged at
/// warning and acked — retrying won't change the verdict. The already-exists guard is also what makes
/// an at-least-once redelivery safe: the second pass rejects and acks as a no-op. Infra failures throw
/// so the queue retries up to <c>maxDequeueCount</c> (host.json).
/// </summary>
public class GenerateInvoiceHandler(
    IMediator mediator,
    IEmployeeRepository employeeRepository,
    ITenantProvider tenantProvider,
    ILogger<GenerateInvoiceHandler> logger)
{
    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ADR-0002 D2.1a — DUAL-READ at the deploy boundary: read the QueueEnvelope<T> first, fall back
        // to the bare body (in-flight pre-envelope messages must not poison).
        var message = ReadPayload(messageText)
            ?? throw new InvalidOperationException($"Failed to deserialize GenerateInvoiceMessage: {messageText}");

        if (string.IsNullOrEmpty(message.EmployeeId) || string.IsNullOrEmpty(message.PayPeriodId))
        {
            logger.LogWarning(
                "Discarding GenerateInvoice message with missing EmployeeId/PayPeriodId: {Message}", messageText);
            return;
        }

        // Queue trigger — no JWT context. Look up cross-tenant by the trusted EmployeeId, then set the
        // tenant override so the EmployeeInvoice and OrderEmployeePay.AssignToInvoice writes inherit the
        // right TenantId.
        var employee = await employeeRepository.GetByIdIgnoringTenantAsync(message.EmployeeId, ct);
        if (employee is null)
        {
            // Target-not-found stays TRANSIENT/bounded-retry — throw so the queue retries (a re-enqueue
            // can race brief read-replica lag); it is NOT reclassified to a permanent ack.
            logger.LogWarning("Employee {EmployeeId} not found, will retry via queue visibility timeout",
                message.EmployeeId);
            throw new InvalidOperationException($"Employee {message.EmployeeId} not found");
        }

        if (!string.IsNullOrEmpty(employee.TenantId))
        {
            tenantProvider.SetTenantOverride(employee.TenantId);
        }

        var result = await mediator.Send(
            new GenerateInvoice.Command(message.EmployeeId, message.PayPeriodId), ct);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "GenerateInvoice succeeded for employee {EmployeeId} / period {PayPeriodId} → invoice {InvoiceId}",
                message.EmployeeId, message.PayPeriodId, result.Value?.InvoiceId);
        }
        else
        {
            // Validator rejected (already-invoiced, no unpaid pays, etc.). Don't throw — retrying won't
            // change the verdict and we don't want to poison a permanent business-rule miss. The
            // already-exists rejection is also the idempotency dedup for at-least-once redelivery.
            logger.LogWarning(
                "GenerateInvoice rejected for employee {EmployeeId} / period {PayPeriodId}: {Error}",
                message.EmployeeId, message.PayPeriodId, result.Error?.Message ?? "unknown");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static GenerateInvoiceMessage? ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<GenerateInvoiceMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { EmployeeId: { Length: > 0 } } payload)
            {
                return payload;
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        try
        {
            return JsonSerializer.Deserialize<GenerateInvoiceMessage>(messageText, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
