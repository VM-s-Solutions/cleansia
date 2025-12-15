using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IReceiptService
{
    Task<OrderReceipt> GenerateReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadReceiptPdfAsync(OrderReceipt receipt, CancellationToken cancellationToken = default);
}
