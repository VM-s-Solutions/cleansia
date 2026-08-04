using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0036 D3 — the hold window is a fraction of the fill window, ceilinged, and ZERO below a lead
/// time of <c>2 * BookingPolicy.StandardLeadTimeHours</c>. Invariant H is what these numbers exist to
/// deliver: at least 90% of every seat's fill window stays open to the whole board, at every lead time.
/// </summary>
public class PreferredHoldWindowTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2.0)]
    [InlineData(3.9)]
    [InlineData(4.0)]
    [InlineData(7.99)]
    public void No_Hold_Is_Granted_Below_The_Eight_Hour_Floor(double leadHours)
    {
        var hold = BookingPolicy.ComputePreferredHold(Now.AddHours(leadHours), Now);

        Assert.Equal(TimeSpan.Zero, hold);
    }

    /// <summary>
    /// The floor is a DERIVATION of the one urgency constant, not a second constant. If express ever
    /// moves, the hold floor moves with it — a literal 8 here would silently stop tracking it.
    /// </summary>
    [Fact]
    public void The_Floor_Is_Twice_The_Standard_Lead_Time_Constant()
    {
        var floorHours = 2 * BookingPolicy.StandardLeadTimeHours;

        Assert.Equal(TimeSpan.Zero, BookingPolicy.ComputePreferredHold(Now.AddHours(floorHours - 0.01), Now));
        Assert.True(BookingPolicy.ComputePreferredHold(Now.AddHours(floorHours), Now) > TimeSpan.Zero);
    }

    [Fact]
    public void The_Shortest_Hold_The_Formula_Can_Produce_Is_Forty_Eight_Minutes()
    {
        var hold = BookingPolicy.ComputePreferredHold(Now.AddHours(2 * BookingPolicy.StandardLeadTimeHours), Now);

        Assert.Equal(TimeSpan.FromMinutes(48), hold);
    }

    [Theory]
    [InlineData(8, 48)]
    [InlineData(24, 144)]
    [InlineData(72, 432)]
    public void The_Hold_Is_A_Tenth_Of_The_Lead_Time_Below_The_Ceiling(double leadHours, int expectedMinutes)
    {
        var hold = BookingPolicy.ComputePreferredHold(Now.AddHours(leadHours), Now);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), hold);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(168)]
    [InlineData(720)]
    public void The_Hold_Never_Exceeds_The_Ceiling(double leadHours)
    {
        var hold = BookingPolicy.ComputePreferredHold(Now.AddHours(leadHours), Now);

        Assert.Equal(TimeSpan.FromHours(BookingPolicy.PreferredHoldCeilingHours), hold);
    }

    /// <summary>
    /// Invariant H, restated per seat (ADR-0036 CH-V4): the hold may never be more than a tenth of the
    /// time there was to fill the seat. Checked across the whole bookable range rather than at the
    /// three worked examples, because the ceiling and the fraction interact.
    /// </summary>
    [Fact]
    public void At_Least_Ninety_Percent_Of_Every_Fill_Window_Stays_Open_To_The_Board()
    {
        for (var leadHours = 2.0; leadHours <= 24 * 60; leadHours += 0.25)
        {
            var hold = BookingPolicy.ComputePreferredHold(Now.AddHours(leadHours), Now);

            Assert.True(
                hold.TotalHours <= leadHours * 0.10 + 1e-9,
                $"lead {leadHours}h granted a hold of {hold.TotalHours}h");
        }
    }

    [Fact]
    public void A_Cleaning_Time_In_The_Past_Earns_No_Hold()
    {
        Assert.Equal(TimeSpan.Zero, BookingPolicy.ComputePreferredHold(Now.AddHours(-1), Now));
    }
}
