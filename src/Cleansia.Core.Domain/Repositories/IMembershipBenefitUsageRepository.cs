using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// ADR-0035 D3 — the reserved-slot ledger's data access.
///
/// <para><b>The quota key is <c>(TenantId, UserId, BenefitKind, PeriodKey)</c> and nothing else</b>
/// (AM-19). <c>UserMembershipId</c> is written once, at reservation, and must never appear in a
/// <c>WHERE</c>, <c>GROUP BY</c>, <c>HAVING</c> or join predicate here — a quota scoped to the enrolment
/// row would reset on re-subscribe and would not carry across a mid-month plan switch, which the owner
/// ruled it must.</para>
///
/// <para>There is no global <c>IsActive</c> query filter in this codebase, so every read below filters it
/// by hand.</para>
/// </summary>
public interface IMembershipBenefitUsageRepository : IRepository<MembershipBenefitUsage, string>
{
    /// <summary>
    /// Atomically claim the smallest free slot ordinal in <paramref name="periodKey"/> for this user and
    /// benefit, and return the reserved row (detached — the insert is raw SQL, not change-tracked).
    /// Returns <c>null</c> when no slot is available: the quota is full, or this caller lost a race. A
    /// <b>result</b>, never an exception — the caller simply does not waive.
    ///
    /// <para><b>Declared unit-of-work exception (ADR-0035 D3):</b> this statement issues SQL immediately
    /// and auto-commits <i>outside</i> the MediatR pipeline. Mode A (claim before the price is waived)
    /// requires it: a price may never be waived without a committed slot. The row references no
    /// not-yet-committed row — <c>OrderId</c> is deliberately nullable and stamped later by a
    /// change-tracked update — so the FK hazard ADR-0038 closes does not exist on this path.</para>
    /// </summary>
    Task<MembershipBenefitUsage?> TryReserveSlotAsync(
        string userId,
        MembershipBenefitKind benefitKind,
        string periodKey,
        string userMembershipId,
        int maxPerPeriod,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Live slots consumed by this user for this benefit in this period. The read that decides the
    /// <i>quoted</i> price and the "N left" display — non-authoritative by design; the reservation
    /// statement is what arbitrates a concurrent claim.
    /// </summary>
    Task<int> CountLiveInPeriodAsync(
        string userId,
        MembershipBenefitKind benefitKind,
        string periodKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// The live slot attached to <paramref name="orderId"/>, tracked, or <c>null</c>. Drives the release
    /// hooks and the cancel-confirmation disclosure.
    /// </summary>
    Task<MembershipBenefitUsage?> GetLiveByOrderIdAsync(
        string orderId,
        MembershipBenefitKind benefitKind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Load a reserved row by id for the change-tracked order stamp. The reservation returns a detached
    /// object, so the attach has to bring it into the caller's unit of work.
    /// </summary>
    Task<MembershipBenefitUsage?> GetTrackedByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Release every live reservation that never got an order and was taken before
    /// <paramref name="cutoffUtc"/> — a credit spent on a booking whose unit of work never committed.
    /// Returns the number released. Tenant-ignoring on purpose: the sweep runs with no JWT.
    /// <para><c>CleanupStalePendingOrders</c> structurally cannot do this — it queries <c>Orders</c>, and
    /// an orphan is by definition a row whose order never existed (AM-7).</para>
    /// </summary>
    Task<int> ReleaseOrphanedReservationsAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
