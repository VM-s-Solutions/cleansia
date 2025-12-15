using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Receipts;

public class OrderReceipt : Auditable
{
    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; private set; } = default!;

    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    public DateTime IssuedAt { get; private set; }

    [MaxLength(100)]
    public string FileName { get; private set; } = default!;

    [MaxLength(100)]
    public string BlobName { get; private set; } = default!;

    public string LanguageId { get; private set; } = default!;
    public Language? Language { get; private set; }

    public bool EmailSent { get; private set; }
    public DateTime? EmailSentAt { get; private set; }

    [MaxLength(255)]
    public string? EmailMessageId { get; private set; }

    public static OrderReceipt Create(
        string orderId,
        string receiptNumber,
        string fileName,
        string blobName,
        string languageId)
    {
        return new OrderReceipt
        {
            OrderId = orderId,
            ReceiptNumber = receiptNumber,
            IssuedAt = DateTime.UtcNow,
            FileName = fileName,
            BlobName = blobName,
            LanguageId = languageId,
            EmailSent = false
        };
    }

    public void MarkEmailSent(string messageId)
    {
        EmailSent = true;
        EmailSentAt = DateTime.UtcNow;
        EmailMessageId = messageId;
    }
}
