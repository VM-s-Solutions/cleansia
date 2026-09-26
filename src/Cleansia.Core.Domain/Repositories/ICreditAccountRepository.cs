using Cleansia.Core.Domain.Credit;

namespace Cleansia.Core.Domain.Repositories;

public interface ICreditAccountRepository : IRepository<CreditAccount, string>
{
    /// <summary>
    /// Serialize erasure and credit mutations for this owner until the unit of work commits or rolls
    /// back, and re-read any of the owner's accounts this unit of work already tracks unchanged.
    /// </summary>
    Task LockForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// EVERY account this customer holds, with their ledgers, newest balance first. Empty if they have
    /// never had credit.
    ///
    /// <para>Replaces <c>GetByUserIdAsync</c>, and the rename is the point. "The customer's account"
    /// was a sentence that stopped being true the moment a balance became denominated, and both of its
    /// callers believed it: the admin ledger screen rendered an arbitrary one, and the admin discharge
    /// emptied an arbitrary one. A method that returns a LIST cannot be misread that way, and the two
    /// callers now each say what they do with the set.</para>
    ///
    /// <para>Currency-blind on purpose. "What does this customer hold" must never be scoped to one
    /// currency, and neither must the erasure's forfeiture of it.</para>
    /// </summary>
    Task<IReadOnlyList<CreditAccount>> GetAllForUserAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The customer's account, or null for an erased or missing user. Creates an empty one in
    /// <paramref name="currencyId"/> if they have never had credit. Shaped on
    /// <c>ILoyaltyAccountRepository.EnsureForUserAsync</c>: the caller is always about to write to it,
    /// so "no account" and "an account holding nothing" are the same thing. Holds the owner and account
    /// locks until the unit of work commits or rolls back.
    ///
    /// <para><b>The currency is part of the LOOKUP, not only of the creation.</b> That sentence used
    /// to read "the currency is used ONLY when creating", and the query behind it matched on
    /// <c>UserId</c> alone — so a caller asking for a customer's EUR account was handed their CZK one
    /// and added a EUR number to it. The doc described the intent and the code did the opposite, which
    /// is why nothing ever failed: the balance was wrong, not the control flow.</para>
    ///
    /// <para>An existing account still keeps the currency it was opened in, because a lookup for a
    /// different currency no longer finds it — it opens that currency's own account instead.</para>
    /// </summary>
    Task<CreditAccount?> EnsureForUserAsync(
        string userId, string currencyId, CancellationToken cancellationToken);

    /// <summary>
    /// Balance, currency and account id only, untracked - the checkout read, which needs to know how
    /// much is spendable and in what currency, and nothing else. Returns null when the customer has no
    /// account, which is the common case and must NOT create one: a booking is not a reason to open a
    /// credit account.
    ///
    /// <para>KEYED ON CURRENCY, because credit is only spendable against an order in the same
    /// currency. The callers already knew that — all three compared
    /// <c>spendable.CurrencyId != order.CurrencyId</c> and took zero — but they were comparing against
    /// whichever account an unordered <c>FirstOrDefault</c> returned, so with two accounts a customer
    /// with a matching balance could be told they had none.</para>
    /// </summary>
    Task<CreditSpendable?> GetSpendableAsync(
        string userId, string currencyId, CancellationToken cancellationToken);

    /// <summary>
    /// What the customer holds, in every currency, without loading the ledger.
    /// </summary>
    Task<IReadOnlyList<CreditSpendable>> GetSpendablesForUserAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Put credit BACK on a balance because the order it settled was unwound. Returns false when the
    /// key has already been used or the owner has been erased; false never claims money was returned.
    ///
    /// <para>This is the writer of <see cref="CreditTransactionReason.OrderPaymentReturned"/>, and
    /// without it credit is a one-way door: an order cancelled or refunded after credit settled part
    /// of it would take the customer's money and give back only the card half — the card cannot refund
    /// what the card never took.</para>
    ///
    /// <para><b>Its own statement, like the debit — never the tracked graph.</b> Two callers need it
    /// that way and one of them cannot work otherwise: <c>CreateOrder</c> compensates a failed Stripe
    /// dispatch by returning the credit it just took, on a request that is about to return a FAILURE
    /// and therefore will never be committed by the pipeline. A tracked <c>Issue</c> there would be
    /// discarded with the rest of the unit of work — and flushing it explicitly would persist the
    /// half-built order the factory has already added to the same context.</para>
    ///
    /// <para>The return commits on its own unless a transaction is already open on this context — the
    /// credit lock of this unit of work (<see cref="LockForUserAsync"/>), an explicit database
    /// transaction or an ambient <c>TransactionScope</c>; then it commits or rolls back with it. A replay
    /// is a no-op rather than a 23505 that would take the caller's commit down with it.</para>
    /// </summary>
    Task<bool> TryReturnAsync(
        string userId,
        string currencyId,
        decimal amount,
        string idempotencyKey,
        string actorId,
        CancellationToken cancellationToken,
        string? orderId = null,
        string? note = null);

    /// <summary>
    /// Accounts whose balance has expired, across every tenant, oldest first.
    ///
    /// <para>Tenant-ignoring on purpose: the sweep runs with no JWT and must see them all. The caller
    /// groups by TenantId, sets the override and commits INSIDE the loop — a deferred commit stamps
    /// every ledger row with whichever tenant was processed last.</para>
    ///
    /// <para><paramref name="take"/> bounds one pass. An expiry sweep that has never run has an
    /// unbounded backlog, and loading all of it into one transaction is how a nightly job turns into
    /// an outage.</para>
    /// </summary>
    Task<IReadOnlyList<CreditAccount>> GetExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken);

    /// <summary>
    /// How much credit has already gone back for one order. The credit leg's counterpart to
    /// <c>IRefundRepository.GetSucceededRefundTotalForOrderAsync</c>, and it exists for the same
    /// reason: a second refund on the same order has to know what the first one already unwound, or
    /// the two legs of one refund end up computed against different denominators.
    /// </summary>
    Task<decimal> GetReturnedTotalForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// The batch form of <see cref="GetReturnedTotalForOrderAsync"/>: Σ
    /// <see cref="CreditTransactionReason.OrderPaymentReturned"/> per order, keyed by order id; an order
    /// with none is absent. The credit leg of every refund on those orders, for the revenue report.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetReturnedTotalsByOrderAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);

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
