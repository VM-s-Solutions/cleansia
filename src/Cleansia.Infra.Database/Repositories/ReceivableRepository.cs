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

    public async Task<IReadOnlyList<Receivable>> GetOpenForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await GetQueryableIgnoringTenant()
            .Include(r => r.Order)
            .Include(r => r.Currency)
            .Where(r => r.UserId == userId && r.Status == ReceivableStatus.Open)
            .OrderBy(r => r.CreatedOn)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Receivable?> GetUnpaidCashForOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        return GetQueryable()
            .FirstOrDefaultAsync(r => r.OrderId == orderId && r.Kind == ReceivableKind.UnpaidCash, cancellationToken);
    }

    public Task<Receivable?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .Include(r => r.Order)
                .ThenInclude(o => o!.AssignedEmployees)
            .Include(r => r.Currency)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Receivable>> GetUnchargedOpenIgnoringTenantAsync(int take, CancellationToken cancellationToken)
    {
        return await GetQueryableIgnoringTenant()
            .Include(r => r.Order)
            .Include(r => r.Currency)
            .Where(r => r.Status == ReceivableStatus.Open && r.Attempts == 0)
            .Where(r => Context.Tenants.Any(t => t.Id == r.TenantId && t.ArchiveRequestedOn == null))
            .OrderBy(r => r.CreatedOn)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
