using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Config.Services.DeviceRevocation;

public static class DeviceRevocationServiceExtensions
{
    /// <summary>
    /// Registers the device-revocation enforcement stack (ADR-0026): the singleton
    /// <see cref="RevokedDeviceDirectory"/> and its polling <see cref="RevokedDeviceDirectoryRefresher"/>,
    /// plus the bound <see cref="DeviceRevocationOptions"/>. Called by the two MOBILE hosts only - the
    /// three web hosts never install it (their tokens carry no device_id claim). The shared enforcement
    /// helper is <see cref="DeviceRevocationTokenValidation.EnforceDeviceRevocation"/>, wired into each
    /// mobile host's <c>OnTokenValidated</c>.
    /// </summary>
    public static IServiceCollection AddDeviceRevocationEnforcement(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DeviceRevocationOptions>(
            configuration.GetSection(DeviceRevocationOptions.SectionName));

        services.AddSingleton<RevokedDeviceDirectory>();
        services.AddSingleton<IRevokedDeviceDirectory>(sp => sp.GetRequiredService<RevokedDeviceDirectory>());
        // Register the refresher as a concrete singleton AND as the hosted service pointing at that same
        // instance, so the host tests can resolve it to force a deterministic poll instead of racing the timer.
        services.AddSingleton<RevokedDeviceDirectoryRefresher>();
        services.AddHostedService(sp => sp.GetRequiredService<RevokedDeviceDirectoryRefresher>());

        return services;
    }
}
