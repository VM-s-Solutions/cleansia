using System.Globalization;
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
    private const string SearchEndpoint = "https://api.mapbox.com/geocoding/v5/mapbox.places";
    private const string StaticEndpoint = "https://api.mapbox.com/styles/v1/mapbox/streets-v12/static";

    // Under three characters a prefix search matches most of a country and costs a
    // billed request to say so.
    private const int MinQueryLength = 3;
    private const int MaxQueryLength = 120;

    private const int StaticZoom = 15;
    // The panel is 320x206 CSS px; @2x covers a retina screen without a second,
    // larger billed request.
    private const string StaticSize = "320x206@2x";

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

    /// <summary>
    /// Forward geocoding with autocomplete, on the v5 `mapbox.places` endpoint.
    ///
    /// v5, where <see cref="GeocodeAsync"/> is on v6, and deliberately so: v6 returns a
    /// different geometry/properties shape and, more to the point, does not carry the
    /// `context[]` rows this method reads the city and postcode out of. The two calls
    /// answer different questions and the provider serves them from different versions.
    /// </summary>
    public async Task<IReadOnlyList<GeoSuggestion>> SearchAsync(
        string query,
        string? countryIsoCodes,
        string? language,
        int limit,
        CancellationToken cancellationToken)
    {
        // Blank token = no-op for local dev machines without Mapbox provisioning. The
        // caller shows no suggestions and the customer types the address by hand.
        if (string.IsNullOrWhiteSpace(_config.GeocodingAccessToken))
        {
            return [];
        }

        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length < MinQueryLength)
        {
            return [];
        }

        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        var url = $"{SearchEndpoint}/{HttpUtility.UrlEncode(trimmed)}.json"
            + $"?autocomplete=true&types=address,postcode&limit={Math.Clamp(limit, 1, 10)}"
            + $"&access_token={_config.GeocodingAccessToken}";
        if (!string.IsNullOrWhiteSpace(countryIsoCodes))
        {
            url += $"&country={HttpUtility.UrlEncode(countryIsoCodes.ToLowerInvariant())}";
        }
        if (!string.IsNullOrWhiteSpace(language))
        {
            url += $"&language={HttpUtility.UrlEncode(language)}";
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return DegradeSearch(IntegrationFailureClassifier.FromHttpStatus((int)response.StatusCode), exception: null);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<MapboxSearchResponse>(stream, cancellationToken: cancellationToken);

            var features = payload?.Features ?? [];
            var results = new List<GeoSuggestion>(features.Count);
            foreach (var feature in features)
            {
                var suggestion = ToSuggestion(feature);
                if (suggestion != null)
                {
                    results.Add(suggestion);
                }
            }

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return DegradeSearch(IntegrationFailureClassifier.FromException(ex), ex);
        }
    }

    public async Task<GeoStaticMap?> GetStaticMapAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.GeocodingAccessToken))
        {
            return null;
        }

        if (double.IsNaN(latitude) || double.IsNaN(longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        // Both the pin and the centre are formatted invariantly: a comma decimal
        // separator would split the coordinate into two path segments and the request
        // would resolve to a different, valid-looking Mapbox URL.
        var lng = longitude.ToString(CultureInfo.InvariantCulture);
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var url = $"{StaticEndpoint}/pin-l+0284c7({lng},{lat})/{lng},{lat},{StaticZoom},0/{StaticSize}"
            + $"?access_token={_config.GeocodingAccessToken}&attribution=true&logo=true";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                IntegrationFailureMetrics.Record(HttpClientName,
                    IntegrationFailureClassifier.FromHttpStatus((int)response.StatusCode));
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            return new GeoStaticMap(bytes, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            IntegrationFailureMetrics.Record(HttpClientName, IntegrationFailureClassifier.FromException(ex));
            _logger.LogWarning(TransientDegradeEvent, ex,
                "Mapbox static map degraded; the address panel renders without an image.");
            return null;
        }
    }

    private IReadOnlyList<GeoSuggestion> DegradeSearch(IntegrationFailureClass failureClass, Exception? exception)
    {
        IntegrationFailureMetrics.Record(HttpClientName, failureClass);

        if (failureClass == IntegrationFailureClass.AuthConfig)
        {
            _logger.LogError(AuthConfigDegradeEvent, exception,
                "Mapbox address search failed: {FailureClass} (provider config/credentials); returning no suggestions.",
                failureClass);
        }
        else
        {
            _logger.LogWarning(TransientDegradeEvent, exception,
                "Mapbox address search degraded: {FailureClass} (rate-limit/outage); returning no suggestions.",
                failureClass);
        }

        // The query itself is NOT logged. It is a partial address a customer is typing
        // — personal data that a degrade path has no reason to persist (S3).
        return [];
    }

    private static GeoSuggestion? ToSuggestion(SearchFeature feature)
    {
        var center = feature.Center;
        if (center == null || center.Count < 2)
        {
            return null;
        }

        // v5 returns [longitude, latitude].
        var longitude = center[0];
        var latitude = center[1];

        var placeName = feature.PlaceName ?? string.Empty;
        var baseStreet = feature.Text ?? string.Empty;
        var houseNumber = feature.Address ?? string.Empty;

        var street = (baseStreet, houseNumber) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{baseStreet} {houseNumber}",
            ({ Length: > 0 }, _) => baseStreet,
            _ => placeName.Split(',').FirstOrDefault()?.Trim() ?? string.Empty,
        };

        // Context rows come most-specific-first, so for Prague the array reads
        // [locality.holesovice, place.praha, district.*, …]. The serviced-city list
        // stores the city (`Praha`), not the district (`Holešovice`), so `place` always
        // wins; `locality` is the fallback for a result with no place row at all.
        var cityFromPlace = string.Empty;
        var cityFromLocality = string.Empty;
        var zipCode = string.Empty;
        foreach (var context in feature.Context ?? [])
        {
            var id = context.Id ?? string.Empty;
            var text = context.Text ?? string.Empty;
            if (id.StartsWith("postcode", StringComparison.Ordinal))
            {
                zipCode = text;
            }
            else if (id.StartsWith("place", StringComparison.Ordinal) && cityFromPlace.Length == 0)
            {
                cityFromPlace = text;
            }
            else if (id.StartsWith("locality", StringComparison.Ordinal) && cityFromLocality.Length == 0)
            {
                cityFromLocality = text;
            }
        }

        return new GeoSuggestion(
            placeName,
            street,
            cityFromPlace.Length > 0 ? cityFromPlace : cityFromLocality,
            zipCode,
            latitude,
            longitude);
    }

    private sealed record MapboxSearchResponse(
        [property: JsonPropertyName("features")] List<SearchFeature>? Features);

    private sealed record SearchFeature(
        [property: JsonPropertyName("place_name")] string? PlaceName,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("address")] string? Address,
        [property: JsonPropertyName("center")] List<double>? Center,
        [property: JsonPropertyName("context")] List<SearchContext>? Context);

    private sealed record SearchContext(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("text")] string? Text);

    private sealed record MapboxResponse(
        [property: JsonPropertyName("features")] List<Feature>? Features);

    private sealed record Feature(
        [property: JsonPropertyName("geometry")] Geometry? Geometry);

    private sealed record Geometry(
        [property: JsonPropertyName("coordinates")] List<double>? Coordinates);
}
