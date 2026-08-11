using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What cancelling a given order right now costs. <c>CancelOrder</c> charges this and
/// <c>GetCancellationFeePreview</c> quotes it, and they call the same function rather than the same
/// rule — a customer told one number and charged another is the whole defect this type removes.
///
/// <para>The schedule itself stays in <see cref="BookingPolicy"/>; this adds only what the policy is
/// deliberately ignorant of — which order fields feed it, what "a cleaner is on the job" means, and
/// how the money is rounded.</para>
/// </summary>
internal sealed record CancellationAssessment(
    CancellationFeeTier Tier,
    decimal FeeRate,
    decimal FeeAmount,
    decimal RefundAmount,
    bool HasBeenAccepted);

/// <inheritdoc cref="CancellationAssessment"/>
internal static class CancellationAssessor
{
    /// <summary>
    /// Not yet derived from the customer's history; a shared constant so the quote and the charge
    /// cannot land on different sides of the wider first-time oops window.
    /// </summary>
    private const bool IsFirstTimeCustomer = false;

    /// <summary>
    /// The <c>BusinessErrorMessage</c> key for why this order cannot be cancelled at all, or
    /// <see langword="null"/> when it can. Each caller wraps it in an <c>Error</c> against its own
    /// <c>OrderId</c> field.
    /// </summary>
    public static string? BlockedReason(Order order) => order.CurrentStatus switch
    {
        OrderStatus.Cancelled => BusinessErrorMessage.OrderAlreadyCancelled,
        OrderStatus.Completed => BusinessErrorMessage.OrderAlreadyCompleted,
        OrderStatus.InProgress => BusinessErrorMessage.OrderInProgressCannotCancel,
        _ => null,
    };

    public static CancellationAssessment Assess(Order order, CancellationPolicy policy, DateTime nowUtc)
    {
        // An assignment row, NOT an OrderStatus.Confirmed track. Confirmed is overloaded — the payment
        // webhook, cash auto-confirm and the admin override all write it with no cleaner involved — and
        // it is also SILENT in the case that matters most: TakeOrder assigns unconditionally but appends
        // its Confirmed track only from New/Pending, so a cleaner taking an already-Confirmed order
        // leaves no new track. The row is the only durable evidence a cleaner was pulled onto the job,
        // which is what the fee prices.
        var hasBeenAccepted = order.AssignedEmployees.Count > 0;

        var tier = BookingPolicy.ClassifyCancellation(
            order.CleaningDateTime,
            order.CreatedOn.UtcDateTime,
            nowUtc,
            IsFirstTimeCustomer,
            hasBeenAccepted,
            freeCancellationHoursOverride: policy.FreeCancellationHours);
        var feeRate = BookingPolicy.CancellationFeeRateFor(tier);

        // Round to the currency's 2 dp at the source (away-from-zero): the Refund row persists
        // numeric(18,2) (rounds) while Stripe truncates (long)(amount*100), so an unrounded value can
        // make the ledger and Stripe diverge by a cent and skew the Refunded/PartiallyRefunded
        // comparison. Rounding once here makes every downstream reader agree.
        var refundAmount = Math.Round(order.TotalPrice * (1m - feeRate), 2, MidpointRounding.AwayFromZero);

        // The fee is the residual, never a second rounding of the same half-cent: rounding both legs
        // independently makes them sum to a cent more than the total the customer is looking at.
        return new CancellationAssessment(
            Tier: tier,
            FeeRate: feeRate,
            FeeAmount: order.TotalPrice - refundAmount,
            RefundAmount: refundAmount,
            HasBeenAccepted: hasBeenAccepted);
    }
}
