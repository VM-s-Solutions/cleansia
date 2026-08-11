using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// ADR-0035 D3.2 — the ONE consuming seam: resolve → reserve → attach → release. One collaborator on
/// <c>CreateOrder.Handler</c> instead of three, and one grep target for a reviewer.
/// </summary>
public interface IExpressWaiverConsumer
{
    /// <inheritdoc cref="IExpressWaiverResolver.ResolveForUserAsync"/>
    Task<ExpressWaiver> ResolveAsync(
        string? userId,
        DateTime? cleaningUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claim a slot for an already-resolved waiver. <c>null</c> means the slot is gone — the quota filled
    /// between the resolve and here, or this caller lost a race. The caller must NOT then waive: honoring
    /// a waived price without a committed slot is exactly the soft cap this design rejects, and every
    /// concurrent request would pass its own resolve.
    /// </summary>
    Task<MembershipBenefitUsage?> TryReserveAsync(
        ExpressWaiver waiver,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stamp the order id onto a reserved slot as a CHANGE-TRACKED update, so it rides the caller's unit
    /// of work and lands in the same <c>SaveChangesAsync</c> as the <c>Orders</c> INSERT that EF orders
    /// ahead of it. Out-of-band here would fire against a principal row that does not exist yet — the
    /// handler returns before the pipeline commits — and raise <c>23503</c> on a paid booking.
    /// </summary>
    Task AttachOrderAsync(
        MembershipBenefitUsage reservation,
        string orderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Release the live slot attached to this order, if any, so the ordinal returns to the pool through
    /// the filtered unique index. Returns true when a slot was released.
    /// </summary>
    Task<bool> ReleaseForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether cancelling this order right now would forfeit a live express waiver — a live slot attached
    /// to the order AND a release rule that would not fire for a customer-initiated cancel. The customer
    /// must be told before they confirm, because the cases where the forfeiture is invisible (inside the
    /// 15-minute oops window, or on any cash order) are exactly the ones where the cancellation fee is 0.
    /// </summary>
    Task<bool> WouldForfeitOnCustomerCancelAsync(
        string orderId,
        bool hasAssignedEmployee,
        CancellationToken cancellationToken);
}
