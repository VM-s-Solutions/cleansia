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
        Updated(issuedBy, DateTimeOffset.UtcNow);
        return transaction;
    }
}
