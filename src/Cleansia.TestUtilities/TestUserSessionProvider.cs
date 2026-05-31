using System.Security.Claims;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.TestUtilities;

public class TestUserSessionProvider : IUserSessionProvider
{
    private readonly ClaimsPrincipal? _user;

    public TestUserSessionProvider(TestClaimsPrincipalUser? user = null)
    {
        _user = user?.Principal;
    }

    public TestUserSessionProvider(string userId, string email, IEnumerable<Claim>? additionalClaims = null)
    {
        _user = new TestClaimsPrincipalUser(userId, email, additionalClaims).Principal;
    }

    public TestUserSessionProvider(IEnumerable<Claim> claims)
    {
        _user = new TestClaimsPrincipalUser(claims).Principal;
    }

    public ClaimsPrincipal? GetUser()
    {
        return _user;
    }

    public IEnumerable<Claim> GetUserClaims()
    {
        return _user?.Claims ?? [];
    }

    public Claim? GetTypedUserClaim(string claimType)
    {
        return GetUserClaims().FirstOrDefault(claim => claim.Type == claimType);
    }

    public string? GetUserEmail()
    {
        return GetTypedUserClaim(ClaimTypes.Email)?.Value;
    }

    public string? GetUserId()
    {
        return GetTypedUserClaim(ClaimTypes.NameIdentifier)?.Value;
    }

    public string? GetEmployeeId()
    {
        return GetTypedUserClaim(EmployeeIdClaimType)?.Value;
    }

    public string? GetTimeZoneId() => TimeZoneIdOverride;

    /// <summary>
    /// Tests that exercise zone-aware dashboard / report code paths
    /// set this to e.g. "Europe/Prague"; null mirrors a client that
    /// didn't send the X-Time-Zone header (handlers default to UTC).
    /// </summary>
    public string? TimeZoneIdOverride { get; init; }

    public const string EmployeeIdClaimType = "employee_id";
}