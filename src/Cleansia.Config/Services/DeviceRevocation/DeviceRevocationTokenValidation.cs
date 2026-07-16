using System.Security.Claims;
using Cleansia.Core.AppServices.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cleansia.Config.Services.DeviceRevocation;

/// <summary>
/// The one shared enforcement hook the two mobile hosts call from their <c>OnTokenValidated</c>
/// (ADR-0026 D3). The logic lives here once; each host's per-host touch is a single call. It parses
/// the validated principal, consults <see cref="IRevokedDeviceDirectory"/>, and on a match fails
/// authentication with <c>context.Fail("device_revoked")</c> - a 401, never a 403, so the clients'
/// existing 401 -> refresh -> refresh-rejected -> forced-sign-out machinery drives the sign-out with
/// zero client change.
/// </summary>
public static class DeviceRevocationTokenValidation
{
    public const string FailureReason = "device_revoked";

    public static void EnforceDeviceRevocation(this TokenValidatedContext context)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<DeviceRevocationOptions>>().Value;

        // Kill switch: the enforcement helper no-ops, but the refresher keeps polling so the snapshot
        // stays warm and telemetry never goes dark (ADR-0026 A5).
        if (!options.Enabled)
        {
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var deviceId = identity.FindFirst(AuthExtensions.DeviceIdClaimType)?.Value;

        // No device_id claim (web-shaped/legacy/transition tokens) can never match the directory -
        // it passes, bounded by the 30-min TTL backstop (ADR-0026 D6).
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var directory = context.HttpContext.RequestServices.GetRequiredService<IRevokedDeviceDirectory>();

        if (directory.IsRevoked(userId, deviceId, ReadIssuedAt(identity)))
        {
            context.Fail(FailureReason);
        }
    }

    // iat is a NumericDate (Unix seconds) claim both mint sites always stamp. A device-claimed token
    // with a missing/unreadable iat is treated as unprovable-age by the directory (null -> revoked, A2).
    private static DateTimeOffset? ReadIssuedAt(ClaimsIdentity identity)
    {
        var iat = identity.FindFirst("iat")?.Value;
        if (long.TryParse(iat, out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }
}
