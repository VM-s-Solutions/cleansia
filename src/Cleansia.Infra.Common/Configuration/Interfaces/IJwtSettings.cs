namespace Cleansia.Infra.Common.Configuration.Interfaces;

public interface IJwtSettings
{
    string Secret { get; set; }

    double DefaultTokenExpHours { get; set; }

    double CookieTokenExpHours { get; set; }
}