using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class DisputeRepository(CleansiaDbContext context) : BaseRepository<Dispute>(context), IDisputeRepository
{
    public async Task<IReadOnlyList<Dispute>> GetDisputesByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(d => d.UserId == userId)
            .Include(d => d.Messages)
            .Include(d => d.Evidence)
            .ToListAsync(cancellationToken);
    }

    public Task<Dispute?> GetOpenDisputeForOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(d => d.OrderId == orderId && d.Status != DisputeStatus.Closed)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Dispute?> GetByStripeDisputeIdAsync(string stripeDisputeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(d => d.StripeDisputeId == stripeDisputeId, cancellationToken);
    }

    public Task<Dispute?> GetByStripeDisputeIdIgnoringTenantAsync(string stripeDisputeId, CancellationToken cancellationToken)
    {
        // System-level read for the chargeback webhook (ADR-0006 D4): a charge.dispute.* event
        // arrives with no tenant context. Bypass the tenant filter; the caller re-scopes via
        // SetTenantOverride before writing.
        return GetDbSet()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.StripeDisputeId == stripeDisputeId, cancellationToken);
    }

    public Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId)
    {
        return GetDbSet()
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Author)
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
                .ThenInclude(m => m.Author)
            .Include(d => d.Evidence)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
