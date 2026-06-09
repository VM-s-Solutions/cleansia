using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderReceiptRepository : IRepository<OrderReceipt, string>
{
    Task<OrderReceipt?> GetByOrderIdAndLanguageAsync(
        string orderId,
        string languageCode,
        CancellationToken cancellationToken);

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
}
