namespace Cleansia.Infra.Services.Geocoding;

public record GeoCoordinates(double Latitude, double Longitude);

public interface IGeocodingService
{
    Task<GeoCoordinates?> GeocodeAsync(
        string street,
        string city,
        string zipCode,
        string? countryIsoCode,
        CancellationToken cancellationToken);
}
