using System.Security.Claims;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserSessionProvider
{
    ClaimsPrincipal? GetUser();

    IEnumerable<Claim> GetUserClaims();

    Claim? GetTypedUserClaim(string claimType);

    string? GetUserEmail();

    string? GetUserId();

    string? GetEmployeeId();

    /// <summary>
    /// IANA timezone id the caller's device sent on this request
    /// (e.g. "Europe/Prague"). Used by handlers whose day / week /
    /// month windows have to match the user's wall clock — without
    /// this, "today" is always UTC midnight and users east of UTC
    /// see late-evening activity counted under "yesterday". Null
    /// when the client didn't send the header.
    /// </summary>
    string? GetTimeZoneId();
}