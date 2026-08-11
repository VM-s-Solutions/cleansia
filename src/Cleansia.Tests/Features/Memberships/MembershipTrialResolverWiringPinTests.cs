using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The once-per-customer trial seam has to be REGISTERED, and nothing else notices if it is not.
/// Measured, not assumed: with the registration commented out, every host still boots and the whole
/// HostTests suite stays green — MediatR resolves a handler on the first request, so a missing
/// collaborator surfaces as a 500 on the money path rather than at startup. This is the artifact that
/// goes red instead.
/// </summary>
public sealed class MembershipTrialResolverWiringPinTests
{
    [Fact]
    public void SharedConfig_RegistersTheTrialResolver()
    {
        var services = new ServiceCollection().AddServices();

        // The descriptor, not a resolved instance: the resolver's repository comes from the database
        // registration, which needs a connection string this test has no business supplying.
        var descriptor = Assert.Single(services,
            d => d.ServiceType == typeof(IMembershipTrialResolver));

        Assert.Equal(typeof(MembershipTrialResolver), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
