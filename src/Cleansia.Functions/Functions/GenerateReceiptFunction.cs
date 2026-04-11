using System.Text.Json;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class GenerateReceiptFunction(
    IOrderRepository orderRepository,
    IReceiptService receiptService,
    IEmailService emailService,
    ICountryConfigurationRepository countryConfigurationRepository,
    IUnitOfWork unitOfWork,
    ILogger<GenerateReceiptFunction> logger)
{
    [Function("GenerateReceipt")]
    public async Task Run(
        [QueueTrigger("generate-receipt", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
    {
        GenerateReceiptMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<GenerateReceiptMessage>(messageText,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? throw new InvalidOperationException($"Failed to deserialize GenerateReceiptMessage: {messageText}");

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
            logger.LogInformation("Receipt PDF generated for order {OrderId}, downloading...", message.OrderId);

            // BlockingOnline countries (DE TSE, AT RKSV, ES VeriFactu) legally require the
            // fiscal signature on the receipt before it can be delivered. If the initial
            // fiscal attempt failed, hold the email — the retry job will release it once
            // the authority issues the signature.
            var enforcementMode = await ResolveEnforcementModeAsync(order, ct);
            var isBlockingMode = enforcementMode is FiscalEnforcementMode.BlockingOnline
                or FiscalEnforcementMode.BlockingWithOfflineCache;

            if (isBlockingMode && receipt.FiscalCode == null)
            {
                logger.LogWarning(
                    "Holding receipt email for order {OrderId} — country requires fiscal signature and retry is pending",
                    message.OrderId);
                await unitOfWork.CommitAsync(ct);
                return;
            }

            var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, ct);
            logger.LogInformation("Receipt PDF downloaded ({Size} bytes), sending email...", pdfBytes.Length);

            var emailMessageId = await emailService.SendOrderReceiptEmailAsync(
                order.CustomerEmail, order, pdfBytes, receipt.FileName, message.LanguageCode, ct);
            receipt.MarkEmailSent(emailMessageId);

            await unitOfWork.CommitAsync(ct);

            logger.LogInformation("Receipt generated and email sent for order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate receipt for order {OrderId}. Message: {Message}",
                message?.OrderId ?? "unknown", messageText);
            throw; // Re-throw so Azure Functions retries via queue
        }
    }

    private async Task<FiscalEnforcementMode> ResolveEnforcementModeAsync(
        Cleansia.Core.Domain.Orders.Order order,
        CancellationToken cancellationToken)
    {
        var countryId = order.CustomerAddress?.CountryId;
        if (countryId == null)
        {
            return FiscalEnforcementMode.None;
        }

        var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
        return countryConfig?.FiscalEnforcementMode ?? FiscalEnforcementMode.None;
    }
}
