using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// A cancellation the PLATFORM decides — fee-free, refunded in full, every party told — as one seam
/// for the two callers that make that decision: an admin cancelling one order and the company
/// wind-down cancelling every open one (ADR-0064 D2). The refund key follows the reason the caller
/// passes, so the admin path keeps <c>refund:{id}:cancel</c> and the sweep writes
/// <c>refund:{id}:admin</c> through one body.
/// </summary>
public interface IPlatformOrderCancellation
{
    Task<PlatformOrderCancellationResult> CancelAsync(
        Order order,
        string actorId,
        CancelledBy cancelledBy,
        string? reason,
        RefundReason refundReason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The refund leg alone, for a cancelled card order whose refund Stripe refused: the same key
    /// resolves to the pending row and Stripe replays once.
    /// </summary>
    Task<PlatformRefundOutcome> RefundAsync(
        Order order,
        string actorId,
        RefundReason refundReason,
        CancellationToken cancellationToken);
}

public sealed record PlatformOrderCancellationResult(decimal RefundAmount, PlatformRefundOutcome Refund);

public sealed record PlatformRefundOutcome(bool Attempted, bool Initiated, string? FailureMessage)
{
    public static readonly PlatformRefundOutcome NotAttempted = new(false, false, null);
    public static readonly PlatformRefundOutcome Issued = new(true, true, null);
    public static PlatformRefundOutcome Failed(string? message) => new(true, false, message);
}
