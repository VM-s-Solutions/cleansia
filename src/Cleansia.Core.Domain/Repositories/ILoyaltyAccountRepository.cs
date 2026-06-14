using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface ILoyaltyAccountRepository : IRepository<LoyaltyAccount, string>
{
    /// <summary>
    /// Returns the loyalty account for the given user, or null if none exists yet.
    /// </summary>
    Task<LoyaltyAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// No-tracking, ledger-free read for the booking/quote hot path, which reads only
    /// <see cref="LoyaltyAccount.CurrentTier"/>. Returns the same account row as
    /// <see cref="GetByUserIdAsync"/> without enrolling it in the change tracker.
    /// </summary>
    Task<LoyaltyAccount?> GetByUserIdTierOnlyAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Get-or-create — lazily creates the account on first access. The new
    /// account is added to the change tracker; the calling handler's
    /// UnitOfWork pipeline commits.
    /// </summary>
    Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken cancellationToken);
}
