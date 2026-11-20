using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IReceiptService
{
    /// <summary>
    /// ADR-0004 D-F4.1 phase 1 — RESERVE the receipt. Allocates the fiscal sequence,
    /// <c>OrderReceipt.Create</c>s the row, <c>Add</c>s it to the repository, and marks it owed to the retry
    /// job, so a fault or crash before its PDF is stored is recoverable (C-A). Does NOT call the fiscal authority, does NOT generate the
    /// PDF, and does NOT commit — the caller (the handler) owns the claim commit, which MUST land
    /// BEFORE the irreversible external effect performed by <see cref="RealizeFiscalAndPdfAsync"/>.
    /// </summary>
    Task<OrderReceipt> ReserveReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same reservation for the fee receipt of a paid receivable on <paramref name="order"/>: its own
    /// number from the same counter, beside the order's sale receipt. Realized and retried by the same two
    /// methods below, which read the fee from the receipt's receivable instead of the order's sale.
    /// </summary>
    Task<OrderReceipt> ReserveFeeReceiptAsync(
        Order order, Receivable receivable, string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-0004 D-F4.1 phase 2 — REALIZE the external effects for an already-claimed receipt:
    /// register with the country's fiscal authority (stamping <c>SetFiscalData</c> on success — which
    /// settles the claim once the PDF is stored — or <c>MarkFiscalRegistrationFailed</c> on failure), then
    /// generate the PDF and upload it to blob storage. Called AFTER the claim has been committed, so a
    /// redelivery is already deduped by the committed receipt row.
    /// </summary>
    /// <remarks>The document's language is the one <see cref="ReserveReceiptAsync"/> recorded on the row.</remarks>
    Task RealizeFiscalAndPdfAsync(Order order, OrderReceipt receipt, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadReceiptPdfAsync(OrderReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>Whether the file the receipt points at is in storage.</summary>
    Task<bool> IsPdfStoredAsync(OrderReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes what a due receipt still owes: its fiscal registration, where the market requires one and
    /// it has no code yet, then a render as a new numbered file when <paramref name="pdfStored"/> is false
    /// or the receipt carries a code its file lacks; the files before it are kept (→ /decisions/adr-0083).
    /// Returns <c>true</c> when the receipt's file is stored and its e-mail may go: in a market that holds
    /// the e-mail, only with the code. Updates the receipt's retry state but does NOT commit. The
    /// <paramref name="order"/> must be fully loaded (services, packages, currency, address).
    /// </summary>
    Task<bool> CompleteReceiptAsync(
        OrderReceipt receipt, Order order, bool pdfStored, CancellationToken cancellationToken = default);
}
