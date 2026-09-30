using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves the cancellation policy that applies to one order, so callers (the cancel paths, both
/// previews, the booking evidence) don't have to know about Plus or first bookings directly.
/// </summary>
public interface ICancellationPolicyResolver
{
    /// <summary>
    /// Resolve the policy for the given order: its customer's live Plus entitlement, and whether it is
    /// that customer's first booking. A guest order is judged on the second alone.
    /// </summary>
    Task<CancellationPolicy> ResolveForOrderAsync(Order order, CancellationToken cancellationToken);
}

/// <summary>
/// The cancellation rules that apply to one order at one moment. The oops window after booking
/// (<c>BookingPolicy.OopsWindowMinutesPlus</c> for an entitled Plus membership,
/// <c>BookingPolicy.OopsWindowMinutesFirstBooking</c> on a first booking) and, when a Plus plan sets
/// one, the free window before the cleaning move independently. The partial and last-minute thresholds
/// and rates never move.
/// </summary>
public record CancellationPolicy(
    int FreeCancellationHours,
    int PartialCancellationHours,
    decimal PartialCancellationFeeRate,
    decimal LastMinuteCancellationFeeRate,
    int OopsWindowMinutes,
    OopsWindowRule OopsWindowRule);

/// <summary>
/// Which rule set <see cref="CancellationPolicy.OopsWindowMinutes"/>. Persisted by name in the booking
/// and cancellation evidence; a Plus member's first booking records <see cref="Plus"/>.
/// </summary>
public enum OopsWindowRule
{
    Standard = 0,
    FirstBooking = 1,
    Plus = 2,
}
