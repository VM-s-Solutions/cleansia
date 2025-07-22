using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Infra.Clients.Stripe;

public static class StripeExtensions
{
    public static IServiceCollection AddStripe(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        services.AddTransient<IStripeClientFactory, StripeClientFactory>(provider =>
        {
            var stripeConfig = provider.GetRequiredService<IStripeConfig>();
            return new StripeClientFactory(stripeConfig);
        });

        return services;
    }
}