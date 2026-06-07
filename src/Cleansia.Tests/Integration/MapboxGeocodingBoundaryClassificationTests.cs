using System.Diagnostics.Metrics;
using System.Net;
using Cleansia.Core.Clients.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The Mapbox boundary: geocoding still degrades to <c>null</c> (a missing coordinate never blocks
/// order creation), but the failure is now CLASSIFIED first — a 401/403 AuthConfig is an ops signal
/// (recorded on the owner-alert counter), not a routine swallowed Warning. The 429 rate-limit policy
/// is a separate ticket; here a 429 only classifies + degrades, it is not given a bespoke retry.
/// </summary>
public class MapboxGeocodingBoundaryClassificationTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, IntegrationFailureClass.AuthConfig)]
    [InlineData(HttpStatusCode.Forbidden, IntegrationFailureClass.AuthConfig)]
    public async Task Auth_Failure_Degrades_To_Null_And_Records_The_Owner_Alert_Metric(
        HttpStatusCode status, IntegrationFailureClass expectedClass)
    {
        var measurements = CaptureFailureMetrics(out var listener);
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

    private static MapboxGeocodingService BuildService(HttpStatusCode status, string accessToken = "mb-token")
    {
        var config = new Mock<IMapboxConfig>();
        config.SetupGet(c => c.GeocodingAccessToken).Returns(accessToken);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new FixedStatusHandler(status)));

        return new MapboxGeocodingService(
            httpClientFactory.Object, config.Object, NullLogger<MapboxGeocodingService>.Instance);
    }

    private static List<(string? Provider, string? Class)> CaptureFailureMetrics(out MeterListener listener)
    {
        var sink = new List<(string?, string?)>();
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == IntegrationFailureMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name != IntegrationFailureMetrics.FailureCounterName)
            {
                return;
            }

            string? provider = null;
            string? failureClass = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "provider") provider = tag.Value as string;
                if (tag.Key == "class") failureClass = tag.Value as string;
            }

            sink.Add((provider, failureClass));
        });
        listener.Start();
        return sink;
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
}
