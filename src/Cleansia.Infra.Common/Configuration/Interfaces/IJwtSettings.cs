namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IJwtSettings
{
    string Secret { get; set; }

    /// <summary>Short-lived access token lifetime in minutes. Replaces the legacy "hours"-based values.</summary>
    double AccessTokenExpMinutes { get; set; }

    /// <summary>Refresh token lifetime when user chose "remember me".</summary>
    double RefreshTokenExpDays { get; set; }

    /// <summary>Refresh token lifetime when user did NOT choose "remember me".</summary>
    double RefreshTokenShortExpDays { get; set; }
}