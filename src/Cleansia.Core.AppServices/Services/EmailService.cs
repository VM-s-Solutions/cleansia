using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Exceptions;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Cleansia.Core.AppServices.Services;

public sealed partial class EmailService : IEmailService
{
    // The named IHttpClientFactory client whose pooled, resilience-wrapped handler the SendGrid SDK's
    // transport is built on. Kept in sync with SendGridExtensions.HttpClientName.
    private const string SendGridHttpClientName = "SendGrid";

    private readonly ISendGridConfig sendGridConfig;
    private readonly ILogger<EmailService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IEmailTemplateTranslationRepository emailTemplateTranslationRepository;
    private readonly IEmailTemplateRenderer templateRenderer;
    private readonly ICountryConfigurationRepository countryConfigurationRepository;

    public EmailService(
        ISendGridConfig cfg,
        ILogger<EmailService> log,
        IHttpClientFactory httpClientFactory,
        IEmailTemplateTranslationRepository emailTemplateTranslationRepository,
        IEmailTemplateRenderer templateRenderer,
        ICountryConfigurationRepository countryConfigurationRepository)
    {
        sendGridConfig = cfg;
        logger = log;
        this.httpClientFactory = httpClientFactory;
        this.emailTemplateTranslationRepository = emailTemplateTranslationRepository;
        this.templateRenderer = templateRenderer;
        this.countryConfigurationRepository = countryConfigurationRepository;
    }

    public async Task<string> SendResetPasswordEmailAsync(
        string email,
        string fullUserName,
        string code,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.ResetPassword, languageCode, ct);

        var resetLink = $"{sendGridConfig.ClientDomainUrl}{sendGridConfig.ResetPasswordUrl}?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}";

        // Surface the 6-digit reset code in the subject as "<subject> - [code]", mirroring the email
        // confirmation. translations["Subject"] is overwritten before the values are built, because the
        // body renders {{Subject}} as its heading and the two have to agree.
        var baseSubject = translations.GetValueOrDefault("Subject", "Reset Your Password");
        var subject = $"{baseSubject} - [{code}]";
        translations["Subject"] = subject;

        var values = BuildTemplateValues(translations, new
        {
            UserName = fullUserName,
            VerificationCode = code,
            ResetPasswordLink = resetLink
        }, languageCode);

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.ResetPassword), values),
            subject,
            $"Password reset email to {email}",
            ct);
    }

    /// <summary>
    /// The link the e-mail's button points at, and it is a different page for the two audiences.
    ///
    /// <para>A booking with an account goes to that account's own order detail: the reader signs in and
    /// the server authorises them. It must NOT go to the guest track page — that page proves a booking
    /// with a token, <c>IssueForGuest</c> mints none for an account, and the button would land its owner
    /// on "the link is in your confirmation e-mail" for an e-mail they are already reading.</para>
    ///
    /// <para>A guest's copy carries their per-order access token. Only the caller that just minted one
    /// can supply it (the raw value is never readable again), so a caller with none passes null.</para>
    /// </summary>
    private string BuildOrderStatusLink(Order order, string email, string? guestAccessToken)
        => string.IsNullOrEmpty(order.UserId)
            ? BuildGuestTrackLink(order.DisplayOrderNumber, email, guestAccessToken)
            : $"{sendGridConfig.ClientDomainUrl}/orders/{Uri.EscapeDataString(order.Id)}";

    /// <summary>The market's own clock — the zone the receipt dates the same order in.</summary>
    private async Task<TimeZoneInfo> MarketZoneAsync(Order order, CancellationToken ct) =>
        order.CustomerAddress?.CountryId is not { } countryId
            ? TimeZoneInfo.Utc
            : TimeZoneResolution.Resolve(
                (await countryConfigurationRepository.GetByCountryIdAsync(countryId, ct))?.TimeZoneId);

    private static CultureInfo CultureFor(string languageCode) =>
        CultureInfo.GetCultureInfo(EmailLocale.Resolve(languageCode));

    /// <summary>
    /// An amount the way the e-mail's language writes it, never the host's: "1 234,50 €" in Czech,
    /// "€1,234.50" in English — the receipt's convention. No unit when the currency was not loaded.
    /// </summary>
    private static string Money(decimal amount, string currencySymbol, string languageCode)
    {
        var digits = amount.ToString("N2", CultureFor(languageCode));
        if (string.IsNullOrEmpty(currencySymbol))
        {
            return digits;
        }

        return EmailLocale.Resolve(languageCode) == Constants.Language.English
            ? $"{currencySymbol}{digits}"
            : $"{digits} {currencySymbol}";
    }

    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> ReceiptAfterServiceDefaults =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["SubjectAfterService"] = "Your receipt from Cleansia",
                ["ThankYouTextAfterService"] = "Thank you for your cleaning with Cleansia. Your receipt is attached.",
            },
            ["cs"] = new()
            {
                ["SubjectAfterService"] = "Účtenka za váš úklid",
                ["ThankYouTextAfterService"] = "Děkujeme, že jste si vybrali Cleansia. V příloze najdete účtenku za provedený úklid.",
            },
            ["sk"] = new()
            {
                ["SubjectAfterService"] = "Účtenka za vaše upratovanie",
                ["ThankYouTextAfterService"] = "Ďakujeme, že ste si vybrali Cleansia. V prílohe nájdete účtenku za vykonané upratovanie.",
            },
            ["uk"] = new()
            {
                ["SubjectAfterService"] = "Квитанція за ваше прибирання",
                ["ThankYouTextAfterService"] = "Дякуємо, що обрали Cleansia. Квитанцію за виконане прибирання додано до цього листа.",
            },
            ["ru"] = new()
            {
                ["SubjectAfterService"] = "Квитанция за вашу уборку",
                ["ThankYouTextAfterService"] = "Спасибо, что выбрали Cleansia. Квитанция за выполненную уборку приложена к этому письму.",
            },
        };

    private string BuildGuestTrackLink(string displayOrderNumber, string email, string? guestAccessToken)
    {
        var link = $"{sendGridConfig.ClientDomainUrl}/track-order?orderNumber={Uri.EscapeDataString(displayOrderNumber)}&email={Uri.EscapeDataString(email)}";
        return string.IsNullOrEmpty(guestAccessToken)
            ? link
            : link + $"&token={Uri.EscapeDataString(guestAccessToken)}";
    }

    public async Task<string> SendOrderReceiptEmailAsync(
        string email,
        Order order,
        byte[]? pdfBytes = null,
        string fileName = "receipt.pdf",
        string languageCode = Constants.Language.English,
        CancellationToken ct = default,
        string? guestAccessToken = null)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderReceipt, languageCode, ct);

        // A receipt issued after the clean — every cash receipt — thanks the customer for the clean
        // rather than confirming a booking (owner ruling 2026-09-28). A translation row still wins.
        if (order.CompletedAt is not null)
        {
            var afterService = ReceiptAfterServiceDefaults.GetValueOrDefault(EmailLocale.Resolve(languageCode))
                ?? ReceiptAfterServiceDefaults[Constants.Language.English];
            translations["Subject"] = translations.GetValueOrDefault("SubjectAfterService", afterService["SubjectAfterService"]);
            translations["ThankYouText"] = translations.GetValueOrDefault("ThankYouTextAfterService", afterService["ThankYouTextAfterService"]);
        }

        var orderStatusLink = BuildOrderStatusLink(order, email, guestAccessToken);
        var marketZone = await MarketZoneAsync(order, ct);

        var values = BuildTemplateValues(translations, new
        {
            CustomerName = order.CustomerName,
            OrderNumber = order.DisplayOrderNumber,
            OrderDate = TimeZoneInfo.ConvertTime(order.CreatedOn, marketZone).ToString("d", CultureFor(languageCode)),
            // An unloaded Currency navigation is a loader omission, not a CZK order: no unit rather than a guessed one. → /architecture/platform-expandability#_5-where-czk-kc-is-hardcoded-vs-configurable
            TotalAmount = Money(order.TotalPrice, order.Currency?.Symbol ?? string.Empty, languageCode),
            OrderStatusLink = orderStatusLink
        }, languageCode);

        var subject = translations.GetValueOrDefault("Subject", "Your Order Receipt");

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.OrderReceipt), values),
            subject,
            $"Order receipt email to {email}",
            ct,
            pdfBytes,
            fileName);
    }

    public async Task<string> SendTestOrderReceiptEmailAsync(
        string email,
        string customerName,
        string orderNumber,
        string orderDate,
        string totalAmount,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderReceipt, languageCode, ct);

        var orderStatusLink = BuildGuestTrackLink(orderNumber, email, null);

        var values = BuildTemplateValues(translations, new
        {
            CustomerName = customerName,
            OrderNumber = orderNumber,
            OrderDate = orderDate,
            TotalAmount = totalAmount,
            OrderStatusLink = orderStatusLink
        }, languageCode);

        var subject = "[TEST] " + translations.GetValueOrDefault("Subject", "Your Order Receipt");

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.OrderReceipt), values),
            subject,
            $"Test order receipt email to {email}",
            ct);
    }

    public async Task<string> SendEmailConfirmationAsync(
        string email,
        string userName,
        string verificationCode,
        string languageCode,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.ConfirmationEmail, languageCode, ct);

        // Surface the 6-digit code in the subject line so the user can read it from the inbox list
        // without opening the email, as "<subject> - [code]". translations["Subject"] is overwritten
        // BEFORE the values are built, because the body renders {{Subject}} as its heading and the two
        // have to agree. Since T-0677 the subject on the wire is simply this string — there is no
        // hosted template carrying a subject of its own to lose a race against.
        //
        // Historical note, because it cost a session: while these went out as SendGrid dynamic
        // templates, a template's OWN subject beat the per-personalization subject, so setting only
        // the personalization subject had no visible effect. That hazard left with the template.
        var baseSubject = translations.GetValueOrDefault("Subject", "Confirm Your Email");
        var subject = $"{baseSubject} - [{verificationCode}]";
        translations["Subject"] = subject;

        var values = BuildTemplateValues(translations, new
        {
            UserName = userName,
            VerificationCode = verificationCode
        }, languageCode);

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.ConfirmationEmail), values),
            subject,
            $"Confirmation email to {email}",
            ct);
    }

    public async Task<string> SendPeriodClosedEmailAsync(
        string email,
        string employeeName,
        DateOnly startDate,
        DateOnly endDate,
        DateTime closedAt,
        string periodLabel,
        string languageCode = Constants.Language.English,
        byte[]? invoicePdfBytes = null,
        string? invoiceFileName = null,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.PeriodClosed, languageCode, ct);

        var values = BuildTemplateValues(translations, new
        {
            EmployeeName = employeeName,
            PeriodLabel = periodLabel,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            ClosedAt = closedAt.ToString("yyyy-MM-dd HH:mm:ss UTC")
        }, languageCode);

        var subject = translations.GetValueOrDefault("Subject", "Pay Period Closed");
        var hasInvoice = invoicePdfBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(invoiceFileName);

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.PeriodClosed), values),
            subject,
            hasInvoice
                ? $"Period closed email with invoice to {email}"
                : $"Period closed email to {email}",
            ct,
            hasInvoice ? invoicePdfBytes : null,
            hasInvoice ? invoiceFileName : null);
    }

    public async Task<string> SendPeriodEndReminderEmailAsync(
        string email,
        string employeeName,
        DateOnly startDate,
        DateOnly endDate,
        int daysRemaining,
        string periodLabel,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.PeriodEndReminder, languageCode, ct);

        var daysRemainingText = translations.GetValueOrDefault("DaysRemainingText", "{0} days remaining");
        var formattedDaysRemaining = string.Format(daysRemainingText, daysRemaining);

        var values = BuildTemplateValues(translations, new
        {
            EmployeeName = employeeName,
            PeriodLabel = periodLabel,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            DaysRemaining = formattedDaysRemaining
        }, languageCode);

        var subject = translations.GetValueOrDefault("Subject", "Pay Period Ending Soon");

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.PeriodEndReminder), values),
            subject,
            $"Period end reminder email to {email}",
            ct);
    }

    /// <summary>
    /// In-code copy for the status e-mail's cancelled and booked arms, used only where no translation
    /// row supplies a key. The table carries no status-update rows, and these two arms go to guests and
    /// cash customers in their own language.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> StatusEmailDefaults =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new()
            {
                ["StatusTitle_Cancelled"] = "Order cancelled",
                ["StatusTitle_Booked"] = "Booking confirmed",
                ["StatusMessage_Booked"] = "Your cleaning is booked.",
                ["CashDueMessage"] = "Please pay {0} in cash to the cleaner on the day.",
                ["FreeCancellationMessage"] = "You can cancel free of charge up to {0} h before the cleaning; a later cancellation may be charged a fee.",
                ["StatusMessage_Cancelled"] = "Your order has been cancelled.",
                ["RefundMessage"] = "Refund issued:",
                ["CancelledReason_NoCleaner"] = "No cleaner was available for this booking, so it was cancelled.",
                ["CancelledReason_PaymentNotCompleted"] = "The payment wasn't completed, so this booking was released.",
                ["CancelledReason_ByUs"] = "We had to cancel this booking.",
                ["RefundPendingMessage"] = "Your refund is being processed.",
                ["NothingChargedMessage"] = "Nothing was charged.",
                ["StatusSectionLabel"] = "Current status",
                ["OrderNumberLabel"] = "Order #",
                ["CleaningDateLabel"] = "Cleaning date",
                ["AddressLabel"] = "Address",
                ["TotalLabel"] = "Total",
                ["ButtonText"] = "View order details",
                ["QuestionsText"] = "If you have questions about your order, please contact us.",
                ["SupportText"] = "Need help? Contact us at",
                ["Closing"] = "Best regards,",
                ["TeamName"] = "The Cleansia team",
            },
            ["cs"] = new()
            {
                ["StatusTitle_Cancelled"] = "Rezervace zrušena",
                ["StatusTitle_Booked"] = "Rezervace potvrzena",
                ["StatusMessage_Booked"] = "Váš úklid je zarezervován.",
                ["CashDueMessage"] = "Částku {0} zaplaťte v hotovosti uklízeči v den úklidu.",
                ["FreeCancellationMessage"] = "Zdarma můžete zrušit nejpozději {0} h před úklidem; pozdější zrušení může být zpoplatněno.",
                ["StatusMessage_Cancelled"] = "Vaše rezervace byla zrušena.",
                ["RefundMessage"] = "Vrácená částka:",
                ["CancelledReason_NoCleaner"] = "Pro tuto rezervaci nebyl k dispozici žádný uklízeč, a proto byla zrušena.",
                ["CancelledReason_PaymentNotCompleted"] = "Platba nebyla dokončena, a proto byla tato rezervace uvolněna.",
                ["CancelledReason_ByUs"] = "Tuto rezervaci jsme museli zrušit.",
                ["RefundPendingMessage"] = "Vrácení peněz zpracováváme.",
                ["NothingChargedMessage"] = "Nic vám nebylo účtováno.",
                ["StatusSectionLabel"] = "Aktuální stav",
                ["OrderNumberLabel"] = "Rezervace č.",
                ["CleaningDateLabel"] = "Datum úklidu",
                ["AddressLabel"] = "Adresa",
                ["TotalLabel"] = "Celkem",
                ["ButtonText"] = "Zobrazit detail rezervace",
                ["QuestionsText"] = "Pokud máte dotazy k rezervaci, kontaktujte nás.",
                ["SupportText"] = "Potřebujete pomoc? Kontaktujte nás na",
                ["Closing"] = "S pozdravem",
                ["TeamName"] = "Tým Cleansia",
            },
            ["sk"] = new()
            {
                ["StatusTitle_Cancelled"] = "Rezervácia zrušená",
                ["StatusTitle_Booked"] = "Rezervácia potvrdená",
                ["StatusMessage_Booked"] = "Vaše upratovanie je zarezervované.",
                ["CashDueMessage"] = "Sumu {0} zaplaťte v hotovosti upratovačovi v deň upratovania.",
                ["FreeCancellationMessage"] = "Bezplatne môžete zrušiť najneskôr {0} h pred upratovaním; neskoršie zrušenie môže byť spoplatnené.",
                ["StatusMessage_Cancelled"] = "Vaša rezervácia bola zrušená.",
                ["RefundMessage"] = "Vrátená suma:",
                ["CancelledReason_NoCleaner"] = "Pre túto rezerváciu nebol k dispozícii žiadny upratovač, a preto bola zrušená.",
                ["CancelledReason_PaymentNotCompleted"] = "Platba nebola dokončená, a preto bola táto rezervácia uvoľnená.",
                ["CancelledReason_ByUs"] = "Túto rezerváciu sme museli zrušiť.",
                ["RefundPendingMessage"] = "Vrátenie peňazí spracúvame.",
                ["NothingChargedMessage"] = "Nič vám nebolo účtované.",
                ["StatusSectionLabel"] = "Aktuálny stav",
                ["OrderNumberLabel"] = "Rezervácia č.",
                ["CleaningDateLabel"] = "Dátum upratovania",
                ["AddressLabel"] = "Adresa",
                ["TotalLabel"] = "Spolu",
                ["ButtonText"] = "Zobraziť detail rezervácie",
                ["QuestionsText"] = "Ak máte otázky k rezervácii, kontaktujte nás.",
                ["SupportText"] = "Potrebujete pomoc? Kontaktujte nás na",
                ["Closing"] = "S pozdravom",
                ["TeamName"] = "Tím Cleansia",
            },
            ["uk"] = new()
            {
                ["StatusTitle_Cancelled"] = "Бронювання скасовано",
                ["StatusTitle_Booked"] = "Бронювання підтверджено",
                ["StatusMessage_Booked"] = "Ваше прибирання заброньовано.",
                ["CashDueMessage"] = "Будь ласка, сплатіть {0} готівкою прибиральнику в день прибирання.",
                ["FreeCancellationMessage"] = "Безкоштовно скасувати можна не пізніше ніж за {0} год до прибирання; за пізніше скасування може стягуватися плата.",
                ["StatusMessage_Cancelled"] = "Ваше бронювання скасовано.",
                ["RefundMessage"] = "Сума повернення:",
                ["CancelledReason_NoCleaner"] = "Для цього бронювання не знайшлося вільного прибиральника, тому його скасовано.",
                ["CancelledReason_PaymentNotCompleted"] = "Оплату не було завершено, тому це бронювання було скасовано.",
                ["CancelledReason_ByUs"] = "Нам довелося скасувати це бронювання.",
                ["RefundPendingMessage"] = "Повернення коштів обробляється.",
                ["NothingChargedMessage"] = "З вас нічого не стягнуто.",
                ["StatusSectionLabel"] = "Поточний статус",
                ["OrderNumberLabel"] = "Бронювання №",
                ["CleaningDateLabel"] = "Дата прибирання",
                ["AddressLabel"] = "Адреса",
                ["TotalLabel"] = "Разом",
                ["ButtonText"] = "Переглянути бронювання",
                ["QuestionsText"] = "Якщо у вас є запитання щодо бронювання, зв’яжіться з нами.",
                ["SupportText"] = "Потрібна допомога? Напишіть нам:",
                ["Closing"] = "З повагою",
                ["TeamName"] = "Команда Cleansia",
            },
            ["ru"] = new()
            {
                ["StatusTitle_Cancelled"] = "Бронирование отменено",
                ["StatusTitle_Booked"] = "Бронирование подтверждено",
                ["StatusMessage_Booked"] = "Ваша уборка забронирована.",
                ["CashDueMessage"] = "Пожалуйста, оплатите {0} наличными уборщику в день уборки.",
                ["FreeCancellationMessage"] = "Бесплатно отменить можно не позднее чем за {0} ч до уборки; за более позднюю отмену может взиматься плата.",
                ["StatusMessage_Cancelled"] = "Ваше бронирование отменено.",
                ["RefundMessage"] = "Сумма возврата:",
                ["CancelledReason_NoCleaner"] = "Для этого бронирования не нашлось свободного уборщика, поэтому оно отменено.",
                ["CancelledReason_PaymentNotCompleted"] = "Оплата не была завершена, поэтому это бронирование было отменено.",
                ["CancelledReason_ByUs"] = "Нам пришлось отменить это бронирование.",
                ["RefundPendingMessage"] = "Возврат средств обрабатывается.",
                ["NothingChargedMessage"] = "С вас ничего не списано.",
                ["StatusSectionLabel"] = "Текущий статус",
                ["OrderNumberLabel"] = "Бронирование №",
                ["CleaningDateLabel"] = "Дата уборки",
                ["AddressLabel"] = "Адрес",
                ["TotalLabel"] = "Итого",
                ["ButtonText"] = "Посмотреть бронирование",
                ["QuestionsText"] = "Если у вас есть вопросы о бронировании, свяжитесь с нами.",
                ["SupportText"] = "Нужна помощь? Напишите нам:",
                ["Closing"] = "С уважением",
                ["TeamName"] = "Команда Cleansia",
            },
        };

    public Task<string> SendOrderStatusUpdateEmailAsync(
        string email,
        Order order,
        string newStatus,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default,
        decimal? refundedAmount = null,
        string? guestAccessToken = null) =>
        SendStatusEmailAsync(email, order, newStatus, languageCode, ct, refundedAmount, guestAccessToken, freeCancellationHours: null);

    /// <summary>
    /// The status e-mail's booked arm: what a cash booking gets instead of a receipt, which follows at
    /// completion (owner ruling 2026-09-28) — the amount to pay the cleaner in cash, the slot in market
    /// time, the address and the free-cancellation window.
    /// </summary>
    public Task<string> SendOrderBookedEmailAsync(
        string email,
        Order order,
        int freeCancellationHours,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default,
        string? guestAccessToken = null) =>
        SendStatusEmailAsync(email, order, BookedStatus, languageCode, ct, refundedAmount: null, guestAccessToken, freeCancellationHours);

    // In-code copy only: the status e-mail's translation rows are that e-mail's, and a Subject entered for
    // it would retitle this one.
    public async Task<string> SendReceivablePayLinkEmailAsync(
        string email,
        Order order,
        Receivable receivable,
        string payUrl,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var copy = PayLinkDefaults.GetValueOrDefault(languageCode) ?? PayLinkDefaults[Constants.Language.English];
        var culture = CultureFor(languageCode);
        var subject = string.Format(culture, copy["Subject"], order.DisplayOrderNumber);
        var cleaningTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc), await MarketZoneAsync(order, ct));

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in copy)
        {
            values[key] = value;
        }

        values["lang"] = languageCode;
        values["Subject"] = subject;
        values["StatusClass"] = "cancelled";
        values["StatusLabel"] = ReceiptLabels.For(languageCode).ReceivableKinds[receivable.Kind];
        values["OrderNumber"] = order.DisplayOrderNumber;
        values["CleaningDate"] = cleaningTime.ToString("g", culture);
        values["Address"] = order.CustomerAddress is { } address ? $"{address.Street}, {address.City}" : string.Empty;
        values["Total"] = Money(receivable.Amount, order.Currency?.Symbol ?? string.Empty, languageCode);
        values["OrderStatusLink"] = payUrl;
        values["SupportEmail"] = sendGridConfig.AddressFrom;
        values["FooterText"] = $"© {DateTime.UtcNow.Year} Cleansia";

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.OrderStatusUpdate), values),
            subject,
            $"Receivable pay link for receivable {receivable.Id}",
            ct);
    }

    /// <summary>The pay-link e-mail's copy per locale; <c>{0}</c> in the subject is the order number.</summary>
    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> PayLinkDefaults =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new()
            {
                ["Subject"] = "Payment due for order {0}",
                ["StatusMessage"] = "We could not charge the amount below to your saved card. Please pay it with the button below; the link is valid for 24 hours. Until it is paid, cash bookings are not available, and you can still book and pay by card.",
                ["StatusSectionLabel"] = "Amount due for",
                ["OrderNumberLabel"] = "Order #",
                ["CleaningDateLabel"] = "Cleaning date",
                ["AddressLabel"] = "Address",
                ["TotalLabel"] = "Amount due",
                ["ButtonText"] = "Pay now",
                ["QuestionsText"] = "If you have questions about this payment, please contact us.",
                ["SupportText"] = "Need help? Contact us at",
                ["Closing"] = "Best regards,",
                ["TeamName"] = "The Cleansia team",
            },
            ["cs"] = new()
            {
                ["Subject"] = "Platba k rezervaci {0}",
                ["StatusMessage"] = "Níže uvedenou částku se nám nepodařilo strhnout z vaší uložené karty. Zaplaťte ji prosím tlačítkem níže; odkaz platí 24 hodin. Dokud nebude zaplacena, nelze objednávat s platbou v hotovosti, kartou můžete objednávat dál.",
                ["StatusSectionLabel"] = "Dlužná částka za",
                ["OrderNumberLabel"] = "Rezervace č.",
                ["CleaningDateLabel"] = "Datum úklidu",
                ["AddressLabel"] = "Adresa",
                ["TotalLabel"] = "K úhradě",
                ["ButtonText"] = "Zaplatit",
                ["QuestionsText"] = "Pokud máte k této platbě dotazy, kontaktujte nás.",
                ["SupportText"] = "Potřebujete pomoc? Kontaktujte nás na",
                ["Closing"] = "S pozdravem",
                ["TeamName"] = "Tým Cleansia",
            },
            ["sk"] = new()
            {
                ["Subject"] = "Platba k rezervácii {0}",
                ["StatusMessage"] = "Uvedenú sumu sa nám nepodarilo stiahnuť z vašej uloženej karty. Zaplaťte ju, prosím, tlačidlom nižšie; odkaz platí 24 hodín. Kým nebude zaplatená, nie je možné objednávať s platbou v hotovosti, kartou môžete objednávať naďalej.",
                ["StatusSectionLabel"] = "Dlžná suma za",
                ["OrderNumberLabel"] = "Rezervácia č.",
                ["CleaningDateLabel"] = "Dátum upratovania",
                ["AddressLabel"] = "Adresa",
                ["TotalLabel"] = "Na úhradu",
                ["ButtonText"] = "Zaplatiť",
                ["QuestionsText"] = "Ak máte k tejto platbe otázky, kontaktujte nás.",
                ["SupportText"] = "Potrebujete pomoc? Kontaktujte nás na",
                ["Closing"] = "S pozdravom",
                ["TeamName"] = "Tím Cleansia",
            },
            ["uk"] = new()
            {
                ["Subject"] = "Оплата за бронювання {0}",
                ["StatusMessage"] = "Нам не вдалося списати зазначену суму з вашої збереженої картки. Будь ласка, сплатіть її за допомогою кнопки нижче; посилання дійсне 24 години. Доки суму не сплачено, бронювання з оплатою готівкою недоступні, бронювати з оплатою карткою можна й далі.",
                ["StatusSectionLabel"] = "Сума до сплати за",
                ["OrderNumberLabel"] = "Бронювання №",
                ["CleaningDateLabel"] = "Дата прибирання",
                ["AddressLabel"] = "Адреса",
                ["TotalLabel"] = "До сплати",
                ["ButtonText"] = "Сплатити",
                ["QuestionsText"] = "Якщо у вас є запитання щодо цієї оплати, зв’яжіться з нами.",
                ["SupportText"] = "Потрібна допомога? Напишіть нам:",
                ["Closing"] = "З повагою",
                ["TeamName"] = "Команда Cleansia",
            },
            ["ru"] = new()
            {
                ["Subject"] = "Оплата по бронированию {0}",
                ["StatusMessage"] = "Нам не удалось списать указанную сумму с вашей сохранённой карты. Пожалуйста, оплатите её с помощью кнопки ниже; ссылка действительна 24 часа. Пока сумма не оплачена, бронирования с оплатой наличными недоступны, бронировать с оплатой картой можно и дальше.",
                ["StatusSectionLabel"] = "Сумма к оплате за",
                ["OrderNumberLabel"] = "Бронирование №",
                ["CleaningDateLabel"] = "Дата уборки",
                ["AddressLabel"] = "Адрес",
                ["TotalLabel"] = "К оплате",
                ["ButtonText"] = "Оплатить",
                ["QuestionsText"] = "Если у вас есть вопросы об этой оплате, свяжитесь с нами.",
                ["SupportText"] = "Нужна помощь? Напишите нам:",
                ["Closing"] = "С уважением",
                ["TeamName"] = "Команда Cleansia",
            },
        };

    private const string BookedStatus = "booked";

    private async Task<string> SendStatusEmailAsync(
        string email,
        Order order,
        string newStatus,
        string languageCode,
        CancellationToken ct,
        decimal? refundedAmount,
        string? guestAccessToken,
        int? freeCancellationHours)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderStatusUpdate, languageCode, ct);

        var isCancelled = newStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase);
        var isBooked = newStatus.Equals(BookedStatus, StringComparison.OrdinalIgnoreCase);
        if (isCancelled || isBooked)
        {
            var defaults = StatusEmailDefaults.GetValueOrDefault(languageCode)
                ?? StatusEmailDefaults[Constants.Language.English];
            foreach (var (key, value) in defaults)
            {
                translations.TryAdd(key, value);
            }
        }

        var orderStatusLink = BuildOrderStatusLink(order, email, guestAccessToken);
        var address = order.CustomerAddress != null
            ? $"{order.CustomerAddress.Street}, {order.CustomerAddress.City}"
            : "";
        // An unloaded Currency navigation is a loader omission, not a CZK order: no unit rather than a guessed one. → /architecture/platform-expandability#_5-where-czk-kc-is-hardcoded-vs-configurable
        var currencySymbol = order.Currency?.Symbol ?? string.Empty;

        var (statusTitle, statusMessage, statusClass) = newStatus.ToLowerInvariant() switch
        {
            "confirmed" => (
                translations.GetValueOrDefault("StatusTitle_Confirmed", "Order Confirmed"),
                translations.GetValueOrDefault("StatusMessage_Confirmed", "Your cleaning order has been confirmed and is scheduled."),
                "confirmed"),
            "assigned" => (
                translations.GetValueOrDefault("StatusTitle_Assigned", "Cleaner Assigned"),
                translations.GetValueOrDefault("StatusMessage_Assigned", "A professional cleaner has been assigned to your order."),
                "assigned"),
            "inprogress" or "started" => (
                translations.GetValueOrDefault("StatusTitle_Started", "Cleaning Started"),
                translations.GetValueOrDefault("StatusMessage_Started", "Your cleaning session has started."),
                "started"),
            "completed" => (
                translations.GetValueOrDefault("StatusTitle_Completed", "Cleaning Complete"),
                translations.GetValueOrDefault("StatusMessage_Completed", "Your cleaning has been completed successfully."),
                "completed"),
            "cancelled" => (
                translations.GetValueOrDefault("StatusTitle_Cancelled", "Order Cancelled"),
                translations.GetValueOrDefault("StatusMessage_Cancelled", "Your order has been cancelled."),
                "cancelled"),
            BookedStatus => (
                translations.GetValueOrDefault("StatusTitle_Booked", "Booking confirmed"),
                translations.GetValueOrDefault("StatusMessage_Booked", "Your cleaning is booked."),
                "confirmed"),
            _ => (
                translations.GetValueOrDefault("StatusTitle_Default", "Order Update"),
                translations.GetValueOrDefault("StatusMessage_Default", "Your order status has been updated."),
                "confirmed")
        };

        var isLocalised = isCancelled || isBooked;
        var orderLabel = isLocalised ? translations.GetValueOrDefault("OrderNumberLabel", "Order") : "Order";
        var subject = translations.GetValueOrDefault("Subject", $"{orderLabel} {order.DisplayOrderNumber} — {statusTitle}");

        var refundLine = refundedAmount is > 0m
            ? $"{translations.GetValueOrDefault("RefundMessage", "Refund issued:")} {Money(refundedAmount.Value, currencySymbol, languageCode)}."
            : null;
        string?[] bookedLines = isBooked
            ?
            [
                string.Format(
                    CultureFor(languageCode),
                    translations.GetValueOrDefault("CashDueMessage", "Please pay {0} in cash to the cleaner on the day."),
                    Money(order.TotalPrice - order.CreditAppliedAmount, currencySymbol, languageCode)),
                freeCancellationHours is { } hours
                    ? string.Format(
                        CultureFor(languageCode),
                        translations.GetValueOrDefault("FreeCancellationMessage", "You can cancel free of charge up to {0} h before the cleaning; a later cancellation may be charged a fee."),
                        hours)
                    : null,
            ]
            : [];
        string? platformReason = null;
        if (statusClass == "cancelled" && order.CancelledBy is CancelledBy.Admin or CancelledBy.System)
        {
            // The reason code, never an admin's free text: that is written for the company, not the guest.
            platformReason = translations.GetValueOrDefault(order.CancellationReason switch
            {
                OrderCancellationReasons.NoCleanerAvailable => "CancelledReason_NoCleaner",
                OrderCancellationReasons.PaymentNotCompleted => "CancelledReason_PaymentNotCompleted",
                _ => "CancelledReason_ByUs",
            });
            // "Being processed" only for the card charge a platform cancellation sends back — the one state in
            // which PlatformOrderCancellation and CancelUnfilledOrders both attempt the refund. Anything else
            // (already refunded, disputed, nothing to refund, no charge to refund against) promises nothing.
            var refundAttempted = order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && order.TotalPrice > 0m
                && order.HasRefundableChargeSurface;
            refundLine ??= order.TookNoPayment ? translations.GetValueOrDefault("NothingChargedMessage")
                : refundAttempted ? translations.GetValueOrDefault("RefundPendingMessage")
                : null;
        }

        var cleaningTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc), await MarketZoneAsync(order, ct));

        var values = BuildTemplateValues(translations, new
        {
            Subject = subject,
            StatusMessage = string.Join(" ",
                new[] { statusMessage, platformReason, refundLine }.Concat(bookedLines)
                    .Where(line => !string.IsNullOrWhiteSpace(line))),
            StatusSectionLabel = translations.GetValueOrDefault("StatusSectionLabel", "Current Status"),
            StatusClass = statusClass,
            StatusLabel = isLocalised ? statusTitle : newStatus.ToUpperInvariant(),
            OrderNumberLabel = translations.GetValueOrDefault("OrderNumberLabel", "Order #"),
            OrderNumber = order.DisplayOrderNumber,
            CleaningDateLabel = translations.GetValueOrDefault("CleaningDateLabel", "Cleaning Date"),
            CleaningDate = cleaningTime.ToString("g", CultureFor(languageCode)),
            AddressLabel = translations.GetValueOrDefault("AddressLabel", "Address"),
            Address = address,
            TotalLabel = translations.GetValueOrDefault("TotalLabel", "Total"),
            Total = Money(order.TotalPrice, currencySymbol, languageCode),
            OrderStatusLink = orderStatusLink,
            ButtonText = translations.GetValueOrDefault("ButtonText", "View Order Details"),
            QuestionsText = translations.GetValueOrDefault("QuestionsText", "If you have any questions about your order, don't hesitate to reach out."),
            SupportText = translations.GetValueOrDefault("SupportText", "Need help? Contact us at"),
            SupportEmail = translations.GetValueOrDefault("SupportEmail", "info@cleansia.cz"),
            Closing = translations.GetValueOrDefault("Closing", "Best regards,"),
            TeamName = translations.GetValueOrDefault("TeamName", "The Cleansia Team"),
            FooterText = translations.GetValueOrDefault("FooterText", isLocalised
                ? $"© {DateTime.UtcNow.Year} Cleansia"
                : $"© {DateTime.UtcNow.Year} Cleansia s.r.o. All rights reserved.")
        }, languageCode);

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.OrderStatusUpdate), values),
            subject,
            $"Order status update ({newStatus}) to {email}",
            ct);
    }


    public async Task<string> SendPromoCodeEmailAsync(
        string email,
        string promoCode,
        string discountLabel,
        DateTime? expiresOn,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.PromoCode, languageCode, ct);

        // Owner, 2026-09-02: the promo e-mail arrived "corrupted" — a bare "!" where
        // the greeting belongs and a button with no words on it.
        //
        // Nothing was wrong with the template or the send. EmailTemplateTranslation
        // is admin-managed data with no code seed, and no row has ever been entered
        // for PromoCode, so every key resolved to nothing. The renderer strips an
        // unmatched {{Key}} rather than mailing braces, which turned "{{Greeting}}!"
        // into "!" and emptied the CTA. Only Subject and ExpiryNotice survived,
        // because only those two had a fallback here.
        //
        // So the copy the template needs now has one, in every locale the platform
        // speaks. This is the BASE layer: an admin row still wins over it, and the
        // per-send data below still wins over both.
        var defaults = PromoDefaultsFor(languageCode);

        var subject = translations.GetValueOrDefault("Subject", defaults["Subject"]);

        // Expiry is optional: an issued code with no end date renders the sentence
        // empty rather than the words "null" or a fabricated date.
        var expiryNotice = expiresOn is null
            ? string.Empty
            : string.Format(
                translations.GetValueOrDefault("ExpiryNotice", defaults["ExpiryNotice"]),
                expiresOn.Value.ToString("d. M. yyyy"));

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, value) in defaults)
        {
            values[key] = value;
        }

        foreach (var (key, value) in translations)
        {
            values[key] = value;
        }

        // The seeded DiscountText is the phrase ("Discount on your first order")
        // and carries no figure, because the figure is not translatable — it is
        // the value on the row. The pill needs both, or the e-mail offers a
        // discount without ever saying how much.
        var discountPhrase = translations.GetValueOrDefault("DiscountText", defaults["DiscountText"]);

        values["lang"] = languageCode;
        values["PromoCode"] = promoCode;
        values["DiscountText"] = string.IsNullOrWhiteSpace(discountPhrase)
            ? discountLabel
            : $"{discountLabel} · {discountPhrase}";
        values["ExpiryNotice"] = expiryNotice;
        values["OrderLink"] = sendGridConfig.ClientDomainUrl;
        values["SupportEmail"] = sendGridConfig.AddressFrom;
        values["Subject"] = subject;

        var html = templateRenderer.Render("promo-code.html", values);

        return await SendRenderedAsync(
            email,
            html,
            subject,
            $"Promo code email to {email}",
            ct);
    }

    /// <summary>
    /// Renders one of the repository's templates and sends it, optionally with a PDF attached.
    /// </summary>
    /// <remarks>
    /// This replaced <c>SendTemplatedAsync</c> and <c>SendTemplatedWithAttachmentAsync</c>, which
    /// differed only in whether they called <c>AddAttachment</c> — the attachment is a nullable
    /// parameter here rather than a second copy of the whole send.
    ///
    /// Same transport, same pooled client, same resilience handler and the same failure
    /// classification as before; the only change is that the HTML comes from
    /// <c>email-templates/</c> instead of from a template id resolved in someone's SendGrid account.
    /// </remarks>
    private async Task<string> SendRenderedAsync(
        string email,
        string htmlContent,
        string subject,
        string logContext,
        CancellationToken ct,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var client = new SendGridClient(httpClientFactory.CreateClient(SendGridHttpClientName), sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress(sendGridConfig.AddressFrom, "Cleansia"),
            new EmailAddress(email),
            subject,
            plainTextContent: null,
            htmlContent: htmlContent);

        if (attachmentBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentFileName))
        {
            msg.AddAttachment(attachmentFileName, Convert.ToBase64String(attachmentBytes), "application/pdf");
            logger.LogInformation(
                "Adding PDF attachment: {FileName} ({Size} bytes)", attachmentFileName, attachmentBytes.Length);
        }

        logger.LogInformation("Sending {Context}", logContext);

        var response = await client.SendEmailAsync(msg, ct);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowClassifiedAsync(response, email, ct);
        }

        var messageId = response.Headers.TryGetValues("X-Message-Id", out var ids)
            ? ids.FirstOrDefault()
            : "n/a";

        logger.LogInformation("Email sent successfully ({MessageId})", messageId);
        return messageId ?? "n/a";
    }

    // The standard resilience handler has already exhausted any Transient retry by this point, so a
    // non-success response here is terminal: classify, meter for owner alerting, log once, then throw
    // the existing EmailDeliveryException to keep the caller contract unchanged.
    private async Task ThrowClassifiedAsync(Response response, string email, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var failureClass = IntegrationFailureClassifier.FromSendGridResponse(response);
        var body = await response.Body.ReadAsStringAsync(ct);

        IntegrationFailureMetrics.Record(SendGridHttpClientName, failureClass);

        // S6: the response body can echo recipient addresses — keep it out of Error; the class +
        // status carry the alerting signal, and the body stays at Debug for local diagnosis only.
        logger.LogError(
            "SendGrid send failed: {FailureClass} ({StatusCode})",
            failureClass, status);
        logger.LogDebug("SendGrid failure response body: {Body}", body);

        throw new EmailDeliveryException($"Could not deliver email to {email} ({response.StatusCode}).");
    }

    /// <summary>
    /// The value set a template is rendered with, in three layers.
    /// </summary>
    /// <remarks>
    /// <para><b>Copy</b> from <c>EmailTemplateTranslation</c> is the base — it is the per-locale
    /// text and it is the layer a translator owns.</para>
    ///
    /// <para><b>Runtime data</b> overrides it: a name, an order number, an amount. These are facts
    /// about this one send and can never be translated.</para>
    ///
    /// <para><b>Two computed defaults</b> fill in only where nothing above supplied them:
    /// <c>lang</c>, which no translation row carries because it is the row's own language; and
    /// <c>SupportEmail</c>, which falls back to the configured from-address so a missing row cannot
    /// leave a customer with no way to reply. A translation row still wins if it exists.</para>
    ///
    /// Values are stringified here rather than at the call sites, because the renderer substitutes
    /// text and a boxed <c>decimal</c> would format by the ambient culture at an unpredictable point.
    /// </remarks>
    private Dictionary<string, string?> BuildTemplateValues<T>(
        Dictionary<string, string> translations,
        T runtimeData,
        string languageCode)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, value) in translations)
        {
            values[key] = value;
        }

        foreach (var property in typeof(T).GetProperties())
        {
            var value = property.GetValue(runtimeData);
            if (value != null)
            {
                values[property.Name] = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        values["lang"] = languageCode;

        if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("SupportEmail")))
        {
            values["SupportEmail"] = sendGridConfig.AddressFrom;
        }

        return values;
    }

    /// <summary>
    /// The repository file each e-mail renders from.
    /// </summary>
    /// <remarks>
    /// This replaces six SendGrid template ids. The ids were configuration, so a template could be
    /// edited in someone's SendGrid account and nothing in the repository would show it; these are
    /// file names resolved against embedded resources, so the copy that is sent is the copy that was
    /// reviewed. An unknown type throws rather than falling back — a silent default here would mail
    /// the wrong template.
    /// </remarks>
    /// <summary>
    /// Default promo-code copy, per locale. Used only where
    /// <see cref="EmailTemplateTranslation"/> has no row for a key.
    /// </summary>
    /// <remarks>
    /// The translations table is admin-managed and has never carried a PromoCode
    /// row, so before this the template's greeting, intro, instructions, CTA label,
    /// sign-off and footer all rendered empty and the mail reached customers with a
    /// blank button. Copy lives here rather than in a migration because the table is
    /// editable data, not schema: seeding it with HasData would mean regenerating
    /// `Initial` and dropping the DEV database, and an admin edit would then be
    /// overwritten on the next reseed. As a fallback layer, an entered row always wins.
    ///
    /// Keys match the placeholders in `email-templates/promo-code.html`. `{0}` in
    /// ExpiryNotice is the formatted date.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> PromoDefaultsFor(string languageCode) =>
        PromoDefaults.TryGetValue(languageCode ?? string.Empty, out var copy)
            ? copy
            : PromoDefaults[Constants.Language.English];

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> PromoDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cs"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "Váš slevový kód Cleansia",
                ["Greeting"] = "Dobrý den",
                ["IntroText"] = "Máme pro vás slevový kód na úklid s Cleansia.",
                ["DiscountText"] = "Sleva na vaši objednávku",
                ["InstructionsText"] = "Kód zadejte v posledním kroku objednávky.",
                ["ButtonText"] = "Objednat úklid",
                ["ExpiryNotice"] = "Kód platí do {0}.",
                ["IgnoreText"] = "Pokud jste o kód nežádali, můžete tento e-mail ignorovat.",
                ["SupportText"] = "Potřebujete pomoc? Napište nám na",
                ["Closing"] = "S pozdravem,",
                ["TeamName"] = "tým Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všechna práva vyhrazena.",
            },
            ["sk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "Váš zľavový kód Cleansia",
                ["Greeting"] = "Dobrý deň",
                ["IntroText"] = "Máme pre vás zľavový kód na upratovanie s Cleansia.",
                ["DiscountText"] = "Zľava na vašu objednávku",
                ["InstructionsText"] = "Kód zadajte v poslednom kroku objednávky.",
                ["ButtonText"] = "Objednať upratovanie",
                ["ExpiryNotice"] = "Kód platí do {0}.",
                ["IgnoreText"] = "Ak ste o kód nežiadali, tento e-mail môžete ignorovať.",
                ["SupportText"] = "Potrebujete pomoc? Napíšte nám na",
                ["Closing"] = "S pozdravom,",
                ["TeamName"] = "tím Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všetky práva vyhradené.",
            },
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "Your Cleansia discount code",
                ["Greeting"] = "Hello",
                ["IntroText"] = "Here is your discount code for a clean with Cleansia.",
                ["DiscountText"] = "Discount on your order",
                ["InstructionsText"] = "Enter the code at the last step of your booking.",
                ["ButtonText"] = "Book a clean",
                ["ExpiryNotice"] = "The code is valid until {0}.",
                ["IgnoreText"] = "If you did not ask for this code, you can ignore this e-mail.",
                ["SupportText"] = "Need a hand? Write to us at",
                ["Closing"] = "Kind regards,",
                ["TeamName"] = "the Cleansia team",
                ["FooterText"] = "© Cleansia s.r.o. All rights reserved.",
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "Ваш промокод Cleansia",
                ["Greeting"] = "Здравствуйте",
                ["IntroText"] = "Мы приготовили для вас промокод на уборку с Cleansia.",
                ["DiscountText"] = "Скидка на ваш заказ",
                ["InstructionsText"] = "Введите код на последнем шаге оформления заказа.",
                ["ButtonText"] = "Заказать уборку",
                ["ExpiryNotice"] = "Код действует до {0}.",
                ["IgnoreText"] = "Если вы не запрашивали этот код, просто проигнорируйте письмо.",
                ["SupportText"] = "Нужна помощь? Напишите нам на",
                ["Closing"] = "С уважением,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Все права защищены.",
            },
            ["uk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "Ваш промокод Cleansia",
                ["Greeting"] = "Доброго дня",
                ["IntroText"] = "Ми підготували для вас промокод на прибирання з Cleansia.",
                ["DiscountText"] = "Знижка на ваше замовлення",
                ["InstructionsText"] = "Введіть код на останньому кроці оформлення замовлення.",
                ["ButtonText"] = "Замовити прибирання",
                ["ExpiryNotice"] = "Код діє до {0}.",
                ["IgnoreText"] = "Якщо ви не запитували цей код, просто проігноруйте цей лист.",
                ["SupportText"] = "Потрібна допомога? Напишіть нам на",
                ["Closing"] = "З повагою,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Усі права захищено.",
            },
        };

    public Task<string> SendCompanyWindDownCustomerNoticeAsync(
        string email,
        string userName,
        IReadOnlyList<string> companyNames,
        DateOnly windDownFrom,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default) =>
        SendCompanyWindDownNoticeAsync(
            EmailType.CompanyWindDownCustomer, CompanyWindDownCustomerDefaults, email, userName, companyNames, windDownFrom, languageCode, ct);

    public Task<string> SendCompanyWindDownCleanerNoticeAsync(
        string email,
        string userName,
        IReadOnlyList<string> companyNames,
        DateOnly windDownFrom,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default) =>
        SendCompanyWindDownNoticeAsync(
            EmailType.CompanyWindDownCleaner, CompanyWindDownCleanerDefaults, email, userName, companyNames, windDownFrom, languageCode, ct);

    // The same three layers as the promo e-mail: in-code copy per locale, an admin translation row
    // over it, and the per-send facts over both. The company is named as its receipts name it —
    // one legal name per market — never as the registry label.
    private async Task<string> SendCompanyWindDownNoticeAsync(
        EmailType emailType,
        Dictionary<string, IReadOnlyDictionary<string, string>> defaultsByLocale,
        string email,
        string userName,
        IReadOnlyList<string> companyNames,
        DateOnly windDownFrom,
        string languageCode,
        CancellationToken ct)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(emailType, languageCode, ct);
        var defaults = defaultsByLocale.TryGetValue(languageCode ?? string.Empty, out var copy)
            ? copy
            : defaultsByLocale[Constants.Language.English];

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in defaults)
        {
            values[key] = value;
        }

        foreach (var (key, value) in translations)
        {
            values[key] = value;
        }

        var company = companyNames.Count == 0 ? "Cleansia" : string.Join(", ", companyNames);
        var date = windDownFrom.ToString("d. M. yyyy");
        var subject = string.Format(values["Subject"]!, company, date);

        values["lang"] = languageCode ?? Constants.Language.English;
        values["Subject"] = subject;
        values["UserName"] = userName;
        values["IntroText"] = string.Format(values["IntroText"]!, company, date);
        values["WindDownDate"] = date;
        values["AppLink"] = sendGridConfig.ClientDomainUrl;
        values["SupportEmail"] = sendGridConfig.AddressFrom;

        var html = templateRenderer.Render(TemplateFileFor(emailType), values);

        return await SendRenderedAsync(
            email,
            html,
            subject,
            $"Company wind-down notice ({emailType}) to {email}",
            ct);
    }

    /// <summary>Default customer wind-down copy per locale; <c>{0}</c> is the company name(s), <c>{1}</c> the date.</summary>
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> CompanyWindDownCustomerDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} is closing — what it means for you",
                ["Greeting"] = "Hello",
                ["IntroText"] = "{0} is winding down its operations. The last day on which a cleaning can take place is:",
                ["BookingsText"] = "Every booking on or after this date is cancelled and refunded in full. You do not need to do anything.",
                ["PlusText"] = "If you have Cleansia Plus, it ends at the end of your current billing period and will not renew.",
                ["CreditText"] = "Any unspent credit on your account expires when the company closes.",
                ["AccountText"] = "Your account and your order history stay exactly as they are.",
                ["DataText"] = "You can export or delete your data at any time from your account settings.",
                ["ButtonText"] = "Open my account",
                ["ThanksText"] = "Thank you for cleaning with us.",
                ["SupportText"] = "Questions? Write to us at",
                ["Closing"] = "Kind regards,",
                ["TeamName"] = "the Cleansia team",
                ["FooterText"] = "© Cleansia s.r.o. All rights reserved.",
            },
            ["cs"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} končí — co to pro vás znamená",
                ["Greeting"] = "Dobrý den",
                ["IntroText"] = "{0} ukončuje svou činnost. Poslední den, kdy může úklid proběhnout, je:",
                ["BookingsText"] = "Každá objednávka na tento den nebo později je zrušena a v plné výši vrácena. Nemusíte nic dělat.",
                ["PlusText"] = "Pokud máte Cleansia Plus, skončí na konci aktuálního zúčtovacího období a neobnoví se.",
                ["CreditText"] = "Nevyčerpaný kredit na vašem účtu propadne v den, kdy společnost ukončí činnost.",
                ["AccountText"] = "Váš účet i historie objednávek zůstávají beze změny.",
                ["DataText"] = "Svá data si můžete kdykoli exportovat nebo smazat v nastavení účtu.",
                ["ButtonText"] = "Otevřít můj účet",
                ["ThanksText"] = "Děkujeme, že jste uklízeli s námi.",
                ["SupportText"] = "Máte otázky? Napište nám na",
                ["Closing"] = "S pozdravem,",
                ["TeamName"] = "tým Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všechna práva vyhrazena.",
            },
            ["sk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} končí — čo to pre vás znamená",
                ["Greeting"] = "Dobrý deň",
                ["IntroText"] = "{0} ukončuje svoju činnosť. Posledný deň, keď môže upratovanie prebehnúť, je:",
                ["BookingsText"] = "Každá objednávka na tento deň alebo neskôr je zrušená a v plnej výške vrátená. Nemusíte nič robiť.",
                ["PlusText"] = "Ak máte Cleansia Plus, skončí na konci aktuálneho zúčtovacieho obdobia a neobnoví sa.",
                ["CreditText"] = "Nevyčerpaný kredit na vašom účte prepadne v deň, keď spoločnosť ukončí činnosť.",
                ["AccountText"] = "Váš účet aj história objednávok zostávajú bez zmeny.",
                ["DataText"] = "Svoje údaje si môžete kedykoľvek exportovať alebo vymazať v nastaveniach účtu.",
                ["ButtonText"] = "Otvoriť môj účet",
                ["ThanksText"] = "Ďakujeme, že ste upratovali s nami.",
                ["SupportText"] = "Máte otázky? Napíšte nám na",
                ["Closing"] = "S pozdravom,",
                ["TeamName"] = "tím Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všetky práva vyhradené.",
            },
            ["uk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} припиняє роботу — що це означає для вас",
                ["Greeting"] = "Доброго дня",
                ["IntroText"] = "{0} припиняє свою діяльність. Останній день, коли може відбутися прибирання:",
                ["BookingsText"] = "Кожне замовлення на цей день або пізніше скасовано, а кошти повернуто в повному обсязі. Вам нічого не потрібно робити.",
                ["PlusText"] = "Якщо у вас є Cleansia Plus, підписка завершиться наприкінці поточного розрахункового періоду й не поновиться.",
                ["CreditText"] = "Невикористаний кредит на вашому рахунку згорить у день закриття компанії.",
                ["AccountText"] = "Ваш обліковий запис та історія замовлень залишаються без змін.",
                ["DataText"] = "Ви можете будь-коли експортувати або видалити свої дані в налаштуваннях облікового запису.",
                ["ButtonText"] = "Відкрити мій обліковий запис",
                ["ThanksText"] = "Дякуємо, що прибирали з нами.",
                ["SupportText"] = "Є запитання? Напишіть нам на",
                ["Closing"] = "З повагою,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Усі права захищено.",
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} прекращает работу — что это значит для вас",
                ["Greeting"] = "Здравствуйте",
                ["IntroText"] = "{0} прекращает свою деятельность. Последний день, когда может состояться уборка:",
                ["BookingsText"] = "Каждый заказ на этот день или позже отменён, а деньги возвращены в полном объёме. Вам ничего не нужно делать.",
                ["PlusText"] = "Если у вас есть Cleansia Plus, подписка закончится в конце текущего расчётного периода и не продлится.",
                ["CreditText"] = "Неиспользованный кредит на вашем счёте сгорит в день закрытия компании.",
                ["AccountText"] = "Ваш аккаунт и история заказов остаются без изменений.",
                ["DataText"] = "Вы можете в любой момент экспортировать или удалить свои данные в настройках аккаунта.",
                ["ButtonText"] = "Открыть мой аккаунт",
                ["ThanksText"] = "Спасибо, что убирались с нами.",
                ["SupportText"] = "Есть вопросы? Напишите нам на",
                ["Closing"] = "С уважением,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Все права защищены.",
            },
        };

    /// <summary>Default cleaner wind-down copy per locale; <c>{0}</c> is the company name(s), <c>{1}</c> the date.</summary>
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> CompanyWindDownCleanerDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} is closing — your last day of work",
                ["Greeting"] = "Hello",
                ["IntroText"] = "{0} is winding down its operations. Your last day of work is:",
                ["LastDayText"] = "Jobs scheduled on or after this date are cancelled; jobs before it go ahead as planned.",
                ["PayText"] = "Your last pay period is closed and invoiced as usual once the last job is done, and paid the usual way.",
                ["SignInText"] = "Once the company closes, signing in to the partner app will no longer be possible.",
                ["DataText"] = "To export or delete your data, sign in to the customer app with the same account.",
                ["ButtonText"] = "Open the customer app",
                ["ThanksText"] = "Thank you for the work you have done with us.",
                ["SupportText"] = "Questions? Write to us at",
                ["Closing"] = "Kind regards,",
                ["TeamName"] = "the Cleansia team",
                ["FooterText"] = "© Cleansia s.r.o. All rights reserved.",
            },
            ["cs"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} končí — váš poslední pracovní den",
                ["Greeting"] = "Dobrý den",
                ["IntroText"] = "{0} ukončuje svou činnost. Váš poslední pracovní den je:",
                ["LastDayText"] = "Zakázky naplánované na tento den nebo později jsou zrušeny; zakázky před ním proběhnou podle plánu.",
                ["PayText"] = "Vaše poslední výplatní období bude po dokončení poslední zakázky uzavřeno a vyfakturováno jako obvykle a vyplaceno obvyklým způsobem.",
                ["SignInText"] = "Po ukončení činnosti společnosti už nebude možné se přihlásit do partnerské aplikace.",
                ["DataText"] = "Pro export nebo smazání svých dat se přihlaste stejným účtem do zákaznické aplikace.",
                ["ButtonText"] = "Otevřít zákaznickou aplikaci",
                ["ThanksText"] = "Děkujeme za práci, kterou jste s námi odvedli.",
                ["SupportText"] = "Máte otázky? Napište nám na",
                ["Closing"] = "S pozdravem,",
                ["TeamName"] = "tým Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všechna práva vyhrazena.",
            },
            ["sk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} končí — váš posledný pracovný deň",
                ["Greeting"] = "Dobrý deň",
                ["IntroText"] = "{0} ukončuje svoju činnosť. Váš posledný pracovný deň je:",
                ["LastDayText"] = "Zákazky naplánované na tento deň alebo neskôr sú zrušené; zákazky pred ním prebehnú podľa plánu.",
                ["PayText"] = "Vaše posledné výplatné obdobie bude po dokončení poslednej zákazky uzavreté a vyfakturované ako obvykle a vyplatené obvyklým spôsobom.",
                ["SignInText"] = "Po ukončení činnosti spoločnosti už nebude možné prihlásiť sa do partnerskej aplikácie.",
                ["DataText"] = "Na export alebo vymazanie svojich údajov sa prihláste rovnakým účtom do zákazníckej aplikácie.",
                ["ButtonText"] = "Otvoriť zákaznícku aplikáciu",
                ["ThanksText"] = "Ďakujeme za prácu, ktorú ste s nami odviedli.",
                ["SupportText"] = "Máte otázky? Napíšte nám na",
                ["Closing"] = "S pozdravom,",
                ["TeamName"] = "tím Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Všetky práva vyhradené.",
            },
            ["uk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} припиняє роботу — ваш останній робочий день",
                ["Greeting"] = "Доброго дня",
                ["IntroText"] = "{0} припиняє свою діяльність. Ваш останній робочий день:",
                ["LastDayText"] = "Замовлення, заплановані на цей день або пізніше, скасовано; замовлення до нього відбудуться за планом.",
                ["PayText"] = "Ваш останній розрахунковий період буде закрито та виставлено рахунок як зазвичай після завершення останнього замовлення, а виплату здійснено звичайним способом.",
                ["SignInText"] = "Після закриття компанії вхід до партнерського застосунку буде неможливим.",
                ["DataText"] = "Щоб експортувати або видалити свої дані, увійдіть тим самим обліковим записом у застосунок для клієнтів.",
                ["ButtonText"] = "Відкрити застосунок для клієнтів",
                ["ThanksText"] = "Дякуємо за роботу, яку ви виконали разом з нами.",
                ["SupportText"] = "Є запитання? Напишіть нам на",
                ["Closing"] = "З повагою,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Усі права захищено.",
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Subject"] = "{0} прекращает работу — ваш последний рабочий день",
                ["Greeting"] = "Здравствуйте",
                ["IntroText"] = "{0} прекращает свою деятельность. Ваш последний рабочий день:",
                ["LastDayText"] = "Заказы, запланированные на этот день или позже, отменены; заказы до него состоятся по плану.",
                ["PayText"] = "Ваш последний расчётный период будет закрыт и выставлен к оплате как обычно после завершения последнего заказа, а выплата произведена обычным способом.",
                ["SignInText"] = "После закрытия компании вход в партнёрское приложение станет невозможным.",
                ["DataText"] = "Чтобы экспортировать или удалить свои данные, войдите тем же аккаунтом в приложение для клиентов.",
                ["ButtonText"] = "Открыть приложение для клиентов",
                ["ThanksText"] = "Спасибо за работу, которую вы проделали вместе с нами.",
                ["SupportText"] = "Есть вопросы? Напишите нам на",
                ["Closing"] = "С уважением,",
                ["TeamName"] = "команда Cleansia",
                ["FooterText"] = "© Cleansia s.r.o. Все права защищены.",
            },
        };

    private static string TemplateFileFor(EmailType emailType) => emailType switch
    {
        EmailType.ConfirmationEmail => "email-confirmation.html",
        EmailType.ResetPassword => "password-reset.html",
        EmailType.OrderReceipt => "order-receipt.html",
        EmailType.OrderStatusUpdate => "order-status-update.html",
        EmailType.PeriodClosed => "close-period-notification.html",
        EmailType.PeriodEndReminder => "closure-period-reminder.html",
        EmailType.PromoCode => "promo-code.html",
        EmailType.CompanyWindDownCustomer => "company-wind-down-customer.html",
        EmailType.CompanyWindDownCleaner => "company-wind-down-cleaner.html",
        EmailType.AdminNotification => "admin-notification.html",
        _ => throw new InvalidOperationException($"No e-mail template is mapped for {emailType}."),
    };
}
