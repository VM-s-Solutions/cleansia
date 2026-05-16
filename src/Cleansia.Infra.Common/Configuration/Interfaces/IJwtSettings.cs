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

    /// <summary>JWT issuer claim — common across all hosts. Identifies the issuing system.</summary>
    string Issuer { get; }
}

public static class JwtAudiences
{
    public const string Customer = "cleansia.customer";
    public const string Partner = "cleansia.partner";
    public const string Mobile = "cleansia.mobile";
    public const string Admin = "cleansia.admin";
}