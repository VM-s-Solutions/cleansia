namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Central place for all booking-time / cancellation business rules.
/// Keep mobile, web, and backend in sync by referencing these numbers.
///
/// Research summary (April 2026):
///  - Industry-standard lead time for scheduled home services is 4h, with ~2h express tier.
///  - Industry-standard cancellation window is 24h free / 4h 50% / 0h 100%.
///  - Matches Handy, Helpling, Angi.
/// </summary>
public static class BookingPolicy
{
    /// <summary>
    /// Minimum hours between booking creation and cleaning start for STANDARD bookings.
    /// Bookings between this threshold and <see cref="ExpressLeadTimeHours"/> require express surcharge.
    /// </summary>
    public const int StandardLeadTimeHours = 4;

    /// <summary>
    /// Minimum hours between booking creation and cleaning start. Below this, booking is rejected.
    /// Between this and <see cref="StandardLeadTimeHours"/> the express surcharge applies.
    /// </summary>
    public const int ExpressLeadTimeHours = 2;

    /// <summary>
    /// Surcharge applied to the base price for express bookings (2–4h lead time).
    /// 0.20 = +20%.
    /// </summary>
    public const decimal ExpressSurchargeRate = 0.20m;

    /// <summary>
    /// Minutes in each customer-facing booking window (display only; internal scheduling grid stays 30-min).
    /// </summary>
    public const int WindowDurationMinutes = 60;

    /// <summary>
    /// Earliest and latest hour (inclusive) for bookable windows. 08:00–20:00 → 12 one-hour windows.
    /// </summary>
    public const int FirstWindowHour = 8;
    public const int LastWindowHour = 20;

    /// <summary>Cancellations earlier than this many hours before the cleaning are free.</summary>
    public const int FreeCancellationHours = 24;

    /// <summary>Cancellations between <see cref="PartialCancellationHours"/> and <see cref="FreeCancellationHours"/> pay this fraction.</summary>
    public const decimal PartialCancellationFeeRate = 0.25m;

    /// <summary>Cancellations within <see cref="PartialCancellationHours"/> of the cleaning start pay this fraction.</summary>
    public const decimal LastMinuteCancellationFeeRate = 0.50m;

    /// <summary>Cancellations between this threshold and <see cref="FreeCancellationHours"/> incur partial fee; below this, last-minute fee.</summary>
    public const int PartialCancellationHours = 4;

    /// <summary>
    /// "Oops window" — free cancellation within N minutes of booking regardless of execution time.
    /// Protects against accidental taps.
    /// </summary>
    public const int OopsWindowMinutesStandard = 15;

    /// <summary>"Oops window" for first-time customers. More lenient to build trust.</summary>
    public const int OopsWindowMinutesFirstTime = 60;

    /// <summary>Refund + credit issued when cleaner cancels or no-shows.</summary>
    public const decimal NoShowCreditCzk = 500m;

    /// <summary>True if the given start time requires express surcharge (2–4h lead).</summary>
    public static bool RequiresExpressSurcharge(DateTime cleaningUtc, DateTime nowUtc)
    {
        var leadHours = (cleaningUtc - nowUtc).TotalHours;
        return leadHours >= ExpressLeadTimeHours && leadHours < StandardLeadTimeHours;
    }

    /// <summary>True if the given cleaning start time is too soon to accept any booking.</summary>
    public static bool IsBelowMinimumLeadTime(DateTime cleaningUtc, DateTime nowUtc)
    {
        return (cleaningUtc - nowUtc).TotalHours < ExpressLeadTimeHours;
    }

    /// <summary>
    /// Compute the cancellation fee rate (0.0–1.0) for a given cancellation time.
    ///
    /// Acceptance-aware: if no cleaner has accepted the order yet, cancellation
    /// is always free regardless of timing. Once accepted, the standard tiered
    /// policy applies (free 24+ h before, 25% 4–24 h before, 50% under 4 h).
    /// </summary>
    /// <param name="cleaningUtc">Order's scheduled start time.</param>
    /// <param name="bookingCreatedUtc">When the order was created.</param>
    /// <param name="cancelUtc">Current time / proposed cancel time.</param>
    /// <param name="isFirstTimeCustomer">Whether the customer has 0 prior completed orders.</param>
    /// <param name="hasBeenAccepted">True if a cleaner has accepted the order (i.e. an OrderStatusHistory entry of Confirmed exists).</param>
    /// <param name="freeCancellationHoursOverride">
    /// When set, replaces <see cref="FreeCancellationHours"/>. Used by Plus
    /// members who get a more generous free-cancel window. The partial-fee
    /// threshold and rates stay the same — Plus only widens the free window.
    /// </param>
    /// <returns>Fee rate: 0.0 = free, 0.25 = quarter charge, 0.5 = half charge.</returns>
    public static decimal CalculateCancellationFeeRate(
        DateTime cleaningUtc,
        DateTime bookingCreatedUtc,
        DateTime cancelUtc,
        bool isFirstTimeCustomer,
        bool hasBeenAccepted,
        int? freeCancellationHoursOverride = null)
    {
        // No cleaner has taken the order yet — always free.
        if (!hasBeenAccepted)
        {
            return 0m;
        }

        // "Oops window" — free cancellation shortly after booking.
        var oopsMinutes = isFirstTimeCustomer ? OopsWindowMinutesFirstTime : OopsWindowMinutesStandard;
        if ((cancelUtc - bookingCreatedUtc).TotalMinutes <= oopsMinutes)
        {
            return 0m;
        }

        var freeWindow = freeCancellationHoursOverride ?? FreeCancellationHours;
        var hoursBeforeStart = (cleaningUtc - cancelUtc).TotalHours;
        return hoursBeforeStart switch
        {
            var h when h >= freeWindow => 0m,
            >= PartialCancellationHours => PartialCancellationFeeRate,
            _ => LastMinuteCancellationFeeRate,
        };
    }
}
