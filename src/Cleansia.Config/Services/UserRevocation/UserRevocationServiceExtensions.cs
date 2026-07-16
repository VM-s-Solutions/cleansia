using Cleansia.Config.Services.DeviceRevocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Config.Services.UserRevocation;

public static class UserRevocationServiceExtensions
{
    /// <summary>
    /// Registers the reset-cutoff enforcement stack (ADR-0027): the singleton
    /// <see cref="RevokedUserDirectory"/> and its polling <see cref="RevokedUserDirectoryRefresher"/>.
    /// Called by the two MOBILE hosts only, next to <c>AddDeviceRevocationEnforcement</c> - the three
    /// web hosts never install it (their reset cutoff rides the standing web-host TTL follow-up,
    /// ADR-0024 D4.3). Binds NO new options: it reuses the <c>DeviceRevocation</c> section
    /// (<see cref="DeviceRevocationOptions.Enabled"/>, <c>RefreshSeconds</c>) the device stack already
    /// bound (the SHARED switch, ADR-0027 D7). The shared enforcement helper is
    /// <see cref="UserRevocationTokenValidation.EnforceUserRevocation"/>, wired into each mobile host's
    /// <c>OnTokenValidated</c> right after the device probe.
    /// </summary>
    public static IServiceCollection AddUserRevocationEnforcement(
        this IServiceCollection services, IConfiguration configuration)
    {
        // No Configure<DeviceRevocationOptions> here - AddDeviceRevocationEnforcement already bound the
        // shared section. Re-binding would be a second, drift-prone registration of the same switch.

        services.AddSingleton<RevokedUserDirectory>();
        services.AddSingleton<IRevokedUserDirectory>(sp => sp.GetRequiredService<RevokedUserDirectory>());
        // Register the refresher as a concrete singleton AND as the hosted service pointing at that same
        // instance, so the host tests can resolve it to force a deterministic poll instead of racing the timer.
        services.AddSingleton<RevokedUserDirectoryRefresher>();
        services.AddHostedService(sp => sp.GetRequiredService<RevokedUserDirectoryRefresher>());

        return services;
    }
}
