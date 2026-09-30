using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: a booking starts on the quarter-hour, from 08:00 to 19:45, read
/// in the service address's market clock, and no more than 60 days ahead.
/// </summary>
public sealed class BookingStartWindowTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneResolution.Resolve("Europe/Prague");
    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 15)]
    [InlineData(12, 30)]
    [InlineData(19, 45)]
    public void A_Quarter_Hour_Inside_The_Day_Is_Bookable(int hour, int minute) =>
        Assert.True(BookingPolicy.IsBookableTimeOfDay(new TimeOnly(hour, minute)));

    [Theory]
    [InlineData(7, 45)]
    [InlineData(20, 0)]
    [InlineData(3, 0)]
    [InlineData(10, 7)]
    [InlineData(10, 20)]
    public void A_Start_Off_The_Grid_Or_Outside_The_Day_Is_Not(int hour, int minute) =>
        Assert.False(BookingPolicy.IsBookableTimeOfDay(new TimeOnly(hour, minute)));

    [Fact]
    public void Seconds_Past_A_Quarter_Hour_Are_Off_The_Grid() =>
        Assert.False(BookingPolicy.IsBookableTimeOfDay(new TimeOnly(10, 15, 30)));

    /// <summary>
    /// 06:00Z on a summer day is 08:00 in Prague — the first slot — and not a slot at all read in UTC.
    /// The clock is the market's, never the server's or the device's.
    /// </summary>
    [Fact]
    public void The_Start_Is_Read_In_The_Markets_Clock()
    {
        var eightInPrague = new DateTime(2026, 7, 8, 6, 0, 0, DateTimeKind.Utc);

        Assert.True(BookingPolicy.IsBookableStart(eightInPrague, Now.AddMonths(-3), Prague));
        Assert.False(BookingPolicy.IsBookableStart(eightInPrague, Now.AddMonths(-3), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Nineteen_Forty_Five_In_Winter_Prague_Is_Bookable_And_Twenty_Is_Not()
    {
        var now = new DateTime(2026, 12, 1, 6, 0, 0, DateTimeKind.Utc);

        Assert.True(BookingPolicy.IsBookableStart(new DateTime(2026, 12, 3, 18, 45, 0, DateTimeKind.Utc), now, Prague));
        Assert.False(BookingPolicy.IsBookableStart(new DateTime(2026, 12, 3, 19, 0, 0, DateTimeKind.Utc), now, Prague));
    }

    [Fact]
    public void Sixty_Days_Ahead_Is_The_Horizon()
    {
        var sixtyDays = Now.AddDays(BookingPolicy.MaxBookingHorizonDays);

        Assert.True(BookingPolicy.IsBookableStart(sixtyDays, Now, TimeZoneInfo.Utc));
        Assert.False(BookingPolicy.IsBookableStart(sixtyDays.AddMinutes(15), Now, TimeZoneInfo.Utc));
    }
}
