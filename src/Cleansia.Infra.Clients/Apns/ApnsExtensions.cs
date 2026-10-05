using Cleansia.Core.Clients.Abstractions.Apns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.Infra.Clients.Apns;

public static class ApnsExtensions
{
    public static IServiceCollection AddApns(this IServiceCollection services)
    {
        // ADR-0005 D1 — the HTTP/2 APNs transport is a pooled, named IHttpClientFactory client with OTel
        // HttpClientInstrumentation and the standard resilience handler as its only pipeline, instead of
        // newing a socket per send. No request logging: the URL ends in the activity's push token.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is the one way to take the host's default off one client.
        services.AddHttpClient(ApnsLiveActivityClient.HttpClientName)
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler();
#pragma warning restore EXTEXP0001

        services.TryAddSingleton(TimeProvider.System);

        // Singleton: the provider caches the ~50-min ES256 JWT across sends (Apple rate-limits minting);
        // the client is stateless over the pooled factory. Mirrors the singleton FcmPushDispatcher.
        services.AddSingleton<IApnsJwtProvider, ApnsJwtProvider>();
        services.AddSingleton<ILiveActivityPushClient, ApnsLiveActivityClient>();

        return services;
    }
}
