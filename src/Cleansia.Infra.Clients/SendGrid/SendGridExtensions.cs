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
        // ADR-0005 D1 — SendGrid's HTTP transport is sourced from a pooled, named IHttpClientFactory
        // client so it inherits the standard resilience handler + OTel HttpClientInstrumentation
        // instead of newing a fresh SendGridClient socket per send. The standard handler retries
        // Transient (5xx/408/429) and does NOT retry 401/403/4xx — folding in the hand-rolled
        // EmailService Polly (D1.2). Mirrors FiscalServiceCollectionExtensions.cs:54-55.
        services.AddHttpClient(HttpClientName)
            .AddStandardResilienceHandler();

        services.AddTransient<ISendGridClientFactory, SendGridClientFactory>(provider =>
        {
            var sendGridConfig = provider.GetRequiredService<ISendGridConfig>();
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            return new SendGridClientFactory(sendGridConfig, httpClientFactory);
        });

        return services;
    }
}
