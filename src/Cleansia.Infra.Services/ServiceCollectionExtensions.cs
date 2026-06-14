using Cleansia.Infra.Services.Geocoding;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Layouts;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Cleansia.Infra.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptLayoutBuilder, DefaultReceiptLayoutBuilder>();
        services.AddSingleton<IInvoiceLayoutBuilder, DefaultInvoiceLayoutBuilder>();
        services.AddSingleton<LayoutBuilderFactory>();
        services.AddScoped<IPdfService, QuestPdfService>();

        // ADR-0005 D1 — the Mapbox transport is a pooled named IHttpClientFactory client (the
        // reference shape the ADR makes the rule). D1.2/D4.2 — a resilience handler retries only the
        // transient family (408/429/5xx/timeout) with exponential back-off + jitter and HONORS the
        // 429/503 Retry-After header (ShouldRetryAfterHeader), so a rate-limit window backs off rather
        // than being swallowed to a silent null. The 5s per-attempt timeout is the original budget.
        services.AddHttpClient("Mapbox")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .AddResilienceHandler("mapbox-geocode", builder =>
            {
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

        return services;
    }
}
