using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ReceivableRepository(CleansiaDbContext context)
    : BaseRepository<Receivable>(context), IReceivableRepository
{
    public Task<bool> HasOpenForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .AnyAsync(r => r.UserId == userId && r.Status == ReceivableStatus.Open, cancellationToken);
    }
}
