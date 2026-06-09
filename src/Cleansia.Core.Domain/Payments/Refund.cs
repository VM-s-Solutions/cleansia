using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;

namespace Cleansia.Core.Domain.Payments;

public class Refund : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(50)]
    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    [MaxLength(50)]
    public string? ReceiptId { get; private set; }
    public OrderReceipt? Receipt { get; private set; }

    [MaxLength(50)]
    public string? DisputeId { get; private set; }
    public Dispute? Dispute { get; private set; }

    public decimal Amount { get; private set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = default!;

    [Required]
    [MaxLength(120)]
    public string RefundKey { get; private set; } = default!;

    [Required]
    public RefundReason Reason { get; private set; }

    [MaxLength(255)]
    public string? StripeRefundId { get; private set; }

    [Required]
    public RefundSource Source { get; private set; }

    [Required]
    public RefundStatus Status { get; private set; }

    public DateTimeOffset? ConfirmedOn { get; private set; }

    /// <summary>
    /// The admin's justification for refunding outside the soft refund window (ADR-0009 D1). Persisted
    /// as the audit trail of an out-of-window decision; null for an in-window refund (and for the
    /// chargeback/dispute paths, which never window-check).
    /// </summary>
    [MaxLength(500)]
    public string? WindowOverrideReason { get; private set; }

    private Refund() { }

    public static Refund Create(
        string orderId,
        string refundKey,
        decimal amount,
        string currency,
        RefundReason reason,
        RefundSource source,
        string? receiptId = null,
        string? disputeId = null,
        string? windowOverrideReason = null)
    {
        return new Refund
        {
            OrderId = orderId,
            RefundKey = refundKey,
            Amount = amount,
            Currency = currency,
            Reason = reason,
            Source = source,
            ReceiptId = receiptId,
            DisputeId = disputeId,
            WindowOverrideReason = windowOverrideReason,
            Status = RefundStatus.Pending
        };
    }

    /// <summary>
    /// Stamp the refund as confirmed by Stripe (confirm-then-record, ADR-0006 D7). Called only after
    /// the Stripe refund call returns, so a recorded <see cref="RefundStatus.Succeeded"/> row always
    /// means money actually moved.
    /// </summary>
    public Refund MarkSucceeded(string? stripeRefundId, DateTimeOffset confirmedOnUtc)
    {
        StripeRefundId = stripeRefundId;
        Status = RefundStatus.Succeeded;
        ConfirmedOn = confirmedOnUtc;
        return this;
    }
}
