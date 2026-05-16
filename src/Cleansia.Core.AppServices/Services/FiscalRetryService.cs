using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public sealed class FiscalRetryService(
    IOrderReceiptRepository receiptRepository,
    IOrderRepository orderRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IReceiptService receiptService,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ILogger<FiscalRetryService> logger)
    : IFiscalRetryService
{
    // Max number of receipts processed per timer tick. Keeps each run bounded.
    private const int BatchSize = 50;

    public async Task<int> ProcessDueRetriesAsync(CancellationToken cancellationToken)
    {
        // System job — no JWT context. The repo bypasses tenant filter for
        // GetDueForRetryAsync; we must set the override per receipt before
        // mutating so child writes inherit the right tenant.
        var receipts = await receiptRepository.GetDueForRetryAsync(DateTime.UtcNow, BatchSize, cancellationToken);
        if (receipts.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("FiscalRetryService picked up {Count} receipts due for retry", receipts.Count);

        var processed = 0;
        foreach (var receipt in receipts)
        {
            try
            {
                // Reset before each receipt so an override from the previous
                // multi-tenant receipt doesn't leak into a single-tenant one.
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(receipt.TenantId))
                {
                    tenantProvider.SetTenantOverride(receipt.TenantId);
                }

                var order = await orderRepository.GetByIdIgnoringTenantAsync(receipt.OrderId, cancellationToken);
                if (order == null)
                {
                    logger.LogWarning(
                        "Skipping fiscal retry for ReceiptNumber={ReceiptNumber} — order {OrderId} not found",
                        receipt.ReceiptNumber, receipt.OrderId);
                    continue;
                }

                var succeeded = await receiptService.RetryFiscalRegistrationAsync(receipt, order, cancellationToken);

                // For BlockingOnline countries the confirmation email is held until the fiscal
                // authority signs the receipt. Once signed, release it now.
                if (succeeded && !receipt.EmailSent)
                {
                    var enforcementMode = await ResolveEnforcementModeAsync(order, cancellationToken);
                    if (enforcementMode is FiscalEnforcementMode.BlockingOnline or FiscalEnforcementMode.BlockingWithOfflineCache)
                    {
                        var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, cancellationToken);
                        var languageCode = receipt.Language?.Code ?? Constants.Language.English;
                        var messageId = await emailService.SendOrderReceiptEmailAsync(
                            order.CustomerEmail, order, pdfBytes, receipt.FileName, languageCode, cancellationToken);
                        receipt.MarkEmailSent(messageId);
                        logger.LogInformation(
                            "Held receipt email released for ReceiptNumber={ReceiptNumber} after successful fiscal retry",
                            receipt.ReceiptNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow per-receipt failures so one bad row doesn't abort the batch.
                // The receipt's retry-tracking state is updated inside RetryFiscalRegistrationAsync.
                logger.LogError(ex,
                    "FiscalRetryService failed to process ReceiptNumber={ReceiptNumber}",
                    receipt.ReceiptNumber);
            }

            processed++;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("FiscalRetryService committed {Processed} retry attempts", processed);
        return processed;
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
