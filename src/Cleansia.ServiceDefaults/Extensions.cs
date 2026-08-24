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
using Sentry;
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
                    .AddRuntimeInstrumentation()
                    // A MeterProvider emits ONLY the meters it is told about, and the Azure Monitor
                    // distro adds no wildcard — so these two lines are the whole difference between the
                    // app's own counters reaching App Insights and being recorded into nothing. They
                    // were missing from both overloads until 2026-08-22, which is why
                    // RateLimitMetrics' own doc comment describing "the existing App Insights metric
                    // pipeline" was describing a pipeline with no subscriber on the other end.
                    //
                    // Literals, not IntegrationFailureMetrics.MeterName / RateLimitMetrics.MeterName:
                    // Cleansia.Config already references this project, so referencing back would be a
                    // cycle. CustomMeterSubscriptionTests pins the two against the
                    // constants from Cleansia.Tests, which can see both.
                    .AddMeter("Cleansia.Integration")
                    .AddMeter("Cleansia.RateLimiting");
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
    /// Configures Sentry error monitoring on the web host. The OpenTelemetry bridge is the web half of
    /// it: it needs the <c>TracerProvider</c> <see cref="AddServiceDefaults(IServiceCollection, IConfiguration, IHostEnvironment)"/>
    /// builds, which a host outside ASP.NET does not have.
    /// </summary>
    public static IWebHostBuilder UseSentryMonitoring(this IWebHostBuilder webBuilder)
    {
        webBuilder.UseSentry((context, options) =>
        {
            if (ConfigureSentry(options, context.Configuration["Sentry:Dsn"]))
            {
                options.UseOpenTelemetry();
            }
        });

        return webBuilder;
    }

    /// <summary>
    /// Sentry on a host that has no <see cref="IWebHostBuilder"/> — the isolated Functions worker, which
    /// runs every timer sweep, the fiscal receipt queue and the invoice queue, and which since
    /// Application Insights was removed has no other off-box error signal at all.
    ///
    /// <para>Neither shape the API hosts use applies: <c>Sentry.AspNetCore</c>'s <c>UseSentry</c> hangs
    /// off the web host, and the <c>Sentry.OpenTelemetry</c> bridge needs a <c>TracerProvider</c> the
    /// worker never builds (it does not call <c>AddServiceDefaults</c>, so this is also its ONE Sentry
    /// initialisation). What is left is the logging integration, which is what that host actually needs:
    /// its alerting contract — <c>PoisonHandlerBase</c> step 2 — is an <c>ILogger.LogError</c> call.</para>
    /// </summary>
    public static ILoggingBuilder AddSentryMonitoring(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.AddSentry(options =>
        {
            // The alert threshold for a dead-lettered fiscal message, pinned rather than inherited: below
            // this level a log is a breadcrumb attached to some later event, not an event of its own.
            options.MinimumEventLevel = LogLevel.Error;
            ConfigureSentry(options, configuration["Sentry:Dsn"]);
        });

        return logging;
    }

    /// <summary>
    /// The ONE DSN guard, shared by every host. Absent OR blank DSN → Sentry stays disabled: dev and the
    /// test hosts run without one, and a host that fails to start over missing telemetry is worse than a
    /// host with none. Empty string is the SDK's explicit "disabled" sentinel — clearing the DSN is what
    /// keeps it from initialising with an invalid value (which throws) or, on null, from adopting
    /// whatever <c>SENTRY_DSN</c> happens to be in the environment.
    /// </summary>
    /// <returns><c>true</c> when a usable DSN was applied.</returns>
    private static bool ConfigureSentry(SentryOptions options, string? dsn)
    {
        if (string.IsNullOrWhiteSpace(dsn))
        {
            options.Dsn = string.Empty;
            options.AutoSessionTracking = false;
            return false;
        }

        options.Dsn = dsn;
        options.SendDefaultPii = false;
        options.AttachStacktrace = true;
        options.AutoSessionTracking = true;
        options.TracesSampleRate = 0.2;
        // Drop cancellation noise — RequestLoggingMiddleware re-throws OperationCanceledException for a
        // client disconnect, and worker shutdown cancels every in-flight invocation; without this filter
        // Sentry captures every navigation-cancelled request and every deploy.
        options.SetBeforeSend((evt, _) => evt.Exception is OperationCanceledException ? null : evt);
        return true;
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
                    .AddRuntimeInstrumentation()
                    // A MeterProvider emits ONLY the meters it is told about, and the Azure Monitor
                    // distro adds no wildcard — so these two lines are the whole difference between the
                    // app's own counters reaching App Insights and being recorded into nothing. They
                    // were missing from both overloads until 2026-08-22, which is why
                    // RateLimitMetrics' own doc comment describing "the existing App Insights metric
                    // pipeline" was describing a pipeline with no subscriber on the other end.
                    //
                    // Literals, not IntegrationFailureMetrics.MeterName / RateLimitMetrics.MeterName:
                    // Cleansia.Config already references this project, so referencing back would be a
                    // cycle. CustomMeterSubscriptionTests pins the two against the
                    // constants from Cleansia.Tests, which can see both.
                    .AddMeter("Cleansia.Integration")
                    .AddMeter("Cleansia.RateLimiting");
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

    /// <summary>
    /// The site ROOT, answering 200 so the platform's own ping stops being counted as a failure.
    ///
    /// <para><b>This is not a health check and must never become one.</b> App Service pings <c>/</c>
    /// to keep an Always On instance warm, and it does so regardless of <c>healthCheckPath</c> — which
    /// is already <c>/alive</c> and working. These hosts are APIs with no root route, so every one of
    /// those pings returned 404: 1.25k of them in a single day on the partner mobile host, which made
    /// "failed requests" the loudest signal in Application Insights and buried the real ones.</para>
    ///
    /// <para>It reports nothing about dependencies on purpose. <c>/alive</c> is liveness and
    /// <c>/health</c> is readiness; this is neither, it is an acknowledgement that the process is
    /// listening. Giving it a dependency check would make a warm-up ping able to report a database
    /// outage, which is exactly the confusion the two existing endpoints were split to avoid.</para>
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Ok("Cleansia API"));

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
        endpoints.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Ok("Cleansia API"));

        endpoints.MapHealthChecks("/health");

        endpoints.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return endpoints;
    }
}
