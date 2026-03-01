using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class GdprRequestRepository(CleansiaDbContext context) : BaseRepository<GdprRequest>(context), IGdprRequestRepository
{
    public Task<List<GdprRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedOn)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasPendingRequestAsync(string userId, string requestType, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(r => r.UserId == userId
                && r.RequestType == requestType
                && (r.Status == GdprRequestStatus.Pending || r.Status == GdprRequestStatus.Processing),
                cancellationToken);
    }
}
