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
            // Quantize to 2 dp at the money seam (away-from-zero) so EVERY caller
            // — cancel, dispute resolution, admin partial refund, re-drive — is
            // covered: the row persists numeric(18,2) (rounds) while Stripe
            // truncates (long)(amount*100), and an unrounded amount makes the two
            // diverge by a cent and skews the Refunded/PartiallyRefunded check
            // (T-0355). Bounded by the caller's ceiling clamp, so this never
            // rounds above what's refundable.
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
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

    /// <summary>
    /// Lower this refund's amount to at most <paramref name="maxAmount"/> before a re-drive, so a stale
    /// frozen amount can never exceed the live refundable ceiling (the cross-key over-refund guard,
    /// T-0354). Only ever decreases the amount, and only meaningful while the refund has not yet
    /// Succeeded — the re-drive caller only reaches this for a Pending/Failed row.
    /// </summary>
    public Refund ClampAmountTo(decimal maxAmount)
    {
        // Same 2 dp seam-rounding as Create so a re-drive can never persist a
        // sub-cent amount (the ceiling is already 2 dp, so this is defensive).
        var ceiling = Math.Round(maxAmount, 2, MidpointRounding.AwayFromZero);
        if (ceiling < Amount)
        {
            Amount = ceiling;
        }

        return this;
    }
}
