using System.Net;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The Mapbox boundary: geocoding still degrades to <c>null</c> (a missing coordinate never blocks
/// order creation), but the failure is now CLASSIFIED first — a 401/403 AuthConfig is an ops signal
/// (recorded on the owner-alert counter), not a routine swallowed Warning. The 429 rate-limit policy
/// is a separate ticket; here a 429 only classifies + degrades, it is not given a bespoke retry.
/// </summary>
[Collection("IntegrationFailureMeter")]
public class MapboxGeocodingBoundaryClassificationTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.Forbidden, IntegrationFailureClass.AuthConfig)]
    public async Task Auth_Failure_Degrades_To_Null_And_Records_The_Owner_Alert_Metric(
        HttpStatusCode status, IntegrationFailureClass expectedClass)
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        GeoCoordinates? result;
        using (listener)
        {
            var service = BuildService(status);

            result = await service.GeocodeAsync(
                "Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        }

        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox" && m.Class == expectedClass.ToString());
    }

    [Fact]
    public async Task Transient_Failure_Degrades_To_Null()
    {
        var service = BuildService(HttpStatusCode.ServiceUnavailable);

        var result = await service.GeocodeAsync(
            "Main St 1", "Prague", "11000", "cz", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Blank_Token_Is_A_NoOp_Returning_Null()
    {
        var service = BuildService(HttpStatusCode.OK, accessToken: "");

        var result = await service.GeocodeAsync(
            "Main St 1", "Prague", "11000", "cz", CancellationToken.None);

        Assert.Null(result);
    }

    public static TheoryData<Exception> ResiliencePipelineRejections() =>
    [
        new TimeoutRejectedException("total request timeout"),
        new BrokenCircuitException("circuit open"),
    ];

    [Theory]
    [MemberData(nameof(ResiliencePipelineRejections))]
    public async Task Search_Degrades_To_No_Suggestions_When_The_Resilience_Pipeline_Gives_Up(Exception rejection)
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        IReadOnlyList<GeoSuggestion> result;
        using (listener)
        {
            var service = BuildService(() => new ThrowingHandler(rejection));

            result = await service.SearchAsync("Vodickova 10", "cz", 5, CancellationToken.None);
        }

        Assert.Empty(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox");
    }

    [Theory]
    [MemberData(nameof(ResiliencePipelineRejections))]
    public async Task Geocode_Degrades_To_Null_When_The_Resilience_Pipeline_Gives_Up(Exception rejection)
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        GeoCoordinates? result;
        using (listener)
        {
            var service = BuildService(() => new ThrowingHandler(rejection));

            result = await service.GeocodeAsync(
                "Main St 1", "Prague", "11000", "cz", CancellationToken.None);
        }

        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox");
    }

    [Theory]
    [MemberData(nameof(ResiliencePipelineRejections))]
    public async Task Static_Map_Degrades_To_Null_When_The_Resilience_Pipeline_Gives_Up(Exception rejection)
    {
        var measurements = FailureMetricsCapture.Start(out var listener);
        GeoStaticMap? result;
        using (listener)
        {
            var service = BuildService(() => new ThrowingHandler(rejection));

            result = await service.GetStaticMapAsync(50.08, 14.42, CancellationToken.None);
        }

        Assert.Null(result);
        Assert.Contains(measurements, m => m.Provider == "Mapbox");
    }

    private static MapboxGeocodingService BuildService(HttpStatusCode status, string accessToken = "mb-token") =>
        BuildService(() => new FixedStatusHandler(status), accessToken);

    private static MapboxGeocodingService BuildService(
        Func<HttpMessageHandler> handler, string accessToken = "mb-token")
    {
        var config = new Mock<IMapboxConfig>();
        config.SetupGet(c => c.GeocodingAccessToken).Returns(accessToken);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler()));

        return new MapboxGeocodingService(
            httpClientFactory.Object, config.Object, NullLogger<MapboxGeocodingService>.Instance);
    }

    private sealed class FixedStatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"message":"boom"}"""),
            });
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
