using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The per-currency Stripe Customer seam has to be REGISTERED, and — exactly as with the trial
/// resolver — nothing else notices if it is not: MediatR resolves the subscribe handlers on the first
/// request, so a missing collaborator is a 500 on the money path rather than a boot failure.
/// </summary>
public sealed class StripeCustomerResolverWiringPinTests
{
    [Fact]
    public void SharedConfig_RegistersTheStripeCustomerResolver()
    {
        var services = new ServiceCollection().AddServices();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IStripeCustomerResolver));

        Assert.Equal(typeof(StripeCustomerResolver), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
