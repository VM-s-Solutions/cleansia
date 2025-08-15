using System;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Infra.Clients.Stripe;

public class StripeClientFactory(IStripeConfig config) : IStripeClientFactory
{
    public IStripeClient CreateClient()
    {
        return new StripeClient(config);
    }
}