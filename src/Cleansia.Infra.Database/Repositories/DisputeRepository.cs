using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class DisputeRepository(CleansiaDbContext context) : BaseRepository<Dispute>(context), IDisputeRepository
{
    public IQueryable<Dispute> GetDisputesByOrderId(string orderId)
    {
        return GetDbSet()
            .Where(d => d.OrderId == orderId)
            .Include(d => d.Messages)
            .Include(d => d.Evidence);
    }

    public IQueryable<Dispute> GetDisputesByUserId(string userId)
    {
        return GetDbSet()
            .Where(d => d.UserId == userId)
            .Include(d => d.Order)
            .Include(d => d.Messages)
            .Include(d => d.Evidence);
    }

    public IQueryable<Dispute> GetDisputesByStatus(DisputeStatus status)
    {
        return GetDbSet()
            .Where(d => d.Status == status)
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
            .Include(d => d.Evidence);
    }

    public IQueryable<Dispute> GetDisputesWithDetails()
    {
        return GetDbSet()
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .AsSplitQuery();
    }

    public Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId)
    {
        return GetDbSet()
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == disputeId);
    }

    public override Task<Dispute?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
