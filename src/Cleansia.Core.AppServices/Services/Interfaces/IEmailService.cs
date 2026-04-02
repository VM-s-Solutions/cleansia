using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IEmailService
{
    Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, string languageCode = "en", CancellationToken ct = default);

    Task<string> SendOrderReceiptEmailAsync(string email, Order order, byte[]? pdfBytes = null, string fileName = "receipt.pdf", string languageCode = "en", CancellationToken ct = default);

    Task<string> SendTestOrderReceiptEmailAsync(string email, string customerName, string orderNumber, string orderDate, string totalAmount, string languageCode = "en", CancellationToken ct = default);

    Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, string languageCode, CancellationToken ct = default);

    Task<string> SendPeriodClosedEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, string languageCode = "en", byte[]? invoicePdfBytes = null, string? invoiceFileName = null, CancellationToken ct = default);

    Task<string> SendPeriodEndReminderEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, int daysRemaining, string periodLabel, string languageCode = "en", CancellationToken ct = default);

    Task<string> SendOrderStatusUpdateEmailAsync(string email, Order order, string newStatus, string languageCode = "en", CancellationToken ct = default);
}