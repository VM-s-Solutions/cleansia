using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// Business rules for admin-issued partial refunds (ADR-0009). Sibling to
/// <c>BookingPolicy</c>: pure, unit-testable rule constants + methods, kept out of the
/// <c>IRefundService</c> seam (the seam enforces only the refundable ceiling + idempotency).
/// </summary>
public static class RefundPolicy
{
    /// <summary>
    /// ADR-0009 D1: the SOFT refund window, measured from <c>Order.CompletedAt</c>. Inside it an admin
    /// refund needs no justification; once closed the admin may still refund but MUST persist a non-empty
    /// override reason.
    /// </summary>
    public const int RefundWindowDays = 14;

    /// <summary>
    /// ADR-0009 D1. True only when the order has a completion timestamp AND now is within
    /// <see cref="RefundWindowDays"/> of it. A null <paramref name="completedAtUtc"/> (order never
    /// completed) is closed-by-default — there is no anchor to measure the window from.
    /// </summary>
    public static bool IsWithinWindow(DateTime? completedAtUtc, DateTime nowUtc)
    {
        if (completedAtUtc is null)
        {
            return false;
        }

        return (nowUtc - completedAtUtc.Value).TotalDays <= RefundWindowDays;
    }

    /// <summary>
    /// ADR-0009 D1 — the window gates only admin app-refunds. A <see cref="RefundSource.Chargeback"/>
    /// (bank-initiated, recorded from a Stripe webhook) is exempt: the bank already moved the money, so
    /// there is no admin decision to window-check.
    /// </summary>
    public static bool RequiresWindowCheck(RefundSource source) => source != RefundSource.Chargeback;

    /// <summary>
    /// ADR-0009 D3 — the Stripe-fee bearer. The platform absorbs the non-refundable Stripe fee when the
    /// fault is the platform's or service's (<see cref="RefundReason.ServiceNotRendered"/>,
    /// <see cref="RefundReason.DisputeResolution"/>); on a pure goodwill
    /// <see cref="RefundReason.AdminDiscretion"/> the fee is deducted from the refund. The cancellation
    /// fee path (<see cref="RefundReason.CustomerCancellation"/>) is unaffected — that is BookingPolicy's,
    /// not this rule's, so it is never treated as platform-absorbed here.
    /// </summary>
    public static bool PlatformAbsorbsStripeFee(RefundReason reason) =>
        reason is RefundReason.ServiceNotRendered or RefundReason.DisputeResolution;
}
