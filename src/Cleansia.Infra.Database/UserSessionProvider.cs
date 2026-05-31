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

    public string? GetTimeZoneId()
    {
        // Clients (mobile + web) set this header so handlers can do
        // their day/week math in the user's wall-clock zone. Header
        // name chosen to mirror the unofficial-but-widespread
        // X-Timezone convention used by Stripe, Notion, etc. Returns
        // null if absent or blank — handlers fall back to UTC.
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        var header = ctx.Request.Headers["X-Time-Zone"].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header;
    }

    public const string EmployeeIdClaimType = "employee_id";
}