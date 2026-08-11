using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0526 — the cancellation ladder's TIER, the label a client renders instead of re-deriving it.
///
/// <para><see cref="CancellationFeeRateBoundaryTests"/> already pins the rate on both sides of every
/// boundary; this suite pins that the tier moves with it, at the same instants, and that the
/// tier→rate table is the only place a rate is attached to a tier. The two together are what makes
/// "the preview and the cancel cannot disagree" a property of the code rather than of review
/// vigilance: there is ONE ladder, <see cref="BookingPolicy.ClassifyCancellation"/>, and
/// <see cref="BookingPolicy.CalculateCancellationFeeRate"/> is a wrapper over it.</para>
/// </summary>
public class CancellationFeeTierTests
{
    private static readonly DateTime BookingCreated = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static CancellationFeeTier Tier(
        DateTime cleaning, DateTime cancel, bool accepted = true,
        bool firstTime = false, int? freeOverride = null) =>
        BookingPolicy.ClassifyCancellation(
            cleaning, BookingCreated, cancel,
            isFirstTimeCustomer: firstTime, hasBeenAccepted: accepted,
            freeCancellationHoursOverride: freeOverride);

    private static decimal Rate(
        DateTime cleaning, DateTime cancel, bool accepted = true,
        bool firstTime = false, int? freeOverride = null) =>
        BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel,
            isFirstTimeCustomer: firstTime, hasBeenAccepted: accepted,
            freeCancellationHoursOverride: freeOverride);

    // ── The tier→rate table: the ONE place a tier is priced ──

    [Theory]
    [InlineData(CancellationFeeTier.FreeNotAccepted)]
    [InlineData(CancellationFeeTier.FreeOopsWindow)]
    [InlineData(CancellationFeeTier.FreeOutsideWindow)]
    public void Every_Free_Tier_Prices_At_Zero(CancellationFeeTier tier)
    {
        Assert.Equal(0m, BookingPolicy.CancellationFeeRateFor(tier));
    }

    [Fact]
    public void Charging_Tiers_Price_At_The_Policy_Constants()
    {
        Assert.Equal(
            BookingPolicy.PartialCancellationFeeRate,
            BookingPolicy.CancellationFeeRateFor(CancellationFeeTier.Partial));
        Assert.Equal(
            BookingPolicy.LastMinuteCancellationFeeRate,
            BookingPolicy.CancellationFeeRateFor(CancellationFeeTier.LastMinute));
    }

    // ── The tier and the rate move together, at the same instants ──

    [Fact]
    public void Rate_Always_Equals_The_Priced_Tier_Across_The_Whole_Ladder()
    {
        // Anti-fork pin: were the ladder ever copied so the label and the number came from two
        // evaluations, one of these hours would disagree. The set spans every arm plus both sides of
        // both boundaries, for a member window and the standard one.
        var cleaning = BookingCreated.AddDays(30);

        foreach (var freeOverride in new int?[] { null, BookingPolicy.FreeCancellationHours, 4 })
        {
            foreach (var hoursBefore in new[] { 48d, 24d, 23.99, 15d, 12d, 4d, 3.99, 0.5 })
            {
                var cancel = cleaning.AddHours(-hoursBefore);
                Assert.Equal(
                    BookingPolicy.CancellationFeeRateFor(Tier(cleaning, cancel, freeOverride: freeOverride)),
                    Rate(cleaning, cancel, freeOverride: freeOverride));
            }
        }
    }

    // ── No cleaner on the job — the short-circuit that precedes everything ──

    [Fact]
    public void No_Assignment_Then_FreeNotAccepted_Even_Inside_The_LastMinute_Tier()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddMinutes(-30);

        Assert.Equal(CancellationFeeTier.FreeNotAccepted, Tier(cleaning, cancel, accepted: false));
        Assert.Equal(CancellationFeeTier.FreeNotAccepted, Tier(cleaning, cancel, accepted: false, freeOverride: 4));
    }

    // ── Oops window ──

    [Fact]
    public void At_Exactly_The_Standard_Oops_Cap_Then_FreeOopsWindow()
    {
        var cleaning = BookingCreated.AddHours(3);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard);

        Assert.Equal(CancellationFeeTier.FreeOopsWindow, Tier(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Past_The_Oops_Cap_Then_The_Timing_Tier_Applies()
    {
        var cleaning = BookingCreated.AddHours(3);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard + 1);

        Assert.Equal(CancellationFeeTier.LastMinute, Tier(cleaning, cancel));
    }

    [Fact]
    public void FirstTime_Customer_Gets_The_Wider_Oops_Cap()
    {
        var cleaning = BookingCreated.AddHours(3);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard + 1);

        Assert.Equal(CancellationFeeTier.LastMinute, Tier(cleaning, cancel));
        Assert.Equal(CancellationFeeTier.FreeOopsWindow, Tier(cleaning, cancel, firstTime: true));
    }

    // ── Free / partial / last-minute boundaries ──

    [Fact]
    public void At_Exactly_The_Free_Window_Then_FreeOutsideWindow()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours);

        Assert.Equal(CancellationFeeTier.FreeOutsideWindow, Tier(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Inside_The_Free_Window_Then_Partial()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours).AddMinutes(1);

        Assert.Equal(CancellationFeeTier.Partial, Tier(cleaning, cancel));
    }

    [Fact]
    public void At_Exactly_The_Partial_Threshold_Then_Partial()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours);

        Assert.Equal(CancellationFeeTier.Partial, Tier(cleaning, cancel));
    }

    [Fact]
    public void One_Minute_Inside_The_Partial_Threshold_Then_LastMinute()
    {
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours).AddMinutes(1);

        Assert.Equal(CancellationFeeTier.LastMinute, Tier(cleaning, cancel));
    }

    // ── The member's own window: 4 hours, not 24 ──

    [Fact]
    public void Plus_Window_Of_Four_Hours_Is_FreeOutsideWindow_Where_Standard_Is_Partial()
    {
        // Six hours out: a Plus member whose plan carries FreeCancellationWindowHours = 4 (the seeded
        // value) is free; the same order for a non-member is the 25% tier.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-6);

        Assert.Equal(CancellationFeeTier.Partial, Tier(cleaning, cancel));
        Assert.Equal(CancellationFeeTier.FreeOutsideWindow, Tier(cleaning, cancel, freeOverride: 4));
    }

    [Fact]
    public void Plus_Window_Does_Not_Move_The_LastMinute_Threshold()
    {
        // Inside the member's own 4h window the ladder resumes: three hours out is still last-minute.
        var cleaning = BookingCreated.AddDays(30);
        var cancel = cleaning.AddHours(-3);

        Assert.Equal(CancellationFeeTier.LastMinute, Tier(cleaning, cancel, freeOverride: 4));
    }
}
