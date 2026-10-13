using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Core.AppServices.Services;

public sealed class FiscalRetryService(
    IOrderReceiptRepository receiptRepository,
    IOrderRepository orderRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IReceiptService receiptService,
    IEmailService emailService,
    GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ILogger<FiscalRetryService> logger,
    IServiceScopeFactory serviceScopeFactory)
    : IFiscalRetryService
{
    // Max number of receipts processed per timer tick. Keeps each run bounded.
    private const int BatchSize = 50;

    public async Task<int> ProcessDueRetriesAsync(CancellationToken cancellationToken)
    {
        var dueAt = DateTime.UtcNow;
        var receipts = await receiptRepository.GetDueForRetryAsync(dueAt, BatchSize, cancellationToken);
        if (receipts.Count == 0) return 0;
        logger.LogInformation("FiscalRetryService picked up {Count} receipts due for retry", receipts.Count);
        var processed = 0;
        foreach (var receiptId in receipts.Select(r => r.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<FiscalRetryService>()
                    .ProcessReceiptAsync(receiptId, dueAt, cancellationToken)) processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                processed++;
                logger.LogError(ex, "FiscalRetryService failed to process receipt {ReceiptId}; its scope is discarded", receiptId);
            }
        }
        logger.LogInformation("FiscalRetryService processed {Processed} retry attempts", processed);
        return processed;
    }

    private async Task<bool> ProcessReceiptAsync(string receiptId, DateTime dueAt, CancellationToken cancellationToken)
    {
        var receipt = await receiptRepository.GetQueryableIgnoringTenant()
            .Include(r => r.Language)
            .Include(r => r.Receivable)
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
        if (receipt is null || receipt.FiscalCode is not null || receipt.FiscalNextRetryAt is null
            || receipt.FiscalNextRetryAt > dueAt || string.IsNullOrEmpty(receipt.TenantId)) return false;

        tenantProvider.ClearTenantOverride();
        tenantProvider.SetTenantOverride(receipt.TenantId);
        var order = await orderRepository.GetByIdIgnoringTenantAsync(receipt.OrderId, cancellationToken);
        if (order is null || order.TenantId != receipt.TenantId)
        {
            logger.LogWarning("Skipping fiscal retry for receipt {ReceiptId}: no order under its operator", receiptId);
            return false;
        }

        var succeeded = await receiptService.RetryFiscalRegistrationAsync(receipt, order, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await unitOfWork.CommitAsync(cancellationToken);
        if (succeeded && !receipt.EmailSent && !receipt.IsFee)
        {
            var enforcementMode = await ResolveEnforcementModeAsync(order, cancellationToken);
            if (enforcementMode is FiscalEnforcementMode.BlockingOnline or FiscalEnforcementMode.BlockingWithOfflineCache)
            {
                var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, cancellationToken);
                var languageCode = receipt.Language?.Code ?? Constants.Language.English;
                cancellationToken.ThrowIfCancellationRequested();

                // The email and guest link are sent only after their claim commits. A crash after that
                // claim can lose the email; its durable marker prevents a second send.
                receipt.ClaimEmailSend();
                var guestAccessToken = guestAccessTokenIssuer.IssueForGuest(order);
                await unitOfWork.CommitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var messageId = await emailService.SendOrderReceiptEmailAsync(
                    order.CustomerEmail, order, pdfBytes, receipt.FileName, languageCode, cancellationToken,
                    guestAccessToken);

                // The claim is already durable; provider metadata is best effort inside this child.
                receipt.MarkEmailSent(messageId);
                await unitOfWork.CommitAsync(cancellationToken);
                logger.LogInformation("Held receipt email released for ReceiptNumber={ReceiptNumber} after successful fiscal retry", receipt.ReceiptNumber);
            }
        }
        return true;
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
