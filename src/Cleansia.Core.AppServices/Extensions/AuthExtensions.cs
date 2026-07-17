using System.Security.Claims;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Extensions;

public static class AuthExtensions
{
    public static bool CheckIfPasswordSame(this string providedPassword, string saltedHashedPassword)
    {
        return providedPassword.VerifyPassword(saltedHashedPassword);
    }

    public const string EmployeeIdClaimType = "employee_id";
    public const string DeviceIdClaimType = "device_id";

    public static IEnumerable<Claim> SetClaims(this User user, string? employeeId = null, string? deviceId = null)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
        // A unique per-token id. Makes the derived CSRF session key rotate per token issuance instead
        // of being pinned to the stable user id (T-0356), and gives every token jti auditability.
        // CsrfTokenService.GetSessionKey prefers this over the sub/NameIdentifier fallback.
        yield return new Claim("jti", Guid.NewGuid().ToString("N"));
        yield return new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}");
        yield return new Claim(ClaimTypes.Email, user.Email);
        yield return new Claim(ClaimTypes.Role, user.Profile.ToString());

        if (!string.IsNullOrEmpty(user.TenantId))
        {
            yield return new Claim("tenant_id", user.TenantId);
        }

        if (!string.IsNullOrEmpty(employeeId))
        {
            yield return new Claim(EmployeeIdClaimType, employeeId);
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            yield return new Claim(DeviceIdClaimType, deviceId);
        }
    }
}
