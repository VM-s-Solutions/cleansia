using System.Net;
using System.Text;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cleansia.Tests.Features.Addresses;

/// <summary>
/// The provider's feature shape, turned into the fields an order needs.
///
/// This logic used to live in the browser (`mapbox-autocomplete.service.ts`) and was
/// tested there. It moved here with the call, so its test moved here too — deleting the
/// frontend spec without this would have dropped coverage of real branching, not of a
/// transport.
/// </summary>
public class MapboxSuggestionParsingTests
{
    private const string PragueFeature = """
    {
      "features": [
        {
          "place_name": "Vinohradská 12, 120 00 Praha, Česko",
          "text": "Vinohradská",
          "address": "12",
          "center": [14.4378, 50.0755],
          "context": [
            { "id": "postcode.1", "text": "120 00" },
            { "id": "locality.1", "text": "Holešovice" },
            { "id": "place.1", "text": "Praha" }
          ]
        }
      ]
    }
    """;

    [Fact]
    public async Task Splits_A_Feature_Into_Street_City_Zip_And_Coordinates()
    {
        var service = Build(PragueFeature);

        var results = await service.SearchAsync("Vinohradská 12", "cz", "cs", 5, CancellationToken.None);

        var suggestion = Assert.Single(results);
        Assert.Equal("Vinohradská 12, 120 00 Praha, Česko", suggestion.PlaceName);
        Assert.Equal("Vinohradská 12", suggestion.Street);
        Assert.Equal("Praha", suggestion.City);
        Assert.Equal("120 00", suggestion.ZipCode);
        Assert.Equal(50.0755, suggestion.Latitude);
        Assert.Equal(14.4378, suggestion.Longitude);
    }

    [Fact]
    public async Task Prefers_The_Place_Row_Over_The_Locality_Row_For_The_City()
    {
        // The serviced-city list stores "Praha", not the district "Holešovice". A
        // suggestion that resolved to the district would fail the service-area check on
        // an address the platform does in fact serve.
        var service = Build(PragueFeature);

        var results = await service.SearchAsync("Vinohradská 12", "cz", "cs", 5, CancellationToken.None);

        Assert.Equal("Praha", Assert.Single(results).City);
    }

    [Fact]
    public async Task Falls_Back_To_The_Locality_When_There_Is_No_Place_Row()
    {
        var service = Build("""
        {
          "features": [
            {
              "place_name": "Náměstí 1, 100 00 Jinde, Česko",
              "text": "Náměstí",
              "address": "1",
              "center": [14.0, 50.0],
              "context": [{ "id": "locality.9", "text": "Jinde" }]
            }
          ]
        }
        """);

        var results = await service.SearchAsync("Náměstí 1", "cz", "cs", 5, CancellationToken.None);

        Assert.Equal("Jinde", Assert.Single(results).City);
    }

    [Fact]
    public async Task Uses_The_Street_Alone_When_The_Feature_Carries_No_House_Number()
    {
        var service = Build("""
        {
          "features": [
            {
              "place_name": "Zenklova, 180 00 Praha, Česko",
              "text": "Zenklova",
              "center": [14.47, 50.11],
              "context": [{ "id": "place.1", "text": "Praha" }]
            }
          ]
        }
        """);

        Assert.Equal("Zenklova", Assert.Single(await service.SearchAsync("Zenklova", "cz", "cs", 5, CancellationToken.None)).Street);
    }

    [Fact]
    public async Task Falls_Back_To_The_First_Segment_Of_The_Place_Name_When_There_Is_No_Street()
    {
        var service = Build("""
        {
          "features": [
            {
              "place_name": "180 00, Praha, Česko",
              "center": [14.47, 50.11],
              "context": [{ "id": "place.1", "text": "Praha" }]
            }
          ]
        }
        """);

        Assert.Equal("180 00", Assert.Single(await service.SearchAsync("180 00", "cz", "cs", 5, CancellationToken.None)).Street);
    }

    [Fact]
    public async Task Drops_A_Feature_With_No_Usable_Coordinates()
    {
        // An address without a coordinate pair cannot be routed to, so it is not an
        // address the customer may pick.
        var service = Build("""
        { "features": [ { "place_name": "Nowhere", "text": "Nowhere", "center": [14.0] } ] }
        """);

        Assert.Empty(await service.SearchAsync("Nowhere", "cz", "cs", 5, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_Nothing_Below_The_Minimum_Query_Length_Without_Calling_The_Provider()
    {
        var handler = new RecordingHandler(PragueFeature, HttpStatusCode.OK);
        var service = Build(handler);

        Assert.Empty(await service.SearchAsync("ab", "cz", "cs", 5, CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Returns_Nothing_When_No_Token_Is_Provisioned_Without_Calling_The_Provider()
    {
        // A machine with no Mapbox secret is a working machine with no autocomplete,
        // not a broken booking flow.
        var handler = new RecordingHandler(PragueFeature, HttpStatusCode.OK);
        var service = Build(handler, token: string.Empty);

        Assert.Empty(await service.SearchAsync("Vinohradská 12", "cz", "cs", 5, CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Degrades_To_No_Suggestions_When_The_Provider_Fails()
    {
        var service = Build(new RecordingHandler("upstream is down", HttpStatusCode.ServiceUnavailable));

        Assert.Empty(await service.SearchAsync("Vinohradská 12", "cz", "cs", 5, CancellationToken.None));
    }

    [Fact]
    public async Task Never_Puts_The_Query_Or_The_Token_Beyond_The_Provider_Call()
    {
        // The address a customer is typing is personal data, and the token is a
        // credential. Both belong in exactly one place: the outbound request.
        var handler = new RecordingHandler(PragueFeature, HttpStatusCode.OK);
        var service = Build(handler);

        await service.SearchAsync("Vinohradská 12", "cz", "cs", 5, CancellationToken.None);

        Assert.Contains("access_token=test-token", handler.LastUrl);
        Assert.Contains("autocomplete=true", handler.LastUrl);
        Assert.Contains("types=address,postcode", handler.LastUrl);
        Assert.Contains("limit=5", handler.LastUrl);
        Assert.Contains("country=cz", handler.LastUrl);
        Assert.Contains("language=cs", handler.LastUrl);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task Clamps_The_Result_Limit_To_What_The_Provider_Accepts(int requested)
    {
        var handler = new RecordingHandler(PragueFeature, HttpStatusCode.OK);
        var service = Build(handler);

        await service.SearchAsync("Vinohradská 12", "cz", "cs", requested, CancellationToken.None);

        var limit = int.Parse(handler.LastUrl.Split("limit=")[1].Split('&')[0]);
        Assert.InRange(limit, 1, 10);
    }

    // ── plumbing ────────────────────────────────────────────────────────────────────

    private static MapboxGeocodingService Build(string responseBody, string token = "test-token")
        => Build(new RecordingHandler(responseBody, HttpStatusCode.OK), token);

    private static MapboxGeocodingService Build(RecordingHandler handler, string token = "test-token")
        => new(new StubHttpClientFactory(handler), new StubMapboxConfig(token),
            NullLogger<MapboxGeocodingService>.Instance);

    private sealed class RecordingHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string LastUrl { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubMapboxConfig(string token) : IMapboxConfig
    {
        public string GeocodingAccessToken => token;
        public string? DefaultCountryIsoCode => "cz";
    }
}
