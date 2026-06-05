using System.Text.Json;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class GenerateInvoiceHandler(
    ILogger<GenerateInvoiceHandler> logger)
{
    public Task HandleAsync(string messageText, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<GenerateInvoiceMessage>(messageText,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize GenerateInvoiceMessage");

        logger.LogInformation("Invoice generation for employee {EmployeeId} in period {PayPeriodId} — not yet implemented",
            message.EmployeeId, message.PayPeriodId);

        // TODO: Extract invoice PDF generation from PayPeriodBackgroundService into this function
        // For now, invoice PDFs are still generated inline within the timer function

        return Task.CompletedTask;
    }
}
