using System.Security.Claims;
using Cleansia.Config.Services.DeviceRevocation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cleansia.Config.Services.UserRevocation;

/// <summary>
/// The shared reset-cutoff enforcement hook the two mobile hosts call from their <c>OnTokenValidated</c>
/// right after the device probe (ADR-0027 D4). The logic lives here once; each host's per-host touch is
/// a single call. It reads the already-validated principal's <c>sub</c> + <c>iat</c>, consults
/// <see cref="IRevokedUserDirectory"/>, and on a match fails authentication with
/// <c>context.Fail("session_revoked")</c> - a 401, never a 403, so the clients' existing
/// 401 -> refresh -> refresh-rejected -> forced-sign-out machinery drives the sign-out with zero client
/// change (the reset already killed the refresh chain, so the refresh is rejected).
///
/// It honours the SAME <see cref="DeviceRevocationOptions.Enabled"/> kill switch as the device check
/// (ADR-0027 D7): one flip disables both mobile revocation checks, and the refresher keeps polling so
/// the snapshot stays warm. The key is <c>sub</c>, which every access token carries, so unlike the
/// device check there is NO claim-transition window - it is effective from the first post-deploy request.
/// A distinct reason string ("session_revoked" vs the device helper's "device_revoked") keeps the two
/// causes separable in logs; both produce the same client-visible 401.
/// </summary>
public static class UserRevocationTokenValidation
{
    public const string FailureReason = "session_revoked";

    public static void EnforceUserRevocation(this TokenValidatedContext context)
    {
        var options = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<DeviceRevocationOptions>>().Value;

        // Kill switch: the enforcement helper no-ops, but the refresher keeps polling so the snapshot
        // stays warm and telemetry never goes dark (ADR-0027 D7 / ADR-0026 A5 parity).
        if (!options.Enabled)
        {
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var directory = context.HttpContext.RequestServices.GetRequiredService<IRevokedUserDirectory>();

        if (directory.IsRevoked(userId, ReadIssuedAt(identity)))
        {
            context.Fail(FailureReason);
        }
    }

    // iat is a NumericDate (Unix seconds) claim both mint sites always stamp. A token whose sub matches
    // a reset entry but whose iat is missing/unreadable is treated as unprovable-age by the directory
    // (null -> revoked, A2); a legitimate anomaly self-heals via refresh in one round trip.
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
