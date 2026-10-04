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
    /// Account lookup across operators for a user read from an authorized order.
    /// Admin lookups by arbitrary user id must use the tenant-filtered method.
    /// </summary>
    Task<LoyaltyAccount?> GetByUserIdIgnoringTenantAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Get-or-create, for a writer only: a reader that misses takes no lock and adds nothing. The new
    /// account is added to the change tracker; the calling handler's
    /// UnitOfWork pipeline commits. The account is ONE per user across the holding
    /// (<c>IX_LoyaltyAccounts_UserId</c>) and belongs to the user's own company: the read is past the
    /// tenant filter and a new row is stamped with the user's tenant, never with the ambient one, so a
    /// grant under another company's claim finds the same row every time instead of colliding on it.
    /// A miss takes <see cref="LockForUserAsync"/> and reads again, so two first grants at once share one
    /// account.
    /// </summary>
    Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Serializes this customer's loyalty writes until the unit of work commits or rolls back. It is the
    /// customer's credit owner lock, so a unit that already holds that lock holds this one.
    /// </summary>
    Task LockForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="afterSave"/> once this unit of work's write to <paramref name="account"/> is saved,
    /// with the tier the row held just before it, which is a concurrent writer's when the write was replayed
    /// onto its commit, or null when the save creates the row. It runs inside the save's transaction, so what
    /// it stages commits with the write or not at all.
    /// </summary>
    void AfterSave(LoyaltyAccount account, Func<LoyaltyTier?, CancellationToken, Task> afterSave);
}
