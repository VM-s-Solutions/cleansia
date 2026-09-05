using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Credit;

/// <summary>
/// A customer's credit balance — money Cleansia owes them, usually because a clean went wrong.
///
/// <para><b>Shaped on <see cref="Loyalty.LoyaltyAccount"/>, with three things deliberately NOT
/// copied.</b> Loyalty is a per-user account with an append-only transaction list and
/// idempotency-keyed mutators, which is exactly right here. But:</para>
/// <list type="number">
///   <item><b>No silent clamp.</b> <c>LoyaltyAccount.RevokePoints</c> does
///   <c>Math.Max(0, LifetimePoints - points)</c>. Harmless on a tier counter; on money it is a
///   silent partial-spend — a customer with 200 spends 500, gets 500 off, and the ledger floors at
///   zero. Spending here goes through the repository's conditional UPDATE, which either takes the
///   whole amount or takes nothing.</item>
///   <item><b>A real concurrency arbiter.</b> Loyalty's worst race is a double-earn; this one's is
///   the same money spent twice. A balance is an AGGREGATE, so there is no key for two concurrent
///   spends to collide on and no unique index can arbitrate it — the arbiter is a conditional
///   <c>UPDATE … WHERE Balance &gt;= amount</c>, which the database evaluates once.</item>
///   <item><b>A currency.</b> Points are dimensionless; credit is an amount in a currency, and the
///   platform scales every price by an exchange rate.</item>
/// </list>
///
/// <para><b>Balance is stored, not summed.</b> It is the value the conditional UPDATE tests and
/// decrements in one statement, which is what makes the spend atomic — a <c>SUM</c> over the ledger
/// could not be. The ledger stays the audit trail, and
/// <c>Balance == SUM(Transactions.Amount)</c> is an invariant a test asserts against real
/// Postgres.</para>
/// </summary>
public class CreditAccount : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    /// <summary>
    /// Never negative. Nothing in the domain subtracts from it — the repository's conditional UPDATE
    /// is the only writer of a decrease, and it refuses rather than going below zero.
    /// </summary>
    [Required]
    public decimal Balance { get; private set; }

    /// <summary>
    /// The currency the balance is held in. One per account: a customer who somehow earned credit in
    /// two currencies needs a policy decision, not a silent conversion at spend time.
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string CurrencyId { get; private set; } = default!;

    /// <summary>
    /// How long credit lives, counted from the customer's LAST movement — a grant, a spend or a
    /// return all reset it.
    ///
    /// <para>Owner ruling 2026-09-05: credit EXPIRES rather than being paid out. Cleansia does not do
    /// Stripe payouts and standing one up for the handful of customers who would ever ask is not worth
    /// it; a published expiry is the honest alternative to a debt that sits on the books forever.
    /// Twelve months is long enough that nobody feels a compensation evaporated on them.</para>
    ///
    /// <para>It lives HERE and not in <c>BookingPolicy</c> beside the other credit numbers, for two
    /// reasons. Domain cannot reference AppServices, so the entity could not read it there. And it is
    /// genuinely a property of the account: the share of an order credit may settle is a booking rule,
    /// how long a balance lives is not.</para>
    /// </summary>
    public const int ExpiryMonths = 12;

    /// <summary>
    /// When this balance expires if nothing else happens to it. Null on an account that has never
    /// moved, and on one that has just been emptied — an empty account has no clock to run.
    ///
    /// <para><b>Per ACCOUNT, not per grant.</b> Tracking each grant's own expiry means spending has to
    /// draw down oldest-first and the balance stops being one number: real machinery, for a difference
    /// that only appears when a customer holds two grants a year apart. Resetting the clock on every
    /// movement is simpler, strictly more generous, and explicable in the one sentence the customer is
    /// actually shown.</para>
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; private set; }

    private readonly List<CreditTransaction> _transactions = [];
    public IReadOnlyCollection<CreditTransaction> Transactions => _transactions.AsReadOnly();

    private CreditAccount() { }

    public static CreditAccount Create(string userId, string currencyId, string createdBy)
    {
        var account = new CreditAccount
        {
            UserId = userId,
            CurrencyId = currencyId,
            Balance = 0m,
        };
        account.Created(createdBy, DateTimeOffset.UtcNow);
        return account;
    }

    /// <summary>
    /// Add credit and record why.
    ///
    /// <para>Increases are safe to do in the tracked graph: two concurrent grants both increase the
    /// balance and both are correct, and a repeated grant is stopped by the ledger's unique
    /// <c>IdempotencyKey</c> rather than by arithmetic. Spending is the direction that needs the
    /// database to arbitrate, and it does not live here.</para>
    /// </summary>
    public CreditTransaction Issue(
        decimal amount,
        CreditTransactionReason reason,
        string idempotencyKey,
        string issuedBy,
        string? orderId = null,
        string? disputeId = null,
        string? note = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        var transaction = CreditTransaction.Create(
            Id, amount, reason, idempotencyKey, issuedBy, orderId, disputeId, note);
        _transactions.Add(transaction);
        Balance += amount;
        Touch(issuedBy, DateTimeOffset.UtcNow);
        return transaction;
    }

    /// <summary>
    /// Record a movement: stamp the audit fields and push the expiry out.
    ///
    /// <para>Every path that changes the balance comes through here or through the repository's raw
    /// statements, which set the same date in SQL. That is the whole invariant — a balance that moved
    /// today must not expire on a clock started a year ago.</para>
    /// </summary>
    private void Touch(string actorId, DateTimeOffset nowUtc)
    {
        ExpiresOn = nowUtc.AddMonths(ExpiryMonths);
        Updated(actorId, nowUtc);
    }

    /// <summary>
    /// Take the whole balance, because it expired or because an admin discharged it on request.
    ///
    /// <para>Answers with the amount removed, zero on an already-empty account. The LEDGER row is the
    /// caller's job: the sweep writes balance and ledger in one statement the way every other balance
    /// change does, and this exists for the tracked-graph path an admin action uses.</para>
    /// </summary>
    public decimal Drain(string actorId, DateTimeOffset nowUtc)
    {
        var taken = Balance;
        if (taken <= 0m)
        {
            return 0m;
        }

        Balance = 0m;
        // NOT Touch: an emptied account has no live balance and no clock. The next grant starts a
        // fresh twelve months.
        ExpiresOn = null;
        Updated(actorId, nowUtc);
        return taken;
    }

    /// <summary>
    /// Write the ledger row for a balance that was just drained, so
    /// <c>Balance == SUM(Transactions.Amount)</c> survives the expiry.
    ///
    /// <para>Separate from <see cref="Drain"/> because the caller decides what the movement was FOR:
    /// the sweep says it expired, an admin discharging a balance says who asked and why. The amount is
    /// negated here so no caller can record an expiry that adds money.</para>
    /// </summary>
    public CreditTransaction RecordExpiry(
        decimal amount,
        string idempotencyKey,
        string actorId,
        string? note = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        var transaction = CreditTransaction.Create(
            Id, -amount, CreditTransactionReason.Expired, idempotencyKey, actorId, note: note);
        _transactions.Add(transaction);
        return transaction;
    }
}
