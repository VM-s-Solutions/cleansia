using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Sentry.AspNetCore;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The two ways an outbound URL leaves the process other than a log line: the HttpClient span's
/// <c>url.full</c>, exported to Sentry and Application Insights, and Sentry's own message handler, which
/// turns a failed call into an event carrying the URL and records every call as a breadcrumb with the full
/// URL. An ARES URL ends in the cleaner's IČO; a Mapbox search URL carries the address being typed.
///
/// <para>The spans come from a real <see cref="SocketsHttpHandler"/>: a stub handler has no diagnostics
/// handler, so it produces no span and the assertion would pass on nothing. The Application Insights rows
/// are the ones that matter most, because the distro assigns its own request filter after the service
/// defaults run.</para>
/// </summary>
public class OutboundUrlTelemetryTests
{
    private const string Ico = "12345678";
    private const string Address = "Vinohradska";
    private const string MapboxToken = "mb-token";
    private const string MapboxSearch =
        "https://api.mapbox.com/geocoding/v5/mapbox.places/" + Address + "%2012%2C%20Praha.json?access_token=" + MapboxToken;

    private const string AppInsights =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/";

    private const string SampleDsn = "https://0123456789abcdef0123456789abcdef@o0.ingest.example.invalid/1";

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, AppInsights)]
    [InlineData(true, null)]
    [InlineData(true, AppInsights)]
    public async Task An_Outbound_Span_Names_The_Host_And_Never_The_Path(bool hostApplicationBuilder, string? connectionString)
    {
        var spans = new ConcurrentQueue<Activity>();
        await using var provider = Composed(hostApplicationBuilder, connectionString, spans);
        var tracing = provider.GetRequiredService<TracerProvider>();

        using (var offline = new HttpClient(new SocketsHttpHandler
               {
                   ConnectCallback = (_, _) => throw new HttpRequestException("offline"),
               }))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => offline.GetAsync(AresBusinessRegistry.Endpoint + Ico));
            await Assert.ThrowsAsync<HttpRequestException>(() => offline.GetAsync(MapboxSearch));
        }

        tracing.ForceFlush();

        var outbound = spans.Where(s => s.GetTagItem("server.address") is "ares.gov.cz" or "api.mapbox.com").ToList();
        Assert.Contains(outbound, s => Equals(s.GetTagItem("server.address"), "ares.gov.cz"));
        Assert.Contains(outbound, s => Equals(s.GetTagItem("server.address"), "api.mapbox.com"));
        Assert.DoesNotContain(outbound, s => Carries(s, Ico) || Carries(s, Address) || Carries(s, MapboxToken));
    }

    /// <summary>
    /// Asserted on the options rather than on a handler chain: a chain built with a DSN starts the
    /// process-wide <c>SentrySdk</c>, and without one Sentry adds no handler at all.
    /// </summary>
    [Fact]
    public void The_Web_Hosts_Add_No_Sentry_Handler_To_Their_Http_Clients()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Sentry:Dsn"] = SampleDsn;
        builder.WebHost.UseSentryMonitoring();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value;

        Assert.Equal(SampleDsn, options.Dsn);
        Assert.True(options.DisableSentryHttpMessageHandler);
    }

    private static ServiceProvider Composed(bool hostApplicationBuilder, string? connectionString, ConcurrentQueue<Activity> spans)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connectionString;
        }

        IServiceCollection services;
        if (hostApplicationBuilder)
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(settings);
            builder.AddServiceDefaults();
            services = builder.Services;
        }
        else
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddServiceDefaults(
                configuration,
                Mock.Of<IHostEnvironment>(e => e.ApplicationName == "Cleansia.Tests" && e.EnvironmentName == "Production"));
        }

        services.Configure<AzureMonitorOptions>(options =>
        {
            options.DisableOfflineStorage = true;
            options.EnableLiveMetrics = false;
        });
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(new Capture(spans)));
        return services.BuildServiceProvider();
    }

    private static bool Carries(Activity span, string value) =>
        span.DisplayName.Contains(value, StringComparison.Ordinal)
        || span.TagObjects.Concat(span.Events.SelectMany(e => e.Tags))
            .Any(tag => tag.Value?.ToString()?.Contains(value, StringComparison.Ordinal) == true);

    private sealed class Capture(ConcurrentQueue<Activity> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Enqueue(data);
    }
}
