using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IReceiptService
{
    Task<OrderReceipt> GenerateReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadReceiptPdfAsync(OrderReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries fiscal registration for a previously-failed receipt. On success, regenerates the
    /// PDF with the fiscal code and re-uploads it to the blob store. Returns <c>true</c> if the
    /// fiscal authority issued a code; <c>false</c> if the retry failed. This method updates the
    /// receipt's retry tracking state (RetryCount, NextRetryAt, etc.) but does NOT commit.
    /// The <paramref name="order"/> must be fully loaded (services, packages, currency, address).
    /// </summary>
    Task<bool> RetryFiscalRegistrationAsync(OrderReceipt receipt, Order order, CancellationToken cancellationToken = default);
}
