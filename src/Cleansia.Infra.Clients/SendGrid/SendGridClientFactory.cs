using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using SendGrid.Helpers.Mail;
using SendGrid;

namespace Cleansia.Infra.Clients.SendGrid;

public class SendGridClientFactory(
    ISendGridConfig sendGridConfig,
    IHttpClientFactory httpClientFactory) : ISendGridClientFactory
{
    public const string EmailNotSentError = "email.sending_failed";

    public ISendGridClient CreateClient()
    {
        // ADR-0005 D1 — source the SDK's transport from the pooled, named IHttpClientFactory client
        // (standard resilience handler + OTel) instead of letting SendGridClient mint its own socket.
        var transport = httpClientFactory.CreateClient(SendGridExtensions.HttpClientName);
        return new SendGridClient(transport, sendGridConfig.ApiKey);
    }

    public async Task<BusinessResult> SendTemplateEmailAsync(ISendGridClient client, string from, string to, string templateId, object data,
        CancellationToken cancellationToken = default)
    {
        var addressFrom = new EmailAddress(from);
        var addressTo = new EmailAddress(to);
        var templateEmail = MailHelper.CreateSingleTemplateEmail(addressFrom, addressTo, templateId, data);
        var response = await client.SendEmailAsync(templateEmail, cancellationToken);

        return response.IsSuccessStatusCode
            ? BusinessResult.Success()
            : BusinessResult.Failure(
                new Error(nameof(SendTemplateEmailAsync),
                    EmailNotSentError));
    }
}
