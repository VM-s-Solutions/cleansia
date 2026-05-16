namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IMapboxConfig
{
    string GeocodingAccessToken { get; }
    string? DefaultCountryIsoCode { get; }
}
