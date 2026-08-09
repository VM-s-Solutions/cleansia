using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D5.3 — Invariant H after the relaxation from 90% to 80%: for every SEAT on every order, at
/// least <c>MinimumOpenBoardShare</c> of that seat's fill window is open to the entire board, across
/// <b>all</b> preferred-cleaner rounds rather than one.
///
/// <para>The product bound below is the whole of the invariant's enforcement and neither number may
/// move alone — same idiom as <c>OrderSpanCapTests</c>' <c>cap &lt;= floor</c>. It is a CONSERVATIVE
/// OVER-ESTIMATE: it sums fractions of the ORIGINAL lead where round two only ever gets a fraction of
/// the REMAINING lead. The true worst case is 19% against a 20% bound. <b>Do not "tighten" it into
/// equality</b> — the looseness is the safety margin, and the simulation below is what pins the real
/// number.</para>
/// </summary>
public class PreferredOfferInvariantTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Every_Round_At_Its_Fraction_Stays_Inside_The_Open_Board_Share()
    {
        Assert.True(
            BookingPolicy.MaxPreferredOfferRounds * BookingPolicy.PreferredHoldFraction
                <= 1m - BookingPolicy.MinimumOpenBoardShare,
            $"MaxPreferredOfferRounds ({BookingPolicy.MaxPreferredOfferRounds}) x PreferredHoldFraction "
            + $"({BookingPolicy.PreferredHoldFraction}) must stay at or under 1 - MinimumOpenBoardShare "
            + $"({1m - BookingPolicy.MinimumOpenBoardShare}). Raising the round cap requires lowering the "
            + "fraction or re-ruling Invariant H; neither number moves alone.");
    }

    /// <summary>
    /// The bound above is arithmetic on constants; this is the same claim against the real formula,
    /// with the re-offer taken at the EARLIEST instant the no-eviction invariant permits (the lapse of
    /// round one — a later re-offer withholds strictly less). It is what would catch a change to
    /// <c>ComputePreferredHold</c> that the constant product cannot see.
    /// </summary>
    [Fact]
    public void Two_Rounds_Taken_As_Early_As_Possible_Never_Withhold_More_Than_The_Ruled_Share()
    {
        for (var leadHours = 8.0; leadHours <= 24 * 60; leadHours += 0.25)
        {
            var withheld = WithheldHoursOverEveryRound(leadHours);

            Assert.True(
                (decimal)(withheld / leadHours) <= 1m - BookingPolicy.MinimumOpenBoardShare,
                $"lead {leadHours}h withheld {withheld}h across "
                + $"{BookingPolicy.MaxPreferredOfferRounds} rounds");
        }
    }

    /// <summary>
    /// The 19%-against-20% margin, pinned so a later reader does not "fix" the bound into tightness.
    /// Below the ceiling the sequence is h1 = 0.10L then h2 = 0.10(0.9L) = 0.09L, union 0.19L exactly.
    /// </summary>
    [Theory]
    [InlineData(24.0)]
    [InlineData(48.0)]
    [InlineData(120.0)]
    public void The_True_Worst_Case_Below_The_Ceiling_Is_Nineteen_Percent(double leadHours)
    {
        var withheld = WithheldHoursOverEveryRound(leadHours);

        Assert.Equal(0.19 * leadHours, withheld, precision: 6);
    }

    /// <summary>
    /// The ladder terminates. Without the cap the window formula recomputes off the REMAINING lead and
    /// decays by 10% a round, which from a seven-day booking takes ~30 reservations to reach the
    /// eight-hour floor — a lead-time floor is not a loop bound.
    /// </summary>
    [Fact]
    public void The_Ladder_Stops_At_The_Round_Cap_Rather_Than_At_The_Lead_Time_Floor()
    {
        var rounds = 0;
        var cursor = Now;
        var cleaning = Now.AddHours(168);

        while (rounds < BookingPolicy.MaxPreferredOfferRounds
               && BookingPolicy.ComputePreferredHold(cleaning, cursor) > TimeSpan.Zero)
        {
            cursor = cursor.Add(BookingPolicy.ComputePreferredHold(cleaning, cursor));
            rounds++;
        }

        Assert.Equal(BookingPolicy.MaxPreferredOfferRounds, rounds);
        Assert.True(
            BookingPolicy.ComputePreferredHold(cleaning, cursor) > TimeSpan.Zero,
            "the formula would still grant a third window; only the round cap stops the ladder");
    }

    private static double WithheldHoursOverEveryRound(double leadHours)
    {
        var cleaning = Now.AddHours(leadHours);
        var cursor = Now;
        var withheld = 0.0;

        for (var round = 0; round < BookingPolicy.MaxPreferredOfferRounds; round++)
        {
            var hold = BookingPolicy.ComputePreferredHold(cleaning, cursor);
            if (hold <= TimeSpan.Zero)
            {
                break;
            }

            withheld += hold.TotalHours;
            cursor = cursor.Add(hold);
        }

        return withheld;
    }
}
