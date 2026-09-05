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
        // TERMINAL, not merely Closed. Resolved has no outgoing transitions (Dispute.AllowedTransitions)
        // and the only Close() callers go through CanTransitionTo, so a resolved dispute could never
        // become closed — and this filter therefore locked its order out of disputes forever. Order-
        // level that read as a one-shot policy; it becomes a trap the moment a customer is invited to
        // itemise what went wrong, because itemising implies you can come back for the rest.
        //
        // Owner ruling, 2026-09-05: resolve, then a later problem may be raised as a new dispute.
        // Dispute.IsTerminal is the existing name for "this one is finished", so it is the one used.
        return GetDbSet()
            .Where(d => d.OrderId == orderId
                && d.Status != DisputeStatus.Closed
                && d.Status != DisputeStatus.Resolved)
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

    public Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            // The order's CURRENCY comes with it: a dispute's agreed refund is money off that
            // order, and without this the detail screen has nothing to format it in.
            .Include(d => d.Order)
                .ThenInclude(o => o.Currency)
            // The lines the customer selected, and the ORDER graph their names resolve against — the
            // mapper reads the name off the order rather than the catalogue, so a service renamed
            // after the fact still reads as what was bought.
            .Include(d => d.Lines)
            .Include(d => d.Order)
                .ThenInclude(o => o.SelectedServices)
                    .ThenInclude(s => s.Service)
            .Include(d => d.Order)
                .ThenInclude(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p!.IncludedServices)
                            .ThenInclude(s => s.Service)
            .Include(d => d.User)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Author)
            .Include(d => d.Evidence)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
    }

    public Task<Dispute?> GetForUpdateAsync(string disputeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
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
