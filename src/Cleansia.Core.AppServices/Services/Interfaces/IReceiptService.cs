using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IReceiptService
{
    /// <summary>
    /// T-0119 / ADR-0004 D-F4.1 phase 1 — RESERVE the receipt. Allocates the fiscal sequence,
    /// <c>OrderReceipt.Create</c>s the row, <c>Add</c>s it to the repository, and (for any
    /// <c>enforcementMode != None</c>) marks it BORN RETRY-ELIGIBLE so a crash before registration is
    /// recoverable by the retry job (C-A). Does NOT call the fiscal authority, does NOT generate the
    /// PDF, and does NOT commit — the caller (the handler) owns the claim commit, which MUST land
    /// BEFORE the irreversible external effect performed by <see cref="RealizeFiscalAndPdfAsync"/>.
    /// </summary>
    Task<OrderReceipt> ReserveReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// T-0119 / ADR-0004 D-F4.1 phase 2 — REALIZE the external effects for an already-claimed receipt:
    /// register with the country's fiscal authority (stamping <c>SetFiscalData</c> on success — which
    /// clears the born-retry-eligibility — or <c>MarkFiscalRegistrationFailed</c> on failure), then
    /// generate the PDF and upload it to blob storage. Called AFTER the claim has been committed, so a
    /// redelivery is already deduped by the committed receipt row.
    /// </summary>
    Task RealizeFiscalAndPdfAsync(Order order, OrderReceipt receipt, string languageCode, CancellationToken cancellationToken = default);

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
