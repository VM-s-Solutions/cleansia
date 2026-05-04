using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class JwtSettingsConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "JwtSettings"), IJwtSettings
{
    public string Secret { get; set; } = null!;

    public double AccessTokenExpMinutes { get; set; } = 15;

    public double RefreshTokenExpDays { get; set; } = 30;

    public double RefreshTokenShortExpDays { get; set; } = 1;
}
