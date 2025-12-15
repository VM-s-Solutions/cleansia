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

    Task<int> GetNextSequenceForYearAsync(
        int year,
        CancellationToken cancellationToken);
}
