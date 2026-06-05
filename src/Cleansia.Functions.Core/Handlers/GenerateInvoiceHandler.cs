using System.Text.Json;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class GenerateInvoiceHandler(
    ILogger<GenerateInvoiceHandler> logger)
{
    public Task HandleAsync(string messageText, CancellationToken ct)
    {
        // ADR-0002 D2.1a — DUAL-READ at the deploy boundary (PR review #8). FiscalReconciliationService
        // already enqueues QueueEnvelope<GenerateInvoiceMessage>; without this the payload would nest
        // under "payload" and bind to empty ids the moment this stub is implemented. Added now so the
        // wire contract is correct ahead of the PDF-generation extraction below.
        var message = ReadPayload(messageText)
            ?? throw new InvalidOperationException($"Failed to deserialize GenerateInvoiceMessage: {messageText}");

        logger.LogInformation("Invoice generation for employee {EmployeeId} in period {PayPeriodId} — not yet implemented",
            message.EmployeeId, message.PayPeriodId);

        // TODO: Extract invoice PDF generation from PayPeriodBackgroundService into this function
        // For now, invoice PDFs are still generated inline within the timer function

        return Task.CompletedTask;
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
