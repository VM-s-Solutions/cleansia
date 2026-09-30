using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// A schedule's time is the market's wall-clock time: every client shows "Wednesday 10:00" and means
/// 10:00 where the home is. The occurrences are UTC instants, so the same 10:00 is 08:00Z in summer and
/// 09:00Z in winter, and the hour must not drift across a daylight-saving change.
/// </summary>
public sealed class RecurringOccurrenceMarketTimeTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneResolution.Resolve("Europe/Prague");

    [Fact]
    public void A_Prague_Ten_OClock_Schedule_Is_Ten_OClock_Prague_In_Summer()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        var now = Utc(2026, 7, 6, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(7), Prague);

        Assert.Equal([Utc(2026, 7, 8, 8, 0)], occurrences);
    }

    [Fact]
    public void A_Prague_Ten_OClock_Schedule_Is_Ten_OClock_Prague_In_Winter()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        var now = Utc(2026, 1, 5, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(7), Prague);

        Assert.Equal([Utc(2026, 1, 7, 9, 0)], occurrences);
    }

    [Fact]
    public void The_Hour_Holds_Across_The_Spring_Change()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        var now = Utc(2026, 3, 23, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(14), Prague);

        Assert.Equal([Utc(2026, 3, 25, 9, 0), Utc(2026, 4, 1, 8, 0)], occurrences);
    }

    [Fact]
    public void The_Hour_Holds_Across_The_Autumn_Change()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        var now = Utc(2026, 10, 19, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(14), Prague);

        Assert.Equal([Utc(2026, 10, 21, 8, 0), Utc(2026, 10, 28, 9, 0)], occurrences);
    }

    [Fact]
    public void A_Sunday_Schedule_Keeps_Its_Hour_On_The_Day_The_Clocks_Go_Forward()
    {
        var template = Weekly(DayOfWeek.Sunday, new TimeOnly(10, 0));
        var now = Utc(2026, 3, 20, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(14), Prague);

        Assert.Equal([Utc(2026, 3, 22, 9, 0), Utc(2026, 3, 29, 8, 0)], occurrences);
    }

    /// <summary>02:30 does not exist in Prague on 29 March 2026; the slot moves forward by the gap.</summary>
    [Fact]
    public void A_Time_The_Spring_Change_Skips_Moves_Forward_Instead_Of_Failing_The_Template()
    {
        var template = Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30));
        var now = Utc(2026, 3, 27, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(7), Prague);

        Assert.Equal([Utc(2026, 3, 29, 1, 30)], occurrences);
    }

    /// <summary>02:30 happens twice in Prague on 25 October 2026; the later, standard-time one is taken.</summary>
    [Fact]
    public void A_Time_The_Autumn_Change_Repeats_Is_The_Standard_Time_One()
    {
        var template = Weekly(DayOfWeek.Sunday, new TimeOnly(2, 30));
        var now = Utc(2026, 10, 23, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(7), Prague);

        Assert.Equal([Utc(2026, 10, 25, 1, 30)], occurrences);
    }

    /// <summary>
    /// The resume pointer is a UTC instant from before the change. Stepping it in the market's calendar
    /// lands on the next Wednesday at 10:00 local, not on the instant a week of hours later.
    /// </summary>
    [Fact]
    public void Resuming_After_A_Materialized_Winter_Occurrence_Lands_On_The_Summer_Hour()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        template.MarkMaterializedFor(Utc(2026, 3, 25, 9, 0));
        var now = Utc(2026, 3, 23, 0, 0);

        var occurrences = Compute(template, now, Utc(2026, 4, 10, 0, 0), Prague);

        Assert.Equal([Utc(2026, 4, 1, 8, 0), Utc(2026, 4, 8, 8, 0)], occurrences);
    }

    /// <summary>
    /// 22:30Z on Tuesday is already Wednesday in Prague, so Tuesday's 23:00 slot there is behind us and the
    /// next one is a week away. Read in the UTC calendar, "today" would still be Tuesday.
    /// </summary>
    [Fact]
    public void The_Weekday_Is_Read_In_The_Markets_Calendar()
    {
        var template = Weekly(DayOfWeek.Tuesday, new TimeOnly(23, 0));
        var now = Utc(2026, 7, 7, 22, 30);

        var occurrences = Compute(template, now, now.AddDays(8), Prague);

        Assert.Equal([Utc(2026, 7, 14, 21, 0)], occurrences);
    }

    [Fact]
    public void A_Market_Without_A_Zone_Reads_The_Time_In_Utc()
    {
        var template = Weekly(DayOfWeek.Wednesday, new TimeOnly(10, 0));
        var now = Utc(2026, 7, 6, 0, 0);

        var occurrences = Compute(template, now, now.AddDays(7), TimeZoneInfo.Utc);

        Assert.Equal([Utc(2026, 7, 8, 10, 0)], occurrences);
    }

    private static List<DateTime> Compute(
        RecurringBookingTemplate template, DateTime now, DateTime horizon, TimeZoneInfo zone) =>
        MaterializeRecurringBookingTemplate.Handler.ComputeOccurrences(template, now, horizon, zone).ToList();

    private static RecurringBookingTemplate Weekly(DayOfWeek day, TimeOnly time) =>
        RecurringBookingTemplate.Create(
            userId: "user-market-time",
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: day,
            timeOfDay: time,
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-market-time",
            selectedServiceIds: ["service-market-time"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: Utc(2026, 1, 1, 0, 0));

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
