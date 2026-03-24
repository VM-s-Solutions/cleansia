using System.Text.Json;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class GenerateReceiptFunction(
    IOrderRepository orderRepository,
    IReceiptService receiptService,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    ILogger<GenerateReceiptFunction> logger)
{
    [Function("GenerateReceipt")]
    public async Task Run(
        [QueueTrigger("generate-receipt", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<GenerateReceiptMessage>(messageText,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException("Failed to deserialize GenerateReceiptMessage");

        logger.LogInformation("Processing receipt generation for order {OrderId}", message.OrderId);

        var order = await orderRepository.GetByIdAsync(message.OrderId, ct);
        if (order is null)
        {
            logger.LogWarning("Order {OrderId} not found, will retry via queue visibility timeout", message.OrderId);
            throw new InvalidOperationException($"Order {message.OrderId} not found");
        }

        // Idempotency: skip if receipt already exists
        if (order.Receipt is not null)
        {
            logger.LogInformation("Receipt already exists for order {OrderId}, skipping", message.OrderId);
            return;
        }

        var receipt = await receiptService.GenerateReceiptAsync(order, message.LanguageCode, ct);
        var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, ct);

        var messageId = await emailService.SendOrderReceiptEmailAsync(
            order.CustomerEmail, order, pdfBytes, receipt.FileName, message.LanguageCode, ct);
        receipt.MarkEmailSent(messageId);

        await unitOfWork.CommitAsync(ct);

        logger.LogInformation("Receipt generated and email sent for order {OrderId}", message.OrderId);
    }
}
