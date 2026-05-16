using Cleansia.Core.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Cleansia.Infra.Database;

public class UserSessionProvider(IHttpContextAccessor httpContextAccessor) : IUserSessionProvider
{
    public ClaimsPrincipal? GetUser()
    {
        return httpContextAccessor.HttpContext?.User;
    }

    public IEnumerable<Claim> GetUserClaims()
    {
        return GetUser()?.Claims ?? [];
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

    public const string EmployeeIdClaimType = "employee_id";
}