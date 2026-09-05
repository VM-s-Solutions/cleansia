using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Credit;

/// <summary>
/// One movement of a customer's credit balance. Append-only: nothing here is ever updated or
/// deleted, so the ledger and the balance can always be reconciled against each other.
/// </summary>
public class CreditTransaction : Auditable
{
    [Required]
    [MaxLength(26)]
    public string CreditAccountId { get; private set; } = default!;
    public CreditAccount? Account { get; private set; }

    /// <summary>
    /// Signed. Positive is credit issued to the customer, negative is credit spent.
    ///
    /// <para>A single signed column rather than a type discriminator plus a magnitude, so
    /// <c>SUM(Amount)</c> IS the balance and a reconciliation is one query with nothing to get the
    /// sign of wrong.</para>
    /// </summary>
    [Required]
    public decimal Amount { get; private set; }

    [Required]
    public CreditTransactionReason Reason { get; private set; }

    /// <summary>
    /// The order this movement belongs to, when there is one — the order it was spent on, or the
    /// order whose dispute produced it.
    /// </summary>
    [MaxLength(26)]
    public string? OrderId { get; private set; }

    [MaxLength(26)]
    public string? DisputeId { get; private set; }

    /// <summary>
    /// What makes a retry the same movement rather than a second one. NOT nullable, deliberately:
    /// a nullable key under a filtered unique index is a backstop that enforces nothing, and this is
    /// money. Every caller must name its own idempotency.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string IdempotencyKey { get; private set; } = default!;

    [MaxLength(500)]
    public string? Note { get; private set; }

    private CreditTransaction() { }

    public static CreditTransaction Create(
        string creditAccountId,
        decimal amount,
        CreditTransactionReason reason,
        string idempotencyKey,
        string createdBy,
        string? orderId = null,
        string? disputeId = null,
        string? note = null)
    {
        var transaction = new CreditTransaction
        {
            CreditAccountId = creditAccountId,
            Amount = amount,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            OrderId = orderId,
            DisputeId = disputeId,
            Note = note,
        };
        transaction.Created(createdBy, DateTimeOffset.UtcNow);
        return transaction;
    }
}
