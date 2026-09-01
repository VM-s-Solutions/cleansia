namespace Cleansia.Infra.Services.Geocoding;

public record GeoCoordinates(double Latitude, double Longitude);

/// <summary>
/// One address the customer can pick from an autocomplete list, already split into
/// the fields an order needs. Parsing the provider's feature shape belongs here and
/// not in a browser: the shape is the provider's, it changes with their API version,
/// and every client that parsed it separately would have to change with it.
/// </summary>
public record GeoSuggestion(
    string PlaceName,
    string Street,
    string City,
    string ZipCode,
    double Latitude,
    double Longitude);

/// <summary>A rendered map image, with the content type the provider returned it as.</summary>
public record GeoStaticMap(byte[] Content, string ContentType);

public interface IGeocodingService
{
    Task<GeoCoordinates?> GeocodeAsync(
        string street,
        string city,
        string zipCode,
        string? countryIsoCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Prefix search for an address the customer is typing. Distinct from
    /// <see cref="GeocodeAsync"/>, which resolves ONE already-complete address to a
    /// coordinate pair and returns nothing else — a different provider endpoint, a
    /// different result shape, and a different question.
    /// </summary>
    Task<IReadOnlyList<GeoSuggestion>> SearchAsync(
        string query,
        string? countryIsoCodes,
        string? language,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// A static map image centred on a coordinate. Null when geocoding is not
    /// provisioned or the provider refused — the caller shows the panel empty
    /// rather than a broken image.
    /// </summary>
    Task<GeoStaticMap?> GetStaticMapAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
