using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Clients.SendGrid;

public static class SendGridExtensions
{
    /// <summary>
    /// The named <see cref="System.Net.Http.IHttpClientFactory"/> client whose pooled
    /// <c>SocketsHttpHandler</c> the SendGrid SDK's transport is built on. ADR-0005 D1.
    /// </summary>
    public const string HttpClientName = "SendGrid";

    public static IServiceCollection AddSendGrid(this IServiceCollection services)
    {
        // ADR-0005 D1 — SendGrid's HTTP transport is a pooled, named IHttpClientFactory client with
        // OTel HttpClientInstrumentation and the standard resilience handler as its only pipeline,
        // instead of newing a fresh SendGridClient socket per send. It retries Transient
        // (5xx/408/429) and does NOT retry 401/403/4xx (D1.2). The send is a keyless POST, so a 202
        // lost to a timeout can deliver a mail twice; the retry stays, because the inline senders
        // swallow a failed send and a short outage would otherwise lose the mail outright.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is the one way to take the host's default off one client.
        services.AddHttpClient(HttpClientName)
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler();
#pragma warning restore EXTEXP0001

        services.AddTransient<ISendGridClientFactory, SendGridClientFactory>(provider =>
        {
            var sendGridConfig = provider.GetRequiredService<ISendGridConfig>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            return new SendGridClientFactory(sendGridConfig, httpClientFactory);
        });

        return services;
    }
}
