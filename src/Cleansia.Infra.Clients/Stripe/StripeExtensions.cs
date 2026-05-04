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

        // Direct IStripeClient injection — used by CreatePaymentIntent and any
        // future handler that needs Stripe operations without the per-call
        // factory indirection. Same StripeClient instance type as the factory
        // produces, just registered directly so MediatR handlers can request it.
        services.AddTransient<IStripeClient, StripeClient>();

        return services;
    }
}