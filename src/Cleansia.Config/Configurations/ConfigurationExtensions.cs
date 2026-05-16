using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Configurations;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddConfigurationBindings(this IServiceCollection services)
    {
        services.AddSingleton<IStripeConfig, StripeConfig>();
        services.AddSingleton<ISendGridConfig, SendGridConfig>();
        services.AddSingleton<IJwtSettings, JwtSettingsConfig>();
        services.AddSingleton<IGoogleConfig, GoogleConfig>();
        services.AddSingleton<IMapboxConfig, MapboxConfig>();
        services.AddSingleton<IFcmConfig, FcmConfig>();

        return services;
    }
}