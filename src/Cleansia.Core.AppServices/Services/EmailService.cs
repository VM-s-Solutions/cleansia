using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Orders;
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

    public EmailService(ISendGridConfig cfg, ILogger<EmailService> log)
    {
        sendGridConfig = cfg;
        logger = log;

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

    public Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendOrderReceiptEmailAsync(string email, Order order, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, EmailTranslation emailTranslation, CancellationToken ct = default)
    {
        var model = new
        {
            UserName = userName,
            VerificationCode = verificationCode,
            Title = emailTranslation.Title,
            Header = emailTranslation.Header,
            SubHeader = emailTranslation.SubHeader,
            GreetingWord = emailTranslation.GreetingWord,
            Instruction = emailTranslation.Instruction,
            CodeNote = emailTranslation.CodeNote,
            Footer = emailTranslation.Footer
        };

        return SendTemplatedAsync(email, sendGridConfig.EmailConfirmationTemplateId,
            model,
            emailTranslation.Subject,
            $"Confirmation email to {email}", ct);
    }

    private async Task<string> SendTemplatedAsync<T>(string email, string templateId, T model, string subject, string logContext, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var client = new SendGridClient(sendGridConfig.ApiKey);
        var msg = MailHelper.CreateSingleTemplateEmail(
            new EmailAddress(sendGridConfig.AddressFrom),
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
}