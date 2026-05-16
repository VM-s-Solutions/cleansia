using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface ILoyaltyAccountRepository : IRepository<LoyaltyAccount, string>
{
    /// <summary>
    /// Returns the loyalty account for the given user, or null if none exists yet.
    /// </summary>
    Task<LoyaltyAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Get-or-create — lazily creates the account on first access. The new
    /// account is added to the change tracker; the calling handler's
    /// UnitOfWork pipeline commits.
    /// </summary>
    Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken cancellationToken);
}
