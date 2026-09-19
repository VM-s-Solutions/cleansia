using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Extensions;

/// <summary>Account tenant for a user id from the session or an already-authorized resource.</summary>
public static class AccountTenantExtensions
{
    public static Task<string?> UserTenantIdAsync(this CleansiaDbContext context, string userId, CancellationToken cancellationToken)
    {
        return context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => u.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
