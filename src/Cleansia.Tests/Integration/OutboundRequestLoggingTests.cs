using System.Collections.Concurrent;
using System.Net;
using Cleansia.Infra.Clients.Apns;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services;
using Cleansia.Infra.Services.BusinessRegistry;
using Cleansia.Infra.Services.Geocoding;
using Cleansia.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The HTTP client factory's request logging writes the URL at Information and masks only its query. These
/// clients carry personal data in the path: a Mapbox search the address being typed, a static map its
/// coordinates, an ARES lookup the cleaner's IČO, an APNs push the activity's push token.
/// </summary>
[Collection("IntegrationFailureMeter")]
public class OutboundRequestLoggingTests
{
    private const string MapboxClientName = "Mapbox";

    [Theory]
    [InlineData(MapboxClientName)]
    [InlineData(AresBusinessRegistry.HttpClientName)]
    [InlineData(ApnsLiveActivityClient.HttpClientName)]
    public void A_Client_With_Personal_Data_In_Its_Path_Has_No_Request_Logging(string clientName)
    {
        using var provider = HostComposition(new CapturingLoggerProvider());

        using var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);
        var chain = Chain(handler).Select(h => h.GetType()).ToList();

        Assert.DoesNotContain(typeof(LoggingHttpMessageHandler), chain);
        Assert.DoesNotContain(typeof(LoggingScopeHttpMessageHandler), chain);
    }

    /// <summary>
    /// A 401 is not retried and is logged as the search's own auth/config event, so the capture is known to
    /// be listening. Scopes are captured too: the request scope carries the URL to any sink that includes
    /// scopes.
    /// </summary>
    [Fact]
    public async Task Under_The_Host_Defaults_No_Log_Line_Or_Scope_Carries_A_Typed_Address_Or_Its_Coordinates()
    {
        var logs = new CapturingLoggerProvider();
        var mapbox = new StubHandler(HttpStatusCode.Unauthorized);
        await using var provider = HostComposition(logs, mapbox);
        await using var scope = provider.CreateAsyncScope();
        var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

        await geocoding.SearchAsync("Vinohradská 12, Praha", "cz", 5, CancellationToken.None);
        await geocoding.GetStaticMapAsync(50.0755, 14.4378, CancellationToken.None);

        Assert.Contains(mapbox.Urls, url => url.Contains("Vinohrad", StringComparison.Ordinal));
        Assert.Contains(mapbox.Urls, url => url.Contains("50.0755", StringComparison.Ordinal));
        Assert.Contains(logs.Entries, e => e.EventId == MapboxGeocodingService.AuthConfigDegradeEvent.Id);
        Assert.DoesNotContain(logs.Entries, e =>
            e.Text.Contains("Vinohrad", StringComparison.Ordinal)
            || e.Text.Contains("50.0755", StringComparison.Ordinal)
            || e.Text.Contains("14.4378", StringComparison.Ordinal));
    }

    private static ServiceProvider HostComposition(CapturingLoggerProvider logs, HttpMessageHandler? mapbox = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServiceDefaults(
            configuration,
            Mock.Of<IHostEnvironment>(e => e.ApplicationName == "Cleansia.Tests" && e.EnvironmentName == "Production"));
        services.AddInfrastructureServices();
        services.AddApns();
        services.AddSingleton(Mock.Of<IMapboxConfig>(c => c.GeocodingAccessToken == "mb-token"));
        if (mapbox is not null)
        {
            services.AddHttpClient(MapboxClientName).ConfigurePrimaryHttpMessageHandler(() => mapbox);
        }

        return services.BuildServiceProvider();
    }

    private static IEnumerable<HttpMessageHandler> Chain(HttpMessageHandler? handler)
    {
        for (var current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            yield return current;
        }
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public ConcurrentQueue<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Enqueue(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(int EventId, string Text)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<(int EventId, string Text)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                entries.Enqueue((0, state.ToString() ?? string.Empty));
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue((eventId.Id, formatter(state, exception)));
        }
    }
}
