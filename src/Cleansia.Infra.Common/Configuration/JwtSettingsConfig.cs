using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class JwtSettingsConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "JwtSettings"), IJwtSettings
{
    public string Secret { get; set; } = null!;

    public double DefaultTokenExpHours { get; set; } = 6;

    public double CookieTokenExpHours { get; set; } = 1;
}
