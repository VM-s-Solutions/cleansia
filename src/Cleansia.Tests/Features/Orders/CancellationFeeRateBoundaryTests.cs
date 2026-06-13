using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Adversarial boundary gap-fill for <see cref="BookingPolicy.CalculateCancellationFeeRate"/>
/// (AC1/AC2/AC3). The base suite pins the tier midpoints and the exact-boundary INNER edge
/// (<c>h == FreeCancellationHours</c> → free, <c>h == PartialCancellationHours</c> → 25%); these
/// cases pin the OTHER side of each switch arm — the value an off-by-one (<c>&gt;</c> vs
/// <c>&gt;=</c>) implementation would get wrong — plus the Plus free-window override that the base
/// suite never exercises. Every expected rate is the policy's named constant, never a recomputation.
/// </summary>
public class CancellationFeeRateBoundaryTests
{
    private static readonly DateTime BookingCreated = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static decimal Rate(
        DateTime cleaning, DateTime cancel, bool accepted = true,
        bool firstTime = false, int? freeOverride = null) =>
        BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel,
            isFirstTimeCustomer: firstTime, hasBeenAccepted: accepted,
            freeCancellationHoursOverride: freeOverride);

    // ── Free/partial boundary at exactly FreeCancellationHours (24h) ──

    [Fact]
    public void At_Exactly_FreeWindow_Then_Free()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours);

        Assert.Equal(0m, Rate(cleaning, cancel));
    }

    [Fact]
    public void Just_Inside_FreeWindow_By_One_Minute_Then_Partial_Not_Free()
    {
        // 23h59m before start — below 24h, so the free arm must NOT catch it. An implementation that
        // used > 24 instead of >= 24, or rounded the hours, would wrongly still bill 0.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours).AddMinutes(1);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Outside_FreeWindow_Then_Free()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours).AddMinutes(-1);

        Assert.Equal(0m, Rate(cleaning, cancel));
    }

    // ── Partial/last-minute boundary at exactly PartialCancellationHours (4h) ──

    [Fact]
    public void At_Exactly_PartialWindow_Then_Partial()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel));
    }

    [Fact]
    public void Just_Inside_PartialWindow_By_One_Minute_Then_LastMinute_Not_Partial()
    {
        // 3h59m before start — below 4h, so the last-minute arm must catch it. A > 4 vs >= 4 slip,
        // or hour-rounding, would wrongly bill only 25%.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours).AddMinutes(1);

        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, Rate(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Outside_PartialWindow_Then_Partial()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours).AddMinutes(-1);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel));
    }

    // ── Oops-window boundary at exactly the cap ──

    [Fact]
    public void At_Exactly_Standard_Oops_Window_Then_Free()
    {
        // cleaning soon (last-minute tier) but cancel is exactly at the 15-min cap — the <= keeps it free.
        var cleaning = BookingCreated.AddHours(3);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard);

        Assert.Equal(0m, Rate(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Past_Standard_Oops_Window_Then_Tier_Applies()
    {
        // 16 min after booking, < 4h before start → last-minute tier (the oops short-circuit is gone).
        var cleaning = BookingCreated.AddHours(3);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard + 1);

        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, Rate(cleaning, cancel));
    }

    // ── AC3: free-window override REPLACES FreeCancellationHours; partial/last-minute unchanged ──
    // The override is the hours-before-start at/above which the cancel is free. A SMALLER override is a
    // more generous free window (free even when cancelling closer to the appointment). These pin the
    // current behavior at a 12h override that the base suite never exercises.

    [Fact]
    public void Override_FreeInsideOverrideWindow_WhereDefaultWouldBePartial()
    {
        // 15h before start: inside the default 24h window (would be 25%), but at/above a 12h override → free.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-15);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel));      // default 24h
        Assert.Equal(0m, Rate(cleaning, cancel, freeOverride: 12));                          // override 12h
    }

    [Fact]
    public void Override_AtExactlyOverrideBoundary_Then_Free()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-12);

        Assert.Equal(0m, Rate(cleaning, cancel, freeOverride: 12));
    }

    [Fact]
    public void Override_DoesNotMovePartialOrLastMinuteThresholds()
    {
        var cleaning = BookingCreated.AddDays(30);

        // Just above the override's edge (12h01m before start, i.e. further out) is still free.
        Assert.Equal(0m, Rate(cleaning, cleaning.AddHours(-12).AddMinutes(-1), freeOverride: 12));

        // The partial/last-minute boundary is unchanged by the override: exactly 4h is still partial,
        // 3h59m is still last-minute.
        Assert.Equal(
            BookingPolicy.PartialCancellationFeeRate,
            Rate(cleaning, cleaning.AddHours(-BookingPolicy.PartialCancellationHours), freeOverride: 12));
        Assert.Equal(
            BookingPolicy.LastMinuteCancellationFeeRate,
            Rate(cleaning, cleaning.AddHours(-BookingPolicy.PartialCancellationHours).AddMinutes(1), freeOverride: 12));
    }

    [Fact]
    public void Override_BelowOverrideWindow_Then_Partial()
    {
        // 11h before start is below the 12h override free edge but at/above the 4h partial edge → 25%.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-11);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel, freeOverride: 12));
    }

    [Fact]
    public void NotAccepted_OverridesEvenInsideLastMinuteTier_RegardlessOfPlusWindow()
    {
        // Acceptance short-circuit precedes both oops and tiers and ignores the override entirely.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddMinutes(-30);

        Assert.Equal(0m, Rate(cleaning, cancel, accepted: false, freeOverride: 12));
    }
}
