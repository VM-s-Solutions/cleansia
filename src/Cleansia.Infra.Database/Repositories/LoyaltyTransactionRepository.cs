using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LoyaltyTransactionRepository(CleansiaDbContext context)
    : BaseRepository<LoyaltyTransaction>(context), ILoyaltyTransactionRepository
{
    public async Task<IReadOnlyList<LoyaltyTransaction>> GetForAccountAsync(
        string accountId, int offset, int limit, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(t => t.LoyaltyAccountId == accountId)
            .OrderByDescending(t => t.OccurredOn)
            .Skip(offset)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountForAccountAsync(string accountId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(t => t.LoyaltyAccountId == accountId)
            .CountAsync(cancellationToken);
    }

    public Task<LoyaltyTransaction?> GetLatestForOrderSourceAsync(
        string orderId, LoyaltyEarnSource source, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(t => t.OrderId == orderId && t.Source == source)
            .OrderByDescending(t => t.OccurredOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<LoyaltyTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<int> GetRevokedPointsSumForOrderSourceAsync(
        string orderId, LoyaltyEarnSource source, CancellationToken cancellationToken)
    {
        // Revoke rows store Points as a negative delta; negate the SUM to return a positive magnitude.
        var signedSum = await GetDbSet()
            .Where(t => t.OrderId == orderId
                && t.Source == source
                && t.Type == LoyaltyTransactionType.Revoke)
            .SumAsync(t => t.Points, cancellationToken);

        return -signedSum;
    }
}
