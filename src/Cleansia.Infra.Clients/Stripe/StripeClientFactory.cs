using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Infra.Clients.Stripe;

public class StripeClientFactory(
    IStripeConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger<StripeClient> logger) : IStripeClientFactory
{
    public IStripeClient CreateClient()
    {
        return new StripeClient(config, httpClientFactory, logger);
    }
}
