using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IEmailService
{
    Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendOrderReceiptEmailAsync(string email, Order order, byte[]? pdfBytes = null, string fileName = "receipt.pdf", string languageCode = Constants.Language.English, CancellationToken ct = default, string? guestAccessToken = null);

    Task<string> SendTestOrderReceiptEmailAsync(string email, string customerName, string orderNumber, string orderDate, string totalAmount, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, string languageCode, CancellationToken ct = default);

    Task<string> SendPeriodClosedEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, string languageCode = Constants.Language.English, byte[]? invoicePdfBytes = null, string? invoiceFileName = null, CancellationToken ct = default);

    Task<string> SendPeriodEndReminderEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, int daysRemaining, string periodLabel, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// Sends a first-order promo code. Rendered from <c>email-templates/promo-code.html</c>
    /// in this repository rather than from a hosted SendGrid template.
    /// </summary>
    Task<string> SendPromoCodeEmailAsync(string email, string promoCode, string discountLabel, DateTime? expiresOn, string languageCode = Constants.Language.English, CancellationToken ct = default);

    Task<string> SendOrderStatusUpdateEmailAsync(string email, Order order, string newStatus, string languageCode = Constants.Language.English, CancellationToken ct = default, decimal? refundedAmount = null, string? guestAccessToken = null);

    /// <summary>
    /// The booking e-mail sent when the contract is concluded, with <paramref name="confirmationPdf"/>
    /// attached: the slot in market time, the address, the free-cancellation window and, for a cash
    /// booking, the amount to pay the cleaner in cash.
    /// </summary>
    Task<string> SendOrderBookedEmailAsync(string email, Order order, int freeCancellationHours, string languageCode = Constants.Language.English, CancellationToken ct = default, string? guestAccessToken = null, byte[]? confirmationPdf = null, string? confirmationFileName = null);

    /// <summary>
    /// The cleaner's copy of a contract for work they accepted for job <paramref name="jobNumber"/>, the
    /// contract attached as a PDF.
    /// </summary>
    Task<string> SendWorkContractEmailAsync(string email, string cleanerName, string jobNumber, byte[] contractPdf, string contractFileName, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// The pay link a customer is e-mailed when their saved card could not be charged for what they owe on
    /// <paramref name="order"/>: what the amount is for, the amount, the order, and <paramref name="payUrl"/>.
    /// </summary>
    Task<string> SendReceivablePayLinkEmailAsync(string email, Order order, Receivable receivable, string payUrl, string languageCode = Constants.Language.English, CancellationToken ct = default);

    /// <summary>
    /// Asks a cleaner to hand over the company's cash they have held since a pay-period close could not set it
    /// off against their pay (owner ruling 2026-09-28, decision 23): the amount and the close it dates from.
    /// </summary>
    Task<string> SendCashRemittanceRequestEmailAsync(string email, string employeeName, decimal amount, string currencySymbol, DateTime carriedSince, string languageCode = Constants.Language.English, CancellationToken ct = default);

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

    /// <summary>
    /// One admin event to one address (ADR-0065 D2): the chrome is one template, the subject and the
    /// one-paragraph body are the event key's copy with <paramref name="args"/> substituted in the
    /// order its catalogue entry declares. Never a person's name — the args are ids, numbers, enum
    /// names, dates and money.
    /// </summary>
    Task<string> SendAdminNotificationEmailAsync(string email, string eventKey, IReadOnlyDictionary<string, string> args, string languageCode = Constants.Language.English, CancellationToken ct = default);
}