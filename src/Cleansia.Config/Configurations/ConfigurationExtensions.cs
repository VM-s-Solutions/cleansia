using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Configurations;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddConfigurationBindings(this IServiceCollection services)
    {
        // Add Configurations here
        services.AddSingleton<IStripeConfig, StripeConfig>();
        services.AddSingleton<ISendGridConfig, SendGridConfig>();

        return services;
    }
}