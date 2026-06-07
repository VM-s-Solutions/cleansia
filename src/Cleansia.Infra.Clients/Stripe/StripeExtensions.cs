using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Infra.Clients.Stripe;

public static class StripeExtensions
{
    /// <summary>
    /// The named <see cref="System.Net.Http.IHttpClientFactory"/> client whose pooled
    /// <c>SocketsHttpHandler</c> the Stripe SDK's transport is built on. ADR-0005 D1.
    /// </summary>
    public const string HttpClientName = "Stripe";

    public static IServiceCollection AddStripe(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        // ADR-0005 D1 — Stripe's HTTP transport is sourced from a pooled, named IHttpClientFactory
        // client so it inherits the standard resilience handler + OTel HttpClientInstrumentation
        // (ServiceDefaults applies AddHttpClientInstrumentation to factory clients), instead of the
        // SDK newing a fresh socket per call. Mirrors FiscalServiceCollectionExtensions.cs:54-55 and
        // the named "Mapbox" client. AddStandardResilienceHandler is explicit here so the registration
        // owns its resilience contract even outside a host that calls ConfigureHttpClientDefaults.
        services.AddHttpClient(HttpClientName)
            .AddStandardResilienceHandler();

        // Let DI inject IStripeConfig + IHttpClientFactory + ILogger; the hand-built lambda is no
        // longer needed now that the factory depends on the IHttpClientFactory transport (ADR-0005 D1).
        services.AddTransient<IStripeClientFactory, StripeClientFactory>();

        // Direct IStripeClient injection — used by CreatePaymentIntent and any
        // future handler that needs Stripe operations without the per-call
        // factory indirection. Same StripeClient instance type as the factory
        // produces, just registered directly so MediatR handlers can request it.
        services.AddTransient<IStripeClient, StripeClient>();

        return services;
    }
}
