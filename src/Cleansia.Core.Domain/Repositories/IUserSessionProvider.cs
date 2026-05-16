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
}