using Cleansia.Config.Services;
using Cleansia.Core.AppServices.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// Guards the T-0347 channel discriminator wiring: a single charge surface per card order is selected by
/// the per-host <see cref="IOrderChannelProvider"/> registration, not a contract field. The shared
/// <c>AddServices</c> registers the safe Web default (keeps the Stripe Checkout Session flow) via
/// <c>TryAddSingleton</c>; the mobile customer host overrides it with <see cref="OrderChannel.Mobile"/>
/// via <c>AddSingleton</c>, which wins on resolution (last registration of a service type resolves).
/// Mirrors the exact registration calls the two customer hosts make in their ServiceExtensions.
/// </summary>
public sealed class OrderChannelWiringTests
{
    [Fact]
    public void SharedConfig_RegistersWebChannelByDefault()
    {
        var provider = new ServiceCollection().AddServices().BuildServiceProvider();

        var channel = provider.GetRequiredService<IOrderChannelProvider>();

        Assert.Equal(OrderChannel.Web, channel.Channel);
    }

    [Fact]
    public void WebHostRegistration_ResolvesWebChannel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOrderChannelProvider>(new OrderChannelProvider(OrderChannel.Web));
        services.AddServices();

        var channel = services.BuildServiceProvider().GetRequiredService<IOrderChannelProvider>();

        Assert.Equal(OrderChannel.Web, channel.Channel);
    }

    [Fact]
    public void MobileHostRegistration_OverridesSharedDefault_ResolvesMobileChannel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOrderChannelProvider>(new OrderChannelProvider(OrderChannel.Mobile));
        services.AddServices();

        var channel = services.BuildServiceProvider().GetRequiredService<IOrderChannelProvider>();

        Assert.Equal(OrderChannel.Mobile, channel.Channel);
    }
}
