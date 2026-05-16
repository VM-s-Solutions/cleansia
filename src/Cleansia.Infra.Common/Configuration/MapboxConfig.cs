using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class MapboxConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "Mapbox"), IMapboxConfig
{
    public string GeocodingAccessToken { get; set; } = null!;
    public string? DefaultCountryIsoCode { get; set; } = null;
}
