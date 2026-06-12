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

                // Persist this receipt's retry-tracking + fiscal stamp NOW, before the terminal email and
                // before moving to the next receipt. A per-receipt commit means a later receipt's commit
                // fault cannot roll back the work already durably written for this one.
                await unitOfWork.CommitAsync(cancellationToken);

                // For BlockingOnline countries the confirmation email is held until the fiscal
                // authority signs the receipt. Once signed, release it now.
                if (succeeded && !receipt.EmailSent)
                {
                    var enforcementMode = await ResolveEnforcementModeAsync(order, cancellationToken);
                    if (enforcementMode is FiscalEnforcementMode.BlockingOnline or FiscalEnforcementMode.BlockingWithOfflineCache)
                    {
                        var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, cancellationToken);
                        var languageCode = receipt.Language?.Code ?? Constants.Language.English;

                        // ADR-0002 D2.2 — claim-first. Commit the EmailSent marker BEFORE the send so a
                        // commit fault leaves the email un-sent (re-sendable next tick), never sent-but-
                        // unmarked (which would re-send). The accepted residual is a rare lost email on a
                        // crash between this claim commit and the send.
                        receipt.ClaimEmailSend();
                        await unitOfWork.CommitAsync(cancellationToken);

                        var messageId = await emailService.SendOrderReceiptEmailAsync(
                            order.CustomerEmail, order, pdfBytes, receipt.FileName, languageCode, cancellationToken);

                        // Best-effort metadata stamp. The at-most-once guarantee is already secured by the
                        // committed claim above; this records the provider message id for observability.
                        receipt.MarkEmailSent(messageId);
                        await unitOfWork.CommitAsync(cancellationToken);

                        logger.LogInformation(
                            "Held receipt email released for ReceiptNumber={ReceiptNumber} after successful fiscal retry",
                            receipt.ReceiptNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow per-receipt failures so one bad row doesn't abort the batch. The remainder of
                // the batch is still processed and its already-committed receipts stay durable; this
                // receipt's unpersisted work simply stays due for the next tick. Discard this receipt's
                // pending in-memory mutations so a failed commit cannot bleed into the next receipt's.
                unitOfWork.Rollback();
                logger.LogError(ex,
                    "FiscalRetryService failed to process ReceiptNumber={ReceiptNumber}",
                    receipt.ReceiptNumber);
            }

            processed++;
        }

        logger.LogInformation("FiscalRetryService processed {Processed} retry attempts", processed);
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
