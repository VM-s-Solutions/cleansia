using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Payments;

/// <summary>
/// Money a customer owes the company on one order beyond what the order collected (owner ruling
/// 2026-09-28, decision 17): a late cancellation fee on a cash booking, a lockout fee, unpaid cash or an
/// approved top-up. While one is open the customer books no cash (decision 18), and an administrator may
/// write it off. <see cref="Attempts"/> counts the charges tried on the saved card. It is paid through
/// the customer's pay link or an off-session charge, and its payment earns a fee receipt of its own; the
/// order's sale, its charge surface and its refunds are never touched by it.
/// </summary>
public class Receivable : TenantAuditable
{
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    [MaxLength(26)]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    public ReceivableKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public ReceivableStatus Status { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>The Stripe PaymentIntent that paid it, through the pay link or an off-session charge.</summary>
    [MaxLength(255)]
    public string? StripePaymentIntentId { get; private set; }

    public DateTimeOffset? PaidOn { get; private set; }

    public DateTimeOffset? WrittenOffOn { get; private set; }

    [MaxLength(26)]
    public string? WrittenOffByUserId { get; private set; }

    [MaxLength(500)]
    public string? WriteOffNote { get; private set; }

    private Receivable()
    {
    }

    public bool IsOpen => Status == ReceivableStatus.Open;

    public bool IsPaid => Status == ReceivableStatus.Paid;

    public static Receivable ForCashCancellationFee(Order order, decimal fee) =>
        new()
        {
            OrderId = order.Id,
            UserId = order.UserId
                ?? throw new InvalidOperationException($"Order {order.Id} has no customer account to owe a cancellation fee."),
            CurrencyId = order.CurrencyId,
            Kind = ReceivableKind.CashCancellationFee,
            Amount = fee,
            Status = ReceivableStatus.Open,
        };

    public void RecordChargeAttempt() => Attempts++;

    /// <summary>
    /// Money arrived for it. A receivable written off and then paid anyway is paid: the money is the
    /// company's, and it earns a receipt like any other.
    /// </summary>
    public void MarkPaid(string? stripePaymentIntentId, DateTimeOffset paidOn)
    {
        Status = ReceivableStatus.Paid;
        StripePaymentIntentId = stripePaymentIntentId;
        PaidOn = paidOn;
    }

    public void WriteOff(string actorUserId, string note, DateTimeOffset writtenOffOn)
    {
        Status = ReceivableStatus.WrittenOff;
        WrittenOffOn = writtenOffOn;
        WrittenOffByUserId = actorUserId;
        WriteOffNote = note;
    }
}
