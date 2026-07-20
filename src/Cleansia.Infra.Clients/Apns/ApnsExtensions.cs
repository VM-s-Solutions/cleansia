using Cleansia.Core.Clients.Abstractions.Apns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.Infra.Clients.Apns;

public static class ApnsExtensions
{
    public static IServiceCollection AddApns(this IServiceCollection services)
    {
        // ADR-0005 D1 — the HTTP/2 APNs transport is a pooled, named IHttpClientFactory client so it
        // inherits the standard resilience handler + OTel HttpClientInstrumentation, instead of newing a
        // socket per send. Mirrors the "Stripe"/"SendGrid" named clients.
        services.AddHttpClient(ApnsLiveActivityClient.HttpClientName)
            .AddStandardResilienceHandler();

        services.TryAddSingleton(TimeProvider.System);

        // Singleton: the provider caches the ~50-min ES256 JWT across sends (Apple rate-limits minting);
        // the client is stateless over the pooled factory. Mirrors the singleton FcmPushDispatcher.
        services.AddSingleton<IApnsJwtProvider, ApnsJwtProvider>();
        services.AddSingleton<ILiveActivityPushClient, ApnsLiveActivityClient>();

        return services;
    }
}
