using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.AppServices.Shared.DTOs.Enums;

/// <summary>
/// Which arm of the cancellation ladder a given order lands on right now — the reason behind the fee,
/// not a second way to compute it. Produced by <c>BookingPolicy.ClassifyCancellation</c>, priced by
/// <c>BookingPolicy.CancellationFeeRateFor</c>, and shipped on the cancellation preview so a client
/// renders the tier rather than rebuilding the schedule against numbers it cannot know (the member's
/// own free window and the acceptance predicate are both server-side facts).
///
/// <para>Values are frozen: they cross the wire as integers, so a member may be added but never
/// reordered or renumbered.</para>
/// </summary>
[SwaggerEnumAsInt]
public enum CancellationFeeTier
{
    /// <summary>No cleaner has been pulled onto the job, so there is no one's time to compensate.</summary>
    FreeNotAccepted = 0,

    /// <summary>Cancelled within the "oops window" of booking — an accidental tap, not a cancellation.</summary>
    FreeOopsWindow = 1,

    /// <summary>Cancelled early enough to fall outside the caller's free-cancellation window.</summary>
    FreeOutsideWindow = 2,

    /// <summary>Inside the free window but not yet last-minute.</summary>
    Partial = 3,

    /// <summary>Inside the last-minute threshold of the cleaning start.</summary>
    LastMinute = 4,
}
