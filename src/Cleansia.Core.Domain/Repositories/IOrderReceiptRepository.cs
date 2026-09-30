using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderReceiptRepository : IRepository<OrderReceipt, string>
{
    Task<List<OrderReceipt>> GetByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns receipts whose fiscal registration previously failed and are due for a retry
    /// (i.e., <c>FiscalNextRetryAt</c> is in the past). Ordered by oldest-due first.
    /// </summary>
    Task<List<OrderReceipt>> GetDueForRetryAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns recent fiscal failures that have not yet been acknowledged by an admin.
    /// Ordered by most-recent-failure first.
    /// </summary>
    Task<List<OrderReceipt>> GetRecentFiscalFailuresAsync(int take, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the company's receipts issued before <paramref name="issuedBefore"/> whose PDF is still
    /// stored, ordered by id and starting after <paramref name="afterId"/>. Through the tenant filter:
    /// the retention job runs under each company's override.
    /// </summary>
    Task<IReadOnlyList<OrderReceipt>> GetStoredBlobsIssuedBeforeAsync(
        DateTime issuedBefore, string? afterId, int take, CancellationToken cancellationToken);
}
