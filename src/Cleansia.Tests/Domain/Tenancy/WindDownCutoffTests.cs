using Cleansia.Core.Domain.Tenancy;

namespace Cleansia.Tests.Domain.Tenancy;

/// <summary>
/// The wind-down cut-off is midnight of the date in the market's own zone, so a booking at 23:30
/// Bratislava time on the evening before the last day is not swept while one at 00:30 on the day
/// is; a market with no zone, or an unknown one, is read in UTC rather than refusing the run.
/// </summary>
public sealed class WindDownCutoffTests
{
    private static readonly DateOnly From = new(2026, 10, 1);

    [Fact]
    public void Midnight_In_A_Summer_Time_Zone_Is_The_Evening_Before_In_Utc()
    {
        var cutoff = WindDownCutoff.Utc(From, "Europe/Bratislava");

        Assert.Equal(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), cutoff);
        Assert.Equal(DateTimeKind.Utc, cutoff.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    public void A_Missing_Or_Unknown_Zone_Reads_The_Date_In_Utc(string? timeZoneId)
    {
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), WindDownCutoff.Utc(From, timeZoneId));
    }

    [Fact]
    public void The_Local_Date_Of_An_Instant_Follows_The_Zone()
    {
        var lateEvening = new DateTimeOffset(2026, 9, 30, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 10, 1), WindDownCutoff.LocalDate(lateEvening, "Europe/Bratislava"));
        Assert.Equal(new DateOnly(2026, 9, 30), WindDownCutoff.LocalDate(lateEvening, null));
    }
}
