using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Services.Geocoding;

public class MapboxGeocodingService : IGeocodingService
{
    private const string HttpClientName = "Mapbox";
    private const string ForwardEndpoint = "https://api.mapbox.com/search/geocode/v6/forward";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMapboxConfig _config;
    private readonly ILogger<MapboxGeocodingService> _logger;

    public MapboxGeocodingService(
        IHttpClientFactory httpClientFactory,
        IMapboxConfig config,
        ILogger<MapboxGeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<GeoCoordinates?> GeocodeAsync(
        string street,
        string city,
        string zipCode,
        string? countryIsoCode,
        CancellationToken cancellationToken)
    {
        // Blank token = no-op for local dev machines without Mapbox provisioning.
        if (string.IsNullOrWhiteSpace(_config.GeocodingAccessToken))
        {
            return null;
        }

        var query = $"{street}, {zipCode} {city}";
        var encodedQuery = HttpUtility.UrlEncode(query);

        var url = $"{ForwardEndpoint}?q={encodedQuery}&limit=1&access_token={_config.GeocodingAccessToken}";
        if (!string.IsNullOrWhiteSpace(countryIsoCode))
        {
            url += $"&country={countryIsoCode.ToLowerInvariant()}";
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetFromJsonAsync<MapboxResponse>(url, cancellationToken);

            var coordinates = response?.Features?.FirstOrDefault()?.Geometry?.Coordinates;
            if (coordinates == null || coordinates.Count < 2)
            {
                _logger.LogWarning(
                    "Mapbox geocoding failed for {City}/{ZipCode}; continuing without coordinates.",
                    city, zipCode);
                return null;
            }

            // Mapbox v6 returns [longitude, latitude].
            return new GeoCoordinates(coordinates[1], coordinates[0]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex,
                "Mapbox geocoding failed for {City}/{ZipCode}; continuing without coordinates.",
                city, zipCode);
            return null;
        }
    }

    private sealed record MapboxResponse(
        [property: JsonPropertyName("features")] List<Feature>? Features);

    private sealed record Feature(
        [property: JsonPropertyName("geometry")] Geometry? Geometry);

    private sealed record Geometry(
        [property: JsonPropertyName("coordinates")] List<double>? Coordinates);
}
