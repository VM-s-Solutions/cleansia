using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Infra.Common.Configuration.Interfaces;
using SendGrid;

namespace Cleansia.Infra.Clients.SendGrid;

public class SendGridClientFactory(
    ISendGridConfig sendGridConfig,
    IHttpClientFactory httpClientFactory) : ISendGridClientFactory
{
    public ISendGridClient CreateClient()
    {
        // ADR-0005 D1 — source the SDK's transport from the pooled, named IHttpClientFactory client
        // (standard resilience handler + OTel) instead of letting SendGridClient mint its own socket.
        var transport = httpClientFactory.CreateClient(SendGridExtensions.HttpClientName);
        return new SendGridClient(transport, sendGridConfig.ApiKey);
    }
}
