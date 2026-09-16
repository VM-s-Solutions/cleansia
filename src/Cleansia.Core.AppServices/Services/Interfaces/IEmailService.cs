using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IEmailService
{
    Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendOrderReceiptEmailAsync(string email, Order order, byte[]? pdfBytes = null, string fileName = "receipt.pdf", string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendTestOrderReceiptEmailAsync(string email, string customerName, string orderNumber, string orderDate, string totalAmount, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, string languageCode, CancellationToken ct = default);

    Task<string> SendPeriodClosedEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, string languageCode = Constants.Language.English, byte[]? invoicePdfBytes = null, string? invoiceFileName = null, CancellationToken ct = default);

    Task<string> SendPeriodEndReminderEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, int daysRemaining, string periodLabel, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// Sends a first-order promo code. Rendered from <c>email-templates/promo-code.html</c>
    /// in this repository rather than from a hosted SendGrid template.
    /// </summary>
    Task<string> SendPromoCodeEmailAsync(string email, string promoCode, string discountLabel, DateTime? expiresOn, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendOrderStatusUpdateEmailAsync(string email, Order order, string newStatus, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// The wind-down notice to a customer of a closing company (ADR-0064 D2 step 1): the company
    /// names as the receipts print them, the last day of service, and what happens to bookings, Plus,
    /// credit and the account.
    /// </summary>
    Task<string> SendCompanyWindDownCustomerNoticeAsync(string email, string userName, IReadOnlyList<string> companyNames, DateOnly windDownFrom, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// The wind-down notice to an approved cleaner of a closing company: the last day of work, the
    /// last pay period, partner sign-in ending at the close, and the customer app for export or erasure.
    /// </summary>
    Task<string> SendCompanyWindDownCleanerNoticeAsync(string email, string userName, IReadOnlyList<string> companyNames, DateOnly windDownFrom, string languageCode = Constants.Language.English, CancellationToken ct = default);
}