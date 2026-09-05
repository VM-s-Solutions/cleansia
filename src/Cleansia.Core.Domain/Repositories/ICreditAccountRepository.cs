using Cleansia.Core.Domain.Credit;

namespace Cleansia.Core.Domain.Repositories;

public interface ICreditAccountRepository : IRepository<CreditAccount, string>
{
    /// <summary>The customer's account, with its ledger, or null if they have never had credit.</summary>
    Task<CreditAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Take <paramref name="amount"/> from the balance, or take nothing.
    ///
    /// <para>Returns false when the balance is short. It is <b>the only</b> way credit is spent, and
    /// it exists because a balance cannot be arbitrated by a unique index the way a slot ordinal can:
    /// there is no key for two concurrent spends to collide on. The database decides, in one
    /// statement, whether the funds were there.</para>
    /// </summary>
    Task<bool> TryDebitAsync(string creditAccountId, decimal amount, CancellationToken cancellationToken);
}
