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

    // A genuine miss (200 + no feature) and a transient degrade (429/5xx/timeout, surfacing after the
    // resilience handler's Retry-After-aware budget is exhausted) both leave the address without
    // coordinates, but they are observably DISTINCT events so a rate-limit window is not invisible
    // behind the routine no-result Warning (ADR-0005 D4.2 / runtime-readiness.md).
    public static readonly EventId GenuineMissEvent = new(7185_01, "MapboxGeocodeNoResult");
    public static readonly EventId TransientDegradeEvent = new(7185_02, "MapboxGeocodeTransientDegrade");
    public static readonly EventId AuthConfigDegradeEvent = new(7185_03, "MapboxGeocodeAuthConfigDegrade");

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
                _logger.LogWarning(GenuineMissEvent,
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
            _logger.LogError(AuthConfigDegradeEvent, exception,
                "Mapbox geocoding failed: {FailureClass} (provider config/credentials) for {City}/{ZipCode}; continuing without coordinates.",
                failureClass, city, zipCode);
        }
        else
        {
            // Transient/Timeout (incl. a 429 whose Retry-After-aware retry budget the resilience
            // handler exhausted): a distinct event so the rate-limit/outage degrade is observable
            // and not indistinguishable from a genuine no-result miss.
            _logger.LogWarning(TransientDegradeEvent, exception,
                "Mapbox geocoding degraded: {FailureClass} (rate-limit/outage) for {City}/{ZipCode}; continuing without coordinates.",
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
