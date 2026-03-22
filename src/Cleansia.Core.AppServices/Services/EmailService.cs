using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Cleansia.Core.AppServices.Services;

public sealed class EmailService : IEmailService
{
    private readonly ISendGridConfig sendGridConfig;
    private readonly ILogger<EmailService> logger;
    private readonly IAsyncPolicy<Response> policy;
    private readonly IEmailTemplateTranslationRepository emailTemplateTranslationRepository;

    public EmailService(
        ISendGridConfig cfg,
        ILogger<EmailService> log,
        IEmailTemplateTranslationRepository emailTemplateTranslationRepository)
    {
        sendGridConfig = cfg;
        logger = log;
        this.emailTemplateTranslationRepository = emailTemplateTranslationRepository;

        policy = Policy
            .HandleResult<Response>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                i => TimeSpan.FromMilliseconds(i * 300),
                (outcome, delay, attempt, _) =>
                    logger.LogWarning(
                        "SendGrid attempt {Attempt} failed ({Status}). Retrying in {Delay} ms.",
                        attempt,
                        outcome.Result?.StatusCode,
                        delay.TotalMilliseconds));
    }

    public async Task<string> SendResetPasswordEmailAsync(
        string email,
        string fullUserName,
        string code,
        string languageCode = "en",
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
        string languageCode = "en",
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderReceipt, languageCode, ct);

        var orderStatusLink = $"{sendGridConfig.ClientDomainUrl}{sendGridConfig.OrderStatusUrl}?orderId={order.Id}";

        var mergeData = MergeTranslationsWithData(translations, new
        {
            CustomerName = order.CustomerName,
            OrderNumber = order.DisplayOrderNumber,
            OrderDate = order.CreatedOn.ToString("d"),
            TotalAmount = order.TotalPrice.ToString("C"),
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
        string languageCode = "en",
        CancellationToken ct = default)
    {
        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.OrderReceipt, languageCode, ct);

        var orderStatusLink = $"{sendGridConfig.ClientDomainUrl}{sendGridConfig.OrderStatusUrl}?orderId=test-order-id";

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
        string languageCode = "en",
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

        // If invoice PDF is provided, send with attachment; otherwise send without
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
        string languageCode = "en",
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

        var client = new SendGridClient(sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleTemplateEmail(
            new EmailAddress(sendGridConfig.AddressFrom, "Cleansia"),
            new EmailAddress(email),
            templateId,
            model);

        msg.Personalizations[0].Subject = subject;
        logger.LogInformation("Sending {Context}", logContext);

        var response = await policy.ExecuteAsync(
            async _ => await client.SendEmailAsync(msg, ct), ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            logger.LogError("SendGrid returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new EmailDeliveryException($"Could not deliver email to {email} ({response.StatusCode}).");
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

        var client = new SendGridClient(sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleTemplateEmail(
            new EmailAddress(sendGridConfig.AddressFrom, "Cleansia"),
            new EmailAddress(email),
            templateId,
            model);

        msg.Personalizations[0].Subject = subject;

        // Add PDF attachment if provided
        if (pdfBytes != null && pdfBytes.Length > 0)
        {
            var base64Content = Convert.ToBase64String(pdfBytes);
            msg.AddAttachment(fileName, base64Content, "application/pdf");
            logger.LogInformation("Adding PDF attachment: {FileName} ({Size} bytes)", fileName, pdfBytes.Length);
        }

        logger.LogInformation("Sending {Context}", logContext);

        var response = await policy.ExecuteAsync(
            async _ => await client.SendEmailAsync(msg, ct), ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            logger.LogError("SendGrid returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new EmailDeliveryException($"Could not deliver email to {email} ({response.StatusCode}).");
        }

        var messageId = response.Headers.TryGetValues("X-Message-Id", out var ids)
            ? ids.FirstOrDefault()
            : "n/a";

        logger.LogInformation("Email sent successfully ({MessageId})", messageId);
        return messageId!;
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
