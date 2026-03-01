using Cleansia.Core.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Infra.Database;

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public const string TenantClaimType = "tenant_id";

    public string? GetCurrentTenantId()
    {
        return httpContextAccessor.HttpContext?.User
            .FindFirst(TenantClaimType)?.Value;
    }
}
