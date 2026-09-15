using Cleansia.Core.Domain.Credit;

namespace Cleansia.Core.Domain.Repositories;

public interface ICreditAccountRepository : IRepository<CreditAccount, string>
{
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
    /// <para>Currency-blind on purpose. The two questions this serves — "what does this customer hold"
    /// and "is anything owed" — are the two that must never be scoped to one currency.</para>
    /// </summary>
    Task<IReadOnlyList<CreditAccount>> GetAllForUserAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The customer's account, creating an empty one in <paramref name="currencyId"/> if they have
    /// never had credit. Shaped on <c>ILoyaltyAccountRepository.EnsureForUserAsync</c>: the caller is
    /// always about to write to it, so "no account" and "an account holding nothing" are the same
    /// thing and the distinction only ever produced a null check.
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
    Task<CreditAccount> EnsureForUserAsync(
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
    /// What the customer holds, in every currency — the currency-BLIND counterpart, for the two
    /// callers that must not scope to one: the customer's own credit screen, whose wire shape carries a
    /// single balance and its currency, and the GDPR erasure gate.
    ///
    /// <para>The gate is why this is not merely a convenience. Erasure is refused while the platform
    /// still owes money, and it used to ask that question through a single-account read — so a customer
    /// holding nothing in one currency and a positive balance in another could be erased while the
    /// platform still owed them.</para>
    /// </summary>
    Task<IReadOnlyList<CreditSpendable>> GetSpendablesForUserAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Put credit BACK on a balance because the order it settled was unwound. Returns false when the
    /// key has already been used, which is what makes a retried refund or a re-delivered webhook safe.
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
    /// <para>The same shape also makes it safe everywhere else: the money goes back even if the
    /// caller's own commit later fails, and a replay is a no-op rather than a 23505 that would take
    /// that commit down with it.</para>
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
