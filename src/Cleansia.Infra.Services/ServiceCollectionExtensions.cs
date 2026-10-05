using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.Infra.Services.Geocoding;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Cleansia.Infra.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptLayoutBuilder, DefaultReceiptLayoutBuilder>();
        services.AddSingleton<IInvoiceLayoutBuilder, DefaultInvoiceLayoutBuilder>();
        services.AddSingleton<IInvoiceLayoutBuilder, CzechInvoiceLayoutBuilder>();
        services.AddSingleton<LayoutBuilderFactory>();
        services.AddScoped<IPdfService, QuestPdfService>();

        // ADR-0005 D1 — the Mapbox transport is a pooled named IHttpClientFactory client (the
        // reference shape the ADR makes the rule). D1.2/D4.2 — a resilience handler retries only the
        // transient family (408/429/5xx/timeout) with exponential back-off + jitter and HONORS the
        // 429/503 Retry-After header (ShouldRetryAfterHeader), so a rate-limit window backs off rather
        // than being swallowed to a silent null. The 5s per-attempt timeout is the original budget; the
        // 30s total is the only bound on the whole call, an honoured Retry-After included, since the
        // host's standard handler no longer wraps it. The request logging goes: a search URL carries the
        // typed address in its path, a static map URL the coordinates.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is the one way to take the host's default off one client.
        services.AddHttpClient("Mapbox")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers()
            .AddResilienceHandler("mapbox-geocode", builder =>
            {
                builder.AddTimeout(TimeSpan.FromSeconds(30));
                builder.AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldRetryAfterHeader = true,
                });
                builder.AddTimeout(TimeSpan.FromSeconds(5));
            });
        services.AddScoped<IGeocodingService, MapboxGeocodingService>();

        // A lookup sits on a cleaner's profile save and on an admin's approval, both waiting for it, so
        // the whole budget is bounded rather than per attempt only, and the host's standard handler is
        // removed so it cannot wrap that budget in its own retries. The request logging goes too: the
        // URL it writes ends in the cleaner's IČO.
        services.AddHttpClient(AresBusinessRegistry.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .RemoveAllLoggers()
            .RemoveAllResilienceHandlers()
            .AddResilienceHandler("ares-lookup", builder =>
            {
                builder.AddTimeout(TimeSpan.FromSeconds(12));
                builder.AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromMilliseconds(500),
                    MaxDelay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldRetryAfterHeader = true,
                });
                builder.AddTimeout(TimeSpan.FromSeconds(4));
            });
#pragma warning restore EXTEXP0001
        services.AddSingleton(provider => new AresConfig(provider.GetRequiredService<IConfiguration>()));
        services.AddScoped<IBusinessRegistry, AresBusinessRegistry>();

        return services;
    }
}
