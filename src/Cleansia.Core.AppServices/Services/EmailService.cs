using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Cleansia.Core.AppServices.Services;

public sealed class EmailService : IEmailService
{
    // The named IHttpClientFactory client whose pooled, resilience-wrapped handler the SendGrid SDK's
    // transport is built on. Kept in sync with SendGridExtensions.HttpClientName.
    private const string SendGridHttpClientName = "SendGrid";

    private readonly ISendGridConfig sendGridConfig;
    private readonly ILogger<EmailService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IEmailTemplateTranslationRepository emailTemplateTranslationRepository;

    public EmailService(
        ISendGridConfig cfg,
        ILogger<EmailService> log,
        IHttpClientFactory httpClientFactory,
        IEmailTemplateTranslationRepository emailTemplateTranslationRepository)
    {
        sendGridConfig = cfg;
        logger = log;
        this.httpClientFactory = httpClientFactory;
        this.emailTemplateTranslationRepository = emailTemplateTranslationRepository;
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

        var mergeData = MergeTranslationsWithData(translations, new
        {
            UserName = fullUserName,
            VerificationCode = code,
            ResetPasswordLink = resetLink
        });

        var subject = translations.GetValueOrDefault("Subject", "Reset Your Password");

        return await SendTemplatedAsync(
            email,
            sendGridConfig.ResetPasswordTemplateId,
            mergeData,
            subject,
            $"Password reset email to {email}",
            ct);
    }

    public async Task<string> SendOrderReceiptEmailAsync(
        string email,
        Order order,
        byte[]? pdfBytes = null,
        string fileName = "receipt.pdf",
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderReceipt, languageCode, ct);

        var orderStatusLink = $"{sendGridConfig.ClientDomainUrl}/track-order?orderNumber={Uri.EscapeDataString(order.DisplayOrderNumber)}&email={Uri.EscapeDataString(email)}";

        var mergeData = MergeTranslationsWithData(translations, new
        {
            CustomerName = order.CustomerName,
            OrderNumber = order.DisplayOrderNumber,
            OrderDate = order.CreatedOn.ToString("d"),
            TotalAmount = $"{order.Currency?.Symbol ?? "Kč"}{order.TotalPrice:N2}",
            OrderStatusLink = orderStatusLink
        });

        var subject = translations.GetValueOrDefault("Subject", "Your Order Receipt");

        return await SendTemplatedWithAttachmentAsync(
            email,
            sendGridConfig.OrderReceiptTemplateId,
            mergeData,
            subject,
            pdfBytes,
            fileName,
            $"Order receipt email to {email}",
            ct);
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

        var orderStatusLink = $"{sendGridConfig.ClientDomainUrl}/track-order?orderNumber={Uri.EscapeDataString(orderNumber)}&email={Uri.EscapeDataString(email)}";

        var mergeData = MergeTranslationsWithData(translations, new
        {
            CustomerName = customerName,
            OrderNumber = orderNumber,
            OrderDate = orderDate,
            TotalAmount = totalAmount,
            OrderStatusLink = orderStatusLink
        });

        var subject = "[TEST] " + translations.GetValueOrDefault("Subject", "Your Order Receipt");

        return await SendTemplatedAsync(
            email,
            sendGridConfig.OrderReceiptTemplateId,
            mergeData,
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

        var mergeData = MergeTranslationsWithData(translations, new
        {
            UserName = userName,
            VerificationCode = verificationCode
        });

        var subject = translations.GetValueOrDefault("Subject", "Confirm Your Email");

        return await SendTemplatedAsync(
            email,
            sendGridConfig.EmailConfirmationTemplateId,
            mergeData,
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

        var mergeData = MergeTranslationsWithData(translations, new
        {
            EmployeeName = employeeName,
            PeriodLabel = periodLabel,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            ClosedAt = closedAt.ToString("yyyy-MM-dd HH:mm:ss UTC")
        });

        var subject = translations.GetValueOrDefault("Subject", "Pay Period Closed");

        if (invoicePdfBytes != null && !string.IsNullOrWhiteSpace(invoiceFileName))
        {
            return await SendTemplatedWithAttachmentAsync(
                email,
                sendGridConfig.PeriodClosedTemplateId,
                mergeData,
                subject,
                invoicePdfBytes,
                invoiceFileName,
                $"Period closed email with invoice to {email}",
                ct);
        }

        return await SendTemplatedAsync(
            email,
            sendGridConfig.PeriodClosedTemplateId,
            mergeData,
            subject,
            $"Period closed email to {email}",
            ct);
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

        var mergeData = MergeTranslationsWithData(translations, new
        {
            EmployeeName = employeeName,
            PeriodLabel = periodLabel,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            DaysRemaining = formattedDaysRemaining
        });

        var subject = translations.GetValueOrDefault("Subject", "Pay Period Ending Soon");

        return await SendTemplatedAsync(
            email,
            sendGridConfig.PeriodEndReminderTemplateId,
            mergeData,
            subject,
            $"Period end reminder email to {email}",
            ct);
    }

    public async Task<string> SendOrderStatusUpdateEmailAsync(
        string email,
        Order order,
        string newStatus,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderStatusUpdate, languageCode, ct);

        var orderStatusLink = $"{sendGridConfig.ClientDomainUrl}/track-order?orderNumber={Uri.EscapeDataString(order.DisplayOrderNumber)}&email={Uri.EscapeDataString(email)}";
        var address = order.CustomerAddress != null
            ? $"{order.CustomerAddress.Street}, {order.CustomerAddress.City}"
            : "";
        var currencySymbol = order.Currency?.Symbol ?? "Kč";

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
            _ => (
                translations.GetValueOrDefault("StatusTitle_Default", "Order Update"),
                translations.GetValueOrDefault("StatusMessage_Default", "Your order status has been updated."),
                "confirmed")
        };

        var subject = translations.GetValueOrDefault("Subject", $"Order {order.DisplayOrderNumber} — {statusTitle}");

        var mergeData = MergeTranslationsWithData(translations, new
        {
            Subject = subject,
            StatusMessage = statusMessage,
            StatusSectionLabel = translations.GetValueOrDefault("StatusSectionLabel", "Current Status"),
            StatusClass = statusClass,
            StatusLabel = newStatus.ToUpperInvariant(),
            OrderNumberLabel = translations.GetValueOrDefault("OrderNumberLabel", "Order #"),
            OrderNumber = order.DisplayOrderNumber,
            CleaningDateLabel = translations.GetValueOrDefault("CleaningDateLabel", "Cleaning Date"),
            CleaningDate = order.CleaningDateTime.ToString("dd.MM.yyyy HH:mm"),
            AddressLabel = translations.GetValueOrDefault("AddressLabel", "Address"),
            Address = address,
            TotalLabel = translations.GetValueOrDefault("TotalLabel", "Total"),
            Total = $"{currencySymbol}{order.TotalPrice:N2}",
            OrderStatusLink = orderStatusLink,
            ButtonText = translations.GetValueOrDefault("ButtonText", "View Order Details"),
            QuestionsText = translations.GetValueOrDefault("QuestionsText", "If you have any questions about your order, don't hesitate to reach out."),
            SupportText = translations.GetValueOrDefault("SupportText", "Need help? Contact us at"),
            SupportEmail = translations.GetValueOrDefault("SupportEmail", "info@cleansia.cz"),
            Closing = translations.GetValueOrDefault("Closing", "Best regards,"),
            TeamName = translations.GetValueOrDefault("TeamName", "The Cleansia Team"),
            FooterText = translations.GetValueOrDefault("FooterText", $"© {DateTime.UtcNow.Year} Cleansia s.r.o. All rights reserved.")
        });

        return await SendTemplatedAsync(
            email,
            sendGridConfig.OrderStatusUpdateTemplateId,
            mergeData,
            subject,
            $"Order status update ({newStatus}) to {email}",
            ct);
    }

    private async Task<string> SendTemplatedAsync<T>(
        string email,
        string templateId,
        T model,
        string subject,
        string logContext,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        // Hand the SDK the pooled, factory-managed HttpClient (its resilience handler retries Transient
        // 5xx/408/429 and does NOT retry 401/403/4xx) instead of newing its own socket per send.
        var client = new SendGridClient(httpClientFactory.CreateClient(SendGridHttpClientName), sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleTemplateEmail(
            new EmailAddress(sendGridConfig.AddressFrom, "Cleansia"),
            new EmailAddress(email),
            templateId,
            model);

        msg.Personalizations[0].Subject = subject;
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
        return messageId!;
    }

    private async Task<string> SendTemplatedWithAttachmentAsync<T>(
        string email,
        string templateId,
        T model,
        string subject,
        byte[]? pdfBytes,
        string fileName,
        string logContext,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var client = new SendGridClient(httpClientFactory.CreateClient(SendGridHttpClientName), sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleTemplateEmail(
            new EmailAddress(sendGridConfig.AddressFrom, "Cleansia"),
            new EmailAddress(email),
            templateId,
            model);

        msg.Personalizations[0].Subject = subject;

        if (pdfBytes != null && pdfBytes.Length > 0)
        {
            var base64Content = Convert.ToBase64String(pdfBytes);
            msg.AddAttachment(fileName, base64Content, "application/pdf");
            logger.LogInformation("Adding PDF attachment: {FileName} ({Size} bytes)", fileName, pdfBytes.Length);
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
        return messageId!;
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

    private static object MergeTranslationsWithData<T>(Dictionary<string, string> translations, T runtimeData)
    {
        var translationsDict = translations.ToDictionary(k => k.Key, v => (object)v.Value);
        var runtimeProps = typeof(T).GetProperties()
            .Where(p => p.GetValue(runtimeData) != null)
            .ToDictionary(p => p.Name, p => p.GetValue(runtimeData)!);

        return translationsDict.Concat(runtimeProps)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
