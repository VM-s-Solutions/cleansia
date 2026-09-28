using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Monthly is the nth weekday of the month, n read off the schedule's first date (owner ruling
/// 2026-09-28): the 2nd Thursday stays the 2nd Thursday, and a schedule that began on a 5th weekday takes
/// the last one in a month that has no 5th. It replaced a 30-day step that snapped forward to the weekday,
/// which gave a 35-day gap — ten visits a year, drifting later through the month.
///
/// <para>Thursdays, autumn 2026: October 1 8 15 22 29, November 5 12 19 26, December 3 10 17 24 31.
/// Prague is UTC+2 until 25 October and UTC+1 after it, so 10:00 there is 08:00Z and then 09:00Z.</para>
/// </summary>
public sealed class RecurringOccurrenceCadenceTests
{
    private static readonly TimeZoneInfo Prague = TimeZoneResolution.Resolve("Europe/Prague");

    [Fact]
    public void Monthly_Keeps_The_Second_Thursday_Across_Months_Of_Four_And_Five()
    {
        var template = Monthly(startsOn: Utc(2026, 10, 5, 0, 0));

        var occurrences = Compute(template, now: Utc(2026, 10, 1, 0, 0), horizon: Utc(2027, 1, 1, 0, 0));

        Assert.Equal([Utc(2026, 10, 8, 8, 0), Utc(2026, 11, 12, 9, 0), Utc(2026, 12, 10, 9, 0)], occurrences);
    }

    [Fact]
    public void Monthly_Keeps_The_Fourth_Thursday_Even_When_The_Month_Has_A_Fifth()
    {
        var template = Monthly(startsOn: Utc(2026, 10, 22, 0, 0));

        var occurrences = Compute(template, now: Utc(2026, 10, 20, 0, 0), horizon: Utc(2027, 1, 1, 0, 0));

        Assert.Equal([Utc(2026, 10, 22, 8, 0), Utc(2026, 11, 26, 9, 0), Utc(2026, 12, 24, 9, 0)], occurrences);
    }

    [Fact]
    public void Monthly_From_A_Fifth_Thursday_Takes_The_Last_Thursday_Of_A_Month_Without_One()
    {
        var template = Monthly(startsOn: Utc(2026, 10, 29, 0, 0));

        var occurrences = Compute(template, now: Utc(2026, 10, 26, 0, 0), horizon: Utc(2027, 1, 1, 0, 0));

        Assert.Equal([Utc(2026, 10, 29, 9, 0), Utc(2026, 11, 26, 9, 0), Utc(2026, 12, 31, 9, 0)], occurrences);
    }

    /// <summary>
    /// An edit clears the resume pointer. The cadence comes from the first date, not from "the next
    /// Thursday after now", so an edit on 20 October leaves the next visit on the 2nd Thursday of
    /// November rather than moving it to 22 October.
    /// </summary>
    [Fact]
    public void An_Edit_That_Clears_The_Resume_Pointer_Keeps_The_Monthly_Cadence()
    {
        var template = Monthly(startsOn: Utc(2026, 10, 5, 0, 0));

        var occurrences = Compute(template, now: Utc(2026, 10, 20, 0, 0), horizon: Utc(2026, 11, 30, 0, 0));

        Assert.Equal([Utc(2026, 11, 12, 9, 0)], occurrences);
    }

    [Fact]
    public void Monthly_Resumes_After_The_Last_Materialized_Visit()
    {
        var template = Monthly(startsOn: Utc(2026, 10, 5, 0, 0));
        template.MarkMaterializedFor(Utc(2026, 11, 12, 9, 0));

        var occurrences = Compute(template, now: Utc(2026, 11, 1, 0, 0), horizon: Utc(2026, 12, 31, 0, 0));

        Assert.Equal([Utc(2026, 12, 10, 9, 0)], occurrences);
    }

    /// <summary>
    /// A fortnightly schedule that began on 1 October visits on the 1st, 15th and 29th. An edit on
    /// 20 October clears the resume pointer; the next visit stays on the 29th, not the off-week 22nd.
    /// </summary>
    [Fact]
    public void An_Edit_Mid_Cycle_Keeps_A_Biweekly_Schedule_On_Its_Own_Weeks()
    {
        var template = Biweekly(startsOn: Utc(2026, 10, 1, 0, 0));
        template.MarkMaterializedFor(Utc(2026, 10, 15, 8, 0));
        template.UpdateSchedule(
            frequency: RecurrenceFrequency.Biweekly,
            dayOfWeek: DayOfWeek.Thursday,
            timeOfDay: new TimeOnly(11, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-cadence",
            selectedServiceIds: ["service-cadence"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: template.StartsOn,
            endsOn: null,
            preferredEmployeeId: null);

        var occurrences = Compute(template, now: Utc(2026, 10, 20, 0, 0), horizon: Utc(2026, 11, 20, 0, 0));

        Assert.Equal([Utc(2026, 10, 29, 10, 0), Utc(2026, 11, 12, 10, 0)], occurrences);
    }

    [Fact]
    public void Biweekly_Resumes_A_Fortnight_After_The_Last_Materialized_Visit()
    {
        var template = Biweekly(startsOn: Utc(2026, 10, 1, 0, 0));
        template.MarkMaterializedFor(Utc(2026, 10, 15, 8, 0));

        var occurrences = Compute(template, now: Utc(2026, 10, 10, 0, 0), horizon: Utc(2026, 11, 1, 0, 0));

        Assert.Equal([Utc(2026, 10, 29, 9, 0)], occurrences);
    }

    /// <summary>
    /// The sweep ticks at night and its first candidate can be today. A slot closer than the two-hour
    /// floor a one-off booking is held to is not a candidate: it would be an order created already late.
    /// </summary>
    [Fact]
    public void A_Candidate_Under_The_Minimum_Lead_Time_Is_Not_Yielded()
    {
        var template = RecurringBookingTemplate.Create(
            userId: "user-cadence",
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: DayOfWeek.Wednesday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-cadence",
            selectedServiceIds: ["service-cadence"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: Utc(2026, 7, 1, 0, 0));

        var occurrences = MaterializeRecurringBookingTemplate.Handler
            .ComputeOccurrences(template, Utc(2026, 7, 8, 9, 0), Utc(2026, 7, 16, 0, 0), TimeZoneInfo.Utc)
            .ToList();

        Assert.Equal([Utc(2026, 7, 15, 10, 0)], occurrences);
    }

    private static List<DateTime> Compute(RecurringBookingTemplate template, DateTime now, DateTime horizon) =>
        MaterializeRecurringBookingTemplate.Handler.ComputeOccurrences(template, now, horizon, Prague).ToList();

    private static RecurringBookingTemplate Monthly(DateTime startsOn) => Thursdays(RecurrenceFrequency.Monthly, startsOn);

    private static RecurringBookingTemplate Biweekly(DateTime startsOn) => Thursdays(RecurrenceFrequency.Biweekly, startsOn);

    private static RecurringBookingTemplate Thursdays(RecurrenceFrequency frequency, DateTime startsOn) =>
        RecurringBookingTemplate.Create(
            userId: "user-cadence",
            frequency: frequency,
            dayOfWeek: DayOfWeek.Thursday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-cadence",
            selectedServiceIds: ["service-cadence"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: startsOn);

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
