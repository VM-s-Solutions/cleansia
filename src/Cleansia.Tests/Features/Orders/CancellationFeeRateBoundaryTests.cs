using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Adversarial boundary gap-fill for <see cref="BookingPolicy.CalculateCancellationFeeRate"/>.
/// The base suite pins the tier midpoints and the exact-boundary INNER edge
/// (<c>h == FreeCancellationHours</c> → free, <c>h == PartialCancellationHours</c> → 25%); these
/// cases pin the OTHER side of each switch arm — the value an off-by-one (<c>&gt;</c> vs
/// <c>&gt;=</c>) implementation would get wrong — plus the free-window override that the base
/// suite never exercises.
///
/// Override contract: <c>freeCancellationHoursOverride</c> is the ABSOLUTE free-cancellation
/// threshold in hours (it REPLACES <see cref="BookingPolicy.FreeCancellationHours"/>), matching the
/// only production caller — <c>CancelOrder</c> passes <c>CancellationPolicy.FreeCancellationHours</c>,
/// which <c>CancellationPolicyResolver</c> resolves to the absolute window: 24 for the standard tier,
/// the membership's <c>FreeCancellationWindowHours</c> for a Plus member. A SMALLER threshold is MORE
/// generous (free even closer to the start), so a Plus plan seeded at 4 lets a member cancel free up
/// to 4h before — wider than the standard 24h. This is the contract every adjacent piece (the
/// membership domain model, the DTOs, the create/update validators, the seed) already speaks; a
/// "widen-by delta" inversion here, with the caller still feeding the absolute value, collapses the
/// standard 24h threshold to 0 and refunds every standard cancellation in full. Every expected rate
/// is the policy's named constant, never a recomputation.
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

    // ── Override is the ABSOLUTE free threshold (it REPLACES FreeCancellationHours) ──
    // A SMALLER threshold is MORE generous (free closer to the start). This is the production caller's
    // shape: CancelOrder passes CancellationPolicy.FreeCancellationHours, the resolver's absolute
    // window — 24 for standard, the membership's FreeCancellationWindowHours (seed 4) for Plus.

    // ── The PRODUCTION standard-tier shape: the resolver supplies the absolute 24, NOT null. ──
    // CancelOrder never passes null; it always passes CancellationPolicy.FreeCancellationHours, which
    // for a non-member resolves to BookingPolicy.FreeCancellationHours (24). These pin that the
    // standard tier is charged its real fees under the value the sole caller actually feeds — the
    // gap a null-only or delta-only test leaves open, and where a "widen-by delta" inversion silently
    // refunds every standard cancellation in full.

    [Fact]
    public void StandardTier_ResolverSuppliedAbsoluteWindow_LastMinute_StillCharges50()
    {
        // 30 min before start, accepted, resolver-supplied absolute window 24 (the production value) →
        // last-minute tier. Must charge 0.50, not 0. (Inverted "24 − 24 = 0" → free = money leak.)
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddMinutes(-30);

        Assert.Equal(
            BookingPolicy.LastMinuteCancellationFeeRate,
            Rate(cleaning, cancel, freeOverride: BookingPolicy.FreeCancellationHours));
    }

    [Fact]
    public void StandardTier_ResolverSuppliedAbsoluteWindow_Partial_StillCharges25()
    {
        // 12h before start, accepted, resolver-supplied absolute window 24 → partial tier. Must charge
        // 0.25, not 0.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-12);

        Assert.Equal(
            BookingPolicy.PartialCancellationFeeRate,
            Rate(cleaning, cancel, freeOverride: BookingPolicy.FreeCancellationHours));
    }

    [Fact]
    public void StandardTier_ResolverSuppliedAbsoluteWindow_AtExactlyFree_IsFree()
    {
        // Exactly 24h before start with the resolver-supplied absolute window 24 → free (>=).
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours);

        Assert.Equal(0m, Rate(cleaning, cancel, freeOverride: BookingPolicy.FreeCancellationHours));
    }

    [Fact]
    public void StandardTier_ResolverSuppliedAbsoluteWindow_EqualsNullDefault_AcrossTiers()
    {
        // The resolver-supplied 24 must behave IDENTICALLY to the null default across every tier — the
        // standard tier is genuinely unchanged whether the caller passes null or the absolute 24.
        var cleaning = BookingCreated.AddDays(30);

        foreach (var hoursBefore in new[] { 48d, 24d, 23.99, 12d, 4d, 3.99, 0.5 })
        {
            var cancel = cleaning.AddHours(-hoursBefore);
            Assert.Equal(
                Rate(cleaning, cancel),
                Rate(cleaning, cancel, freeOverride: BookingPolicy.FreeCancellationHours));
        }
    }

    // ── Plus override (a SMALLER absolute threshold) is MORE generous than the standard 24h ──

    [Fact]
    public void SmallerOverride_WidensFreeWindow_WhereDefaultWouldBePartial()
    {
        // 15h before start: standard 24h window → 25%. A Plus member whose absolute window is 4
        // (the seed) is free at 15h (15 ≥ 4). A larger override would be STRICTER, the wrong direction.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-15);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel));   // standard tier
        Assert.Equal(0m, Rate(cleaning, cancel, freeOverride: 4));                        // Plus: more generous → free
    }

    [Fact]
    public void Override_IsMonotonic_SmallerThresholdNeverChargesMoreThanLarger()
    {
        // Fix the cancel time at 15h before start. As the absolute threshold SHRINKS the fee must be
        // non-increasing (more generous), and a small enough threshold drives it to free.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-15);

        var large = Rate(cleaning, cancel, freeOverride: 48);       // threshold 48 → 15h < 48 → 0.25
        var standard = Rate(cleaning, cancel);                      // no override → 24 → 0.25
        var medium = Rate(cleaning, cancel, freeOverride: 15);      // threshold 15 → free
        var small = Rate(cleaning, cancel, freeOverride: 4);        // threshold 4 → free

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, large);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, standard);
        Assert.Equal(0m, medium);
        Assert.Equal(0m, small);
        Assert.True(large >= standard && standard >= medium && medium >= small);
    }

    [Fact]
    public void Override_AtExactlyThreshold_Then_Free()
    {
        // Absolute threshold 15 → exactly 15h before start → free (>=).
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-15);

        Assert.Equal(0m, Rate(cleaning, cancel, freeOverride: 15));
    }

    [Fact]
    public void Override_JustInsideThreshold_By_One_Minute_Then_Partial_Not_Free()
    {
        // Absolute threshold 15 → 14h59m before start is below 15h → free arm must NOT catch it; still
        // at/above the 4h partial edge → 25%.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-15).AddMinutes(1);

        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, Rate(cleaning, cancel, freeOverride: 15));
    }

    [Fact]
    public void Override_DoesNotMovePartialOrLastMinuteThresholds()
    {
        // A wider free threshold (8h) leaves the lower tiers' own boundaries intact: exactly 4h is
        // still partial, 3h59m is still last-minute.
        var cleaning = BookingCreated.AddDays(30);

        Assert.Equal(
            BookingPolicy.PartialCancellationFeeRate,
            Rate(cleaning, cleaning.AddHours(-BookingPolicy.PartialCancellationHours), freeOverride: 8));
        Assert.Equal(
            BookingPolicy.LastMinuteCancellationFeeRate,
            Rate(cleaning, cleaning.AddHours(-BookingPolicy.PartialCancellationHours).AddMinutes(1), freeOverride: 8));
    }

    [Fact]
    public void NullOverride_BehavesLikeStandard_FreeWindowUnchanged()
    {
        // A null override falls back to the standard 24h window. Pins that the default is unchanged.
        var cleaning = BookingCreated.AddDays(30);

        Assert.Equal(0m, Rate(cleaning, cleaning.AddHours(-BookingPolicy.FreeCancellationHours)));
        Assert.Equal(
            BookingPolicy.PartialCancellationFeeRate,
            Rate(cleaning, cleaning.AddHours(-BookingPolicy.FreeCancellationHours).AddMinutes(1)));
    }

    [Fact]
    public void NotAccepted_OverridesEvenInsideLastMinuteTier_RegardlessOfWindow()
    {
        // Acceptance short-circuit precedes both oops and tiers and ignores the override entirely.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddMinutes(-30);

        Assert.Equal(0m, Rate(cleaning, cancel, accepted: false, freeOverride: 4));
    }
}
