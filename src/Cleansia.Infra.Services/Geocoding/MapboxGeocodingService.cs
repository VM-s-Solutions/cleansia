using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Cleansia.Core.Clients.Abstractions;
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

        // Geocoding degrades to null (a missing coordinate never blocks order creation), but the
        // failure is classified first so an AuthConfig/Permanent fault is an owner-alert signal, not a
        // routine swallowed Warning.
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Degrade(IntegrationFailureClassifier.FromHttpStatus((int)response.StatusCode),
                    city, zipCode, exception: null);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<MapboxResponse>(stream, cancellationToken: cancellationToken);

            var coordinates = payload?.Features?.FirstOrDefault()?.Geometry?.Coordinates;
            if (coordinates == null || coordinates.Count < 2)
            {
                _logger.LogWarning(
                    "Mapbox geocoding returned no coordinates for {City}/{ZipCode}; continuing without coordinates.",
                    city, zipCode);
                return null;
            }

            // Mapbox v6 returns [longitude, latitude].
            return new GeoCoordinates(coordinates[1], coordinates[0]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return Degrade(IntegrationFailureClassifier.FromException(ex), city, zipCode, ex);
        }
    }

    private GeoCoordinates? Degrade(IntegrationFailureClass failureClass, string city, string zipCode, Exception? exception)
    {
        IntegrationFailureMetrics.Record(HttpClientName, failureClass);

        if (failureClass == IntegrationFailureClass.AuthConfig)
        {
            _logger.LogError(exception,
                "Mapbox geocoding failed: {FailureClass} (provider config/credentials) for {City}/{ZipCode}; continuing without coordinates.",
                failureClass, city, zipCode);
        }
        else
        {
            _logger.LogWarning(exception,
                "Mapbox geocoding failed: {FailureClass} for {City}/{ZipCode}; continuing without coordinates.",
                failureClass, city, zipCode);
        }

        return null;
    }

    private sealed record MapboxResponse(
        [property: JsonPropertyName("features")] List<Feature>? Features);

    private sealed record Feature(
        [property: JsonPropertyName("geometry")] Geometry? Geometry);

    private sealed record Geometry(
        [property: JsonPropertyName("coordinates")] List<double>? Coordinates);
}
