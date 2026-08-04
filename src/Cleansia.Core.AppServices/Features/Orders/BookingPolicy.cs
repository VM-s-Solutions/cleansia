using Cleansia.Core.AppServices.Shared.DTOs.Enums;

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

    /// <summary>
    /// Seats an order carries BEYOND the crew the work needs (<c>Order.RequiredEmployees</c>, derived
    /// from the booked estimate). Passed to <c>Order.CalculateRequiredEmployees</c>, which is the only
    /// writer of the seat cap.
    ///
    /// Zero, by owner ruling (ADR-0037 D9.4 / ADR-0039): a spare seat is not free. Pay is one row per
    /// assigned employee with no crew-size term, so every seat filled beyond the requirement is a
    /// second full wage against an unchanged customer price.
    /// </summary>
    public const int SpareSeatsPerOrder = 0;

    /// <summary>
    /// The longest span a single booking may be CREATED with. <b>Enforced, not assumed:</b>
    /// <c>OrderFactory</c> rejects above it and <c>CreateOrder.Validator</c> mirrors it, which is what
    /// makes <c>Order.MaxOrderSpanHours</c>' premise — no order is longer than the overlap scan's lower
    /// bound — true by construction instead of by hope. <c>OrderSpanCapTests</c> pins
    /// <c>cap &lt;= floor</c>; that ordering is the whole safety argument and neither number moves alone.
    ///
    /// <para><b>This is a DISCLOSURE bound, not a double-booking one</b> (ADR-0039 CH-Q2 / CH-D2), and
    /// the distinction is why it is worth its weight. A double booking would need an order longer than
    /// the 168 h scan floor — a seven-day appointment nothing produces, real but not live. What IS live
    /// is that the span is a plain sum over a caller-chosen selection, so before this cap the client
    /// handed the server any window it liked, from minutes up to 58.25 h (every seeded service plus
    /// every seeded package's included services on one order). A caller-controlled window that wide,
    /// pointed at the preferred-cleaner availability answer, is a binary-search primitive for
    /// reconstructing a cleaner's private schedule. <b>Read this cap as a double-booking guard and you
    /// will correctly de-prioritise it — and ship the oracle.</b></para>
    ///
    /// <para>It is also, unavoidably, a CREW cap: <c>RequiredEmployees = ceil(EstimatedTime / 120)</c>
    /// and <see cref="SpareSeatsPerOrder"/> is 0, so 24 h implies at most 12 seats. 24 rejects nothing a
    /// real appointment does — the entire seeded service catalog booked at once is 20.25 h — and rejects
    /// every selection only a probe or a mis-click produces.</para>
    /// </summary>
    public const int MaxBookableOrderSpanHours = 24;

    /// <summary>
    /// <see cref="MaxBookableOrderSpanHours"/> in the unit <c>Order.EstimatedTime</c> is written in.
    /// </summary>
    public const int MaxBookableOrderSpanMinutes = MaxBookableOrderSpanHours * 60;

    /// <summary>
    /// True if a booked estimate exceeds <see cref="MaxBookableOrderSpanHours"/>. The one comparison,
    /// shared by the validator and <c>OrderFactory</c> — they derive the estimate differently (an
    /// aggregate over the catalog vs. a sum over loaded entities) and must not also disagree on where
    /// the line is.
    /// </summary>
    public static bool ExceedsMaxBookableSpan(int estimatedMinutes)
        => estimatedMinutes > MaxBookableOrderSpanMinutes;

    /// <summary>
    /// True if the given start time requires express surcharge (2–4h lead).
    /// </summary>
    /// <param name="waiverApplies">
    /// A membership benefit has already been RESOLVED and RESERVED for this booking, so the surcharge is
    /// waived. A parameter rather than an <c>&amp;&amp;</c> at each call site, for the same reason
    /// <see cref="CalculateCancellationFeeRate"/>'s <c>freeCancellationHoursOverride</c> is one: the
    /// parameter makes every caller answer the question and makes an omission greppable. Default
    /// <see langword="false"/> = "no benefit" — the same default direction as the cancellation override.
    /// <para>A <see langword="bool"/>, never the waiver record: this class gets the ANSWER, never the
    /// reason, and learns nothing about memberships.</para>
    /// </param>
    public static bool RequiresExpressSurcharge(
        DateTime cleaningUtc, DateTime nowUtc, bool waiverApplies = false)
    {
        if (waiverApplies)
        {
            return false;
        }

        var leadHours = (cleaningUtc - nowUtc).TotalHours;
        return leadHours >= ExpressLeadTimeHours && leadHours < StandardLeadTimeHours;
    }

    /// <summary>True if the given cleaning start time is too soon to accept any booking.</summary>
    public static bool IsBelowMinimumLeadTime(DateTime cleaningUtc, DateTime nowUtc)
    {
        return (cleaningUtc - nowUtc).TotalHours < ExpressLeadTimeHours;
    }

    /// <summary>
    /// ADR-0036 D3 — the share of an order's fill window a preferred cleaner's first refusal may
    /// consume, and the absolute ceiling on it. Together they deliver Invariant H: at least 90% of
    /// every SEAT's fill window is open to the entire board, at every lead time.
    ///
    /// <para>The ceiling is 12 h because that is the smallest window that always intersects a normal
    /// waking period whatever the creation time — a 22:00 → 10:00 hold still covers 07:00–10:00 — which
    /// is the actual thing a hold has to be worth granting. Both numbers are reasoned defaults, not
    /// measurements, and they are <c>const</c>: changing them is a backend release (no client reads
    /// them) and it does not re-time orders that already carry a granted deadline.</para>
    /// </summary>
    public const decimal PreferredHoldFraction = 0.10m;
    public const int PreferredHoldCeilingHours = 12;

    /// <summary>
    /// ADR-0036 D3 — how long the customer's preferred cleaner gets first refusal on the order's first
    /// seat. <see cref="TimeSpan.Zero"/> means no hold may be granted for this lead time; the targeted
    /// notification is granted on a wider predicate and survives that (D4.1).
    ///
    /// <para>The floor is <c>2 * <see cref="StandardLeadTimeHours"/></c> — a DERIVATION of the one
    /// urgency constant, never a second one. At a five-hour lead the named cleaner is the least likely
    /// person on the board to be free, precisely because the notice is short.</para>
    /// </summary>
    public static TimeSpan ComputePreferredHold(DateTime cleaningUtc, DateTime nowUtc)
    {
        var leadHours = (cleaningUtc - nowUtc).TotalHours;
        if (leadHours < 2 * StandardLeadTimeHours)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromHours(Math.Min(leadHours * (double)PreferredHoldFraction, PreferredHoldCeilingHours));
    }

    /// <summary>
    /// Gross up an already-discounted subtotal by the express surcharge. Discount applies to the raw
    /// subtotal and the surcharge goes on top of the discounted price — the ONE ordering, shared by
    /// <c>OrderFactory</c> (what gets persisted) and <c>QuoteOrder</c> (what the wizard shows), so the
    /// quoted saving and the receipted saving cannot drift apart.
    /// </summary>
    public static decimal ApplyExpressSurcharge(decimal discountedSubtotal, bool surchargeApplies)
        => surchargeApplies ? discountedSubtotal * (1 + ExpressSurchargeRate) : discountedSubtotal;

    /// <summary>
    /// Compute the cancellation fee rate (0.0–1.0) for a given cancellation time.
    ///
    /// Acceptance-aware: if no cleaner has been pulled onto the job yet, cancellation
    /// is always free regardless of timing — there is no cleaner's time to compensate.
    /// Once accepted, the standard tiered policy applies (free 24+ h before, 25% 4–24 h
    /// before, 50% under 4 h).
    /// </summary>
    /// <param name="cleaningUtc">Order's scheduled start time.</param>
    /// <param name="bookingCreatedUtc">When the order was created.</param>
    /// <param name="cancelUtc">Current time / proposed cancel time.</param>
    /// <param name="isFirstTimeCustomer">Whether the customer has 0 prior completed orders.</param>
    /// <param name="hasBeenAccepted">
    /// True if a cleaner has actually been pulled onto the job — i.e. the order carries at least one
    /// ASSIGNMENT row (<c>Order.AssignedEmployees</c>), which is what <c>CancelOrder</c> passes.
    /// <b>Not</b> an <c>OrderStatusHistory</c> entry of <c>OrderStatus.Confirmed</c>: Confirmed is a
    /// deliberately overloaded status in this domain (payment settled OR cleaner assigned) written by
    /// four paths, only one of which involves a cleaner, and it is not even written when a cleaner
    /// takes an order that was already Confirmed.
    /// </param>
    /// <param name="freeCancellationHoursOverride">
    /// Absolute free-cancellation threshold in hours that REPLACES
    /// <see cref="FreeCancellationHours"/> when set. The sole caller
    /// (<c>CancelOrder</c>) passes <c>CancellationPolicy.FreeCancellationHours</c>,
    /// which the resolver fills with the absolute window: 24 for the standard tier,
    /// the membership's <c>FreeCancellationWindowHours</c> for a Plus member. A
    /// SMALLER threshold is MORE generous (a member can cancel free closer to the
    /// start), so a Plus plan seeded at e.g. 4 is wider than the standard 24h. Null
    /// keeps the standard 24h window. The partial-fee and last-minute thresholds and
    /// rates are unaffected — only the free window moves.
    /// </param>
    /// <returns>Fee rate: 0.0 = free, 0.25 = quarter charge, 0.5 = half charge.</returns>
    public static decimal CalculateCancellationFeeRate(
        DateTime cleaningUtc,
        DateTime bookingCreatedUtc,
        DateTime cancelUtc,
        bool isFirstTimeCustomer,
        bool hasBeenAccepted,
        int? freeCancellationHoursOverride = null)
        => CancellationFeeRateFor(ClassifyCancellation(
            cleaningUtc,
            bookingCreatedUtc,
            cancelUtc,
            isFirstTimeCustomer,
            hasBeenAccepted,
            freeCancellationHoursOverride));

    /// <summary>
    /// The cancellation schedule itself — the ONE evaluation of which arm applies, shared by the fee
    /// the customer is charged and the tier the preview discloses. <see cref="CalculateCancellationFeeRate"/>
    /// is a wrapper over this, so the number and the reason cannot come from two ladders that drift.
    /// </summary>
    /// <inheritdoc cref="CalculateCancellationFeeRate" path="/param"/>
    public static CancellationFeeTier ClassifyCancellation(
        DateTime cleaningUtc,
        DateTime bookingCreatedUtc,
        DateTime cancelUtc,
        bool isFirstTimeCustomer,
        bool hasBeenAccepted,
        int? freeCancellationHoursOverride = null)
    {
        if (!hasBeenAccepted)
        {
            return CancellationFeeTier.FreeNotAccepted;
        }

        var oopsMinutes = isFirstTimeCustomer ? OopsWindowMinutesFirstTime : OopsWindowMinutesStandard;
        if ((cancelUtc - bookingCreatedUtc).TotalMinutes <= oopsMinutes)
        {
            return CancellationFeeTier.FreeOopsWindow;
        }

        var freeWindow = freeCancellationHoursOverride ?? FreeCancellationHours;
        var hoursBeforeStart = (cleaningUtc - cancelUtc).TotalHours;
        return hoursBeforeStart switch
        {
            var h when h >= freeWindow => CancellationFeeTier.FreeOutsideWindow,
            >= PartialCancellationHours => CancellationFeeTier.Partial,
            _ => CancellationFeeTier.LastMinute,
        };
    }

    /// <summary>What a given tier costs, as a fraction of the order total. The only place a tier is priced.</summary>
    public static decimal CancellationFeeRateFor(CancellationFeeTier tier) => tier switch
    {
        CancellationFeeTier.Partial => PartialCancellationFeeRate,
        CancellationFeeTier.LastMinute => LastMinuteCancellationFeeRate,
        _ => 0m,
    };
}
