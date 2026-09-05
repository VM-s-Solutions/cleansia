using Cleansia.Core.Domain.Credit;

namespace Cleansia.Core.Domain.Repositories;

public interface ICreditAccountRepository : IRepository<CreditAccount, string>
{
    /// <summary>The customer's account, with its ledger, or null if they have never had credit.</summary>
    Task<CreditAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The customer's account, creating an empty one in <paramref name="currencyId"/> if they have
    /// never had credit. Shaped on <c>ILoyaltyAccountRepository.EnsureForUserAsync</c>: the caller is
    /// always about to write to it, so "no account" and "an account holding nothing" are the same
    /// thing and the distinction only ever produced a null check.
    ///
    /// <para>The currency is used ONLY when creating. An existing account keeps the currency it was
    /// opened in - a balance in one currency is not silently converted into another.</para>
    /// </summary>
    Task<CreditAccount> EnsureForUserAsync(
        string userId, string currencyId, CancellationToken cancellationToken);

    /// <summary>
    /// Balance, currency and account id only, untracked - the checkout read, which needs to know how
    /// much is spendable and in what currency, and nothing else. Returns null when the customer has no
    /// account, which is the common case and must NOT create one: a booking is not a reason to open a
    /// credit account.
    /// </summary>
    Task<CreditSpendable?> GetSpendableAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Take <paramref name="amount"/> from the balance, or take nothing.
    ///
    /// <para>Returns false when the balance is short. It is <b>the only</b> way credit is spent, and
    /// it exists because a balance cannot be arbitrated by a unique index the way a slot ordinal can:
    /// there is no key for two concurrent spends to collide on. The database decides, in one
    /// statement, whether the funds were there.</para>
    ///
    /// <para>It writes the ledger row ITSELF, in that same statement. The caller does not get the
    /// chance to forget: the decrement auto-commits on its own, so a split API would let a caller
    /// take the funds and then roll back without recording the movement, quietly breaking the one
    /// invariant this design has — <c>Balance == SUM(Transactions.Amount)</c>.</para>
    /// </summary>
    Task<bool> TryDebitAsync(
        string creditAccountId,
        decimal amount,
        CreditTransactionReason reason,
        string idempotencyKey,
        string actorId,
        CancellationToken cancellationToken,
        string? orderId = null,
        string? note = null);
}
