using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface ILoyaltyTransactionRepository : IRepository<LoyaltyTransaction, string>
{
    /// <summary>
    /// Pages the activity ledger for an account, ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<LoyaltyTransaction>> GetForAccountAsync(string accountId, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Total transaction count for the account (used for paging metadata).
    /// </summary>
    Task<int> CountForAccountAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotency check — returns the latest transaction for a given
    /// order + source, or null if none exists. Used by LoyaltyService to
    /// avoid double-grant / double-revoke when CompleteOrder or CancelOrder
    /// is replayed.
    /// </summary>
    Task<LoyaltyTransaction?> GetLatestForOrderSourceAsync(string orderId, LoyaltyEarnSource source, CancellationToken cancellationToken);
}
