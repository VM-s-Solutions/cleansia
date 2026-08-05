using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sentry.OpenTelemetry;

namespace Cleansia.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Adds service defaults for projects using the Startup class pattern.
    /// </summary>
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSentry();
            });

        AddTelemetryExporters(services, configuration);

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        services.AddServiceDiscovery();
        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return services;
    }

    /// <summary>
    /// Configures Sentry error monitoring on the web host.
    /// Reads DSN from configuration key "Sentry:Dsn". If the DSN is absent OR blank, Sentry is left
    /// completely uninitialized (disabled) — an EMPTY DSN is treated the same as a missing one, because
    /// the Sentry SDK rejects an empty/whitespace DSN and would otherwise fail startup. (Dev runs with no
    /// DSN; prod sets the real one.)
    /// </summary>
    public static IWebHostBuilder UseSentryMonitoring(this IWebHostBuilder webBuilder)
    {
        webBuilder.UseSentry((context, options) =>
        {
            var dsn = context.Configuration["Sentry:Dsn"];
            if (string.IsNullOrWhiteSpace(dsn))
            {
                // No usable DSN → leave Sentry disabled. Clearing the DSN keeps the SDK from attempting
                // to initialize with an invalid value (which throws on startup).
                options.Dsn = string.Empty;
                options.AutoSessionTracking = false;
                return;
            }

            options.Dsn = dsn;
            options.SendDefaultPii = false;
            options.AttachStacktrace = true;
            options.AutoSessionTracking = true;
            options.TracesSampleRate = 0.2;
            options.UseOpenTelemetry();
            // Drop client-disconnect noise — RequestLoggingMiddleware re-throws
            // OperationCanceledException for normal cancellation; without this
            // filter Sentry captures every navigation-cancelled request.
            options.SetBeforeSend((evt, _) => evt.Exception is OperationCanceledException ? null : evt);
        });

        return webBuilder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        AddTelemetryExporters(builder.Services, builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Where the OTel pipeline is SHIPPED, for both <c>AddServiceDefaults</c> overloads.
    ///
    /// It lives here, taking the two things either overload can supply, because the exporter previously
    /// hung off the <see cref="IHostApplicationBuilder"/> path alone — which no host in this solution
    /// calls. The five APIs use the Startup-class pattern, so they reach service registration with an
    /// <see cref="IServiceCollection"/> and nothing else, and every API host therefore collected
    /// telemetry in-process and exported it nowhere for as long as the exporter had existed. Anything
    /// added to one overload and not the other repeats that, silently.
    ///
    /// Both exporters stay guarded on their own configuration value so a host with neither configured
    /// (local dev, the test hosts) registers no exporter at all.
    /// </summary>
    private static void AddTelemetryExporters(IServiceCollection services, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }

        var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            return;
        }

        services.AddOpenTelemetry().UseAzureMonitor(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    /// <summary>
    /// Maps default health check endpoints for projects using the Startup class pattern.
    /// </summary>
    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health");

        endpoints.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return endpoints;
    }
}
