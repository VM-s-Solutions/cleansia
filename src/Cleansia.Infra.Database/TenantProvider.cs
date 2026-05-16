using Cleansia.Core.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Infra.Database;

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public const string TenantClaimType = "tenant_id";

    private string? _override;

    public string? GetCurrentTenantId()
    {
        if (!string.IsNullOrEmpty(_override))
        {
            return _override;
        }
        return httpContextAccessor.HttpContext?.User
            .FindFirst(TenantClaimType)?.Value;
    }

    public void SetTenantOverride(string tenantId)
    {
        _override = tenantId;
    }

    public void ClearTenantOverride()
    {
        _override = null;
    }
}
