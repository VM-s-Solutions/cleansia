using System.Net;
using System.Net.Http.Headers;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services;
using Cleansia.Infra.Services.Geocoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

[Collection("IntegrationFailureMeter")]
public class MapboxGeocoding429RetryAfterTests
{
    private const string ValidFeatureJson =
        """{"features":[{"geometry":{"coordinates":[14.42076,50.087811]}}]}""";

    private const string EmptyFeaturesJson = """{"features":[]}""";

    [Fact]
    public async Task AC1_RateLimit_429_Is_Classified_Transient_And_Metered()
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        GeoCoordinates? result;
        using (listener)
        {
            var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{}"),
            });
            result = await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        }
        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox" && m.Class == IntegrationFailureClass.Transient.ToString());
    }

    [Fact]
    public async Task AC1_RateLimit_429_Logs_Distinct_Transient_Event_Not_The_GenuineMiss_Warning()
    {
        var logger = new CapturingLogger<MapboxGeocodingService>();
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{}"),
        }, logger);
        await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        Assert.Contains(logger.Entries, e => e.EventId == MapboxGeocodingService.TransientDegradeEvent.Id);
        Assert.DoesNotContain(logger.Entries, e => e.EventId == MapboxGeocodingService.GenuineMissEvent.Id);
    }

    [Fact]
    public async Task AC2_ServiceUnavailable_503_Is_Transient_With_The_Distinct_Event()
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        var logger = new CapturingLogger<MapboxGeocodingService>();
        GeoCoordinates? result;
        using (listener)
        {
            var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}"),
            }, logger);
            result = await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        }
        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox" && m.Class == IntegrationFailureClass.Transient.ToString());
        Assert.Contains(logger.Entries, e => e.EventId == MapboxGeocodingService.TransientDegradeEvent.Id);
    }

    [Fact]
    public async Task AC2_Timeout_TaskCanceled_Is_Classified_Timeout_With_The_Distinct_Event()
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        var logger = new CapturingLogger<MapboxGeocodingService>();
        GeoCoordinates? result;
        using (listener)
        {
            var service = BuildService(_ => throw new TaskCanceledException("simulated 5s HttpClient timeout"), logger);
            result = await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        }
        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox" && m.Class == IntegrationFailureClass.Timeout.ToString());
        Assert.Contains(logger.Entries, e => e.EventId == MapboxGeocodingService.TransientDegradeEvent.Id);
    }

    [Fact]
    public async Task AC3_RetryAfter_On_429_Drives_The_Resilience_Wait()
    {
        var retryAfter = TimeSpan.FromSeconds(2);
        var attemptTimestamps = new List<DateTimeOffset>();
        var spy = new TimestampingHandler(attemptTimestamps, _ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return response;
        });
        using var provider = BuildClientProvider(spy);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Mapbox");
        try
        {
            using var response = await client.GetAsync("https://api.mapbox.test/forward");
        }
        catch
        {
        }
        Assert.True(attemptTimestamps.Count > 1, $"A 429 (Transient) must be retried. Observed {attemptTimestamps.Count} attempt(s).");
        var gap = attemptTimestamps[1] - attemptTimestamps[0];
        Assert.True(gap >= retryAfter - TimeSpan.FromMilliseconds(250), $"The retry must honor Retry-After ({retryAfter}); gap was {gap}.");
    }

    [Fact]
    public async Task AC4_GenuineMiss_Empty_Features_Returns_Null_On_The_Miss_Event_No_Transient()
    {
        var logger = new CapturingLogger<MapboxGeocodingService>();
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EmptyFeaturesJson),
        }, logger);
        var result = await service.GeocodeAsync("Nowhere 0", "Atlantis", "00000", "cz", CancellationToken.None);
        Assert.Null(result);
        Assert.Contains(logger.Entries, e => e.EventId == MapboxGeocodingService.GenuineMissEvent.Id);
        Assert.DoesNotContain(logger.Entries, e => e.EventId == MapboxGeocodingService.TransientDegradeEvent.Id);
    }

    [Fact]
    public async Task AC5_HappyPath_Valid_Feature_Returns_Coordinates_In_Lon_Lat_Order()
    {
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidFeatureJson),
        });
        var result = await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(50.087811, result!.Latitude, 6);
        Assert.Equal(14.42076, result.Longitude, 6);
    }

    [Fact]
    public async Task AC6_Transient_Degrade_Does_Not_Throw_To_The_Caller()
    {
        var service = BuildService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{}"),
        });
        var result = await service.GeocodeAsync("Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        Assert.Null(result);
    }

    private static MapboxGeocodingService BuildService(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        ILogger<MapboxGeocodingService>? logger = null,
        string accessToken = "mb-token")
    {
        var config = new Mock<IMapboxConfig>();
        config.SetupGet(c => c.GeocodingAccessToken).Returns(accessToken);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new ResponderHandler(responder)));
        return new MapboxGeocodingService(httpClientFactory.Object, config.Object, logger ?? NullLogger<MapboxGeocodingService>.Instance);
    }

    private static ServiceProvider BuildClientProvider(HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddHttpClient("Mapbox").ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        return services.BuildServiceProvider();
    }

    private sealed class ResponderHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class TimestampingHandler(List<DateTimeOffset> timestamps, Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            timestamps.Add(DateTimeOffset.UtcNow);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, int EventId, string Message)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId.Id, formatter(state, exception)));
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
