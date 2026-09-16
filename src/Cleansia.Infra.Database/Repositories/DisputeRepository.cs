using System.Linq.Expressions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
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

    public IQueryable<Dispute> GetQueryableForOwner(string userId)
    {
        // Past the tenant filter: the dispute carries its order's operator, which for a cross-market
        // booking is not the customer's own company. The pin is the caller's own id — a caller
        // obligation this method cannot verify, so every caller reads it off the JWT.
        return GetQueryableIgnoringTenant().Where(d => d.UserId == userId);
    }

    public Task<Dispute?> GetDisputeWithDetailsForOwnerAsync(string disputeId, string userId, CancellationToken cancellationToken)
    {
        return WithDetailGraph(GetQueryableForOwner(userId))
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
    }

    private IQueryable<Dispute> ForOperator(string? operatorTenantId)
        => GetQueryableIgnoringTenant().Where(d => operatorTenantId != null && d.TenantId == operatorTenantId);

    public Task<int> GetCountForOperatorAsync(string? operatorTenantId, Expression<Func<Dispute, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = ForOperator(operatorTenantId);
        return (filter is null ? query : query.Where(filter)).CountAsync(cancellationToken);
    }

    public IQueryable<Dispute> GetPagedSortForOperator<TSort>(
        string? operatorTenantId, int offset, int limit, Expression<Func<Dispute, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<Dispute>
        => PagedSort<TSort>(ForOperator(operatorTenantId), offset, limit, filter, sort);

    public Task<int> GetCountForOwnerAsync(string userId, Expression<Func<Dispute, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = GetQueryableForOwner(userId);
        return (filter is null ? query : query.Where(filter)).CountAsync(cancellationToken);
    }

    public IQueryable<Dispute> GetPagedSortForOwner<TSort>(
        string userId, int offset, int limit, Expression<Func<Dispute, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<Dispute>
        => PagedSort<TSort>(GetQueryableForOwner(userId), offset, limit, filter, sort);

    public async Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId, CancellationToken cancellationToken)
    {
        if (!await GetDbSet().AnyAsync(d => d.Id == disputeId, cancellationToken)) return null;
        // Prove the operator's root access before loading its cross-company message authors.
        return await WithDetailGraph(GetQueryableIgnoringTenant())
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
    }

    private static IQueryable<Dispute> WithDetailGraph(IQueryable<Dispute> disputes)
    {
        return disputes
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
            .AsNoTracking();
    }

    public Task<Dispute?> GetForUpdateAsync(string disputeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken);
    }

    public override async Task<Dispute?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!await GetDbSet().AnyAsync(d => d.Id == id, cancellationToken)) return null;
        return await GetQueryableIgnoringTenant()
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Author)
            .Include(d => d.Evidence)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
