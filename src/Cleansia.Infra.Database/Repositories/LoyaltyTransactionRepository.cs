using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// Account-owned ledger reads pinned by an authorized order or internal idempotency key,
/// independent of the operator processing that order.
/// </summary>
public class LoyaltyTransactionRepository(CleansiaDbContext context)
    : BaseRepository<LoyaltyTransaction>(context), ILoyaltyTransactionRepository
{
    public Task<LoyaltyTransaction?> GetLatestForOrderSourceAsync(
        string orderId, LoyaltyEarnSource source, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .Where(t => t.OrderId == orderId && t.Source == source)
            .OrderByDescending(t => t.OccurredOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<LoyaltyTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken)
    {
        return GetQueryableIgnoringTenant()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<int> GetRevokedPointsSumForOrderSourceAsync(
        string orderId, LoyaltyEarnSource source, CancellationToken cancellationToken)
    {
        // Revoke rows store Points as a negative delta; negate the SUM to return a positive magnitude.
        var signedSum = await GetQueryableIgnoringTenant()
            .Where(t => t.OrderId == orderId
                && t.Source == source
                && t.Type == LoyaltyTransactionType.Revoke)
            .SumAsync(t => t.Points, cancellationToken);

        return -signedSum;
    }
}
