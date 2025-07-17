using System.Security.Claims;

namespace Cleansia.TestUtilities;

public class TestClaimsPrincipalUser
{
    public TestClaimsPrincipalUser()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Constants.TestUserSession.TestUserId),
            new(ClaimTypes.Email, Constants.TestUserSession.TestUserEmail),
            new(ClaimTypes.Name, Constants.TestUserSession.TestUserName)
        };

        var identity = new ClaimsIdentity(claims);

        Principal = new ClaimsPrincipal(identity);
    }

    public TestClaimsPrincipalUser(
        string? userId = null,
        string? email = null,
        IEnumerable<Claim>? additionalClaims = null,
        string authenticationType = "TestAuthentication",
        bool isAuthenticated = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId ?? Constants.TestUserSession.TestUserId),
            new(ClaimTypes.Email, email ?? Constants.TestUserSession.TestUserEmail),
            new(ClaimTypes.Name, Constants.TestUserSession.TestUserName),
        };

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, authenticationType)
            : new ClaimsIdentity(claims);

        Principal = new ClaimsPrincipal(identity);
    }

    public TestClaimsPrincipalUser(IEnumerable<Claim> claims, string authenticationType = "TestAuthentication")
    {
        Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }

    public TestClaimsPrincipalUser(ClaimsPrincipal principal)
    {
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
    }

    public ClaimsPrincipal Principal { get; }
}