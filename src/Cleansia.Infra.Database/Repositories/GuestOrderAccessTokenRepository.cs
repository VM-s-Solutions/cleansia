using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class GuestOrderAccessTokenRepository(CleansiaDbContext context)
    : BaseRepository<GuestOrderAccessToken>(context), IGuestOrderAccessTokenRepository
{
    public async Task<IReadOnlyList<GuestOrderAccessToken>> GetLiveForOrderIgnoringTenantAsync(
        string orderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await context.GuestOrderAccessTokens
            .IgnoreQueryFilters()
            .Where(t => t.OrderId == orderId && t.RevokedOn == null && t.ExpiresOn > now)
            .ToListAsync(cancellationToken);
    }
}
