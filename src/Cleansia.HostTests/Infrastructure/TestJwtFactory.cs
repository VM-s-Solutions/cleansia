using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cleansia.Core.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// Mints a real, signed access token per audience — the SAME shape the production
/// <c>TokenService.GenerateAccessToken</c> + <c>AuthExtensions.SetClaims</c> emit (HMAC-SHA256 over
/// <c>JwtSettings:Secret</c>, issuer <c>cleansia</c>, the host's audience, and the
/// <see cref="ClaimTypes.NameIdentifier"/> / <see cref="ClaimTypes.Email"/> / <see cref="ClaimTypes.Role"/>
/// / <c>tenant_id</c> / <c>employee_id</c> claims). Because it carries the genuine claims, the host's
/// real <c>AddJwt</c> bearer validation accepts it and the real <c>AddCleansiaAuthorization</c>
/// policies + the handler's <c>IUserSessionProvider</c>/<c>OrderAccessService</c> see the true caller —
/// so the test exercises the full auth + authz pipeline, not a stubbed principal.
/// </summary>
public static class TestJwtFactory
{
    /// <summary>The secret in appsettings.HostTests.json — must match what the booted host reads.</summary>
    public const string Secret = "9eb8a867344aacf6a21bdc768215f0e37def1f945125ba3bdde9aa1e866ddc59";
    public const string Issuer = "cleansia";

    public const string EmployeeIdClaimType = "employee_id";
    public const string TenantClaimType = "tenant_id";

    public static string Mint(
        string audience,
        string userId,
        string email,
        UserProfile profile,
        string? employeeId = null,
        string? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{email} {profile}"),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, profile.ToString()),
        };

        if (!string.IsNullOrEmpty(tenantId))
            claims.Add(new Claim(TenantClaimType, tenantId));

        if (!string.IsNullOrEmpty(employeeId))
            claims.Add(new Claim(EmployeeIdClaimType, employeeId));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
