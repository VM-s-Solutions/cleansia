using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner rulings 2026-09-28: the dirtiness level lengthens the booked time by its own rate, and a
/// service's per-room minutes lengthen it by the size of the home, so the crew and the cash rule follow
/// the work actually booked.
/// </summary>
public class OrderDurationTests
{
    [Theory]
    [InlineData(120, DirtinessLevel.Normal, 120)]
    [InlineData(120, DirtinessLevel.Increased, 138)]
    [InlineData(120, DirtinessLevel.Heavy, 156)]
    [InlineData(45, DirtinessLevel.Increased, 52)]
    [InlineData(45, DirtinessLevel.Heavy, 59)]
    [InlineData(0, DirtinessLevel.Heavy, 0)]
    public void The_Level_Lengthens_The_Booked_Minutes_Rounded_Up_To_A_Whole_Minute(
        int minutes, DirtinessLevel level, int expected)
    {
        var estimate = OrderDuration.EstimateMinutes(
            [NewService("svc", minutes)], [], unitCount: 0, BookingPolicy.DirtinessSurchargeRate(level));

        Assert.Equal(expected, estimate);
    }

    /// <summary>
    /// The sample catalogue's General Cleaning is exactly one cleaner's 120 minutes, so any level above
    /// Normal makes it a two-cleaner job, and a two-cleaner job cannot pay cash.
    /// </summary>
    [Theory]
    [InlineData(DirtinessLevel.Normal, 1, true)]
    [InlineData(DirtinessLevel.Increased, 2, false)]
    [InlineData(DirtinessLevel.Heavy, 2, false)]
    public void The_Crew_And_Cash_Follow_The_Level(DirtinessLevel level, int crew, bool cash)
    {
        var minutes = OrderDuration.EstimateMinutes(
            [NewService("general", 120)], [], unitCount: 3, BookingPolicy.DirtinessSurchargeRate(level));

        Assert.Equal(crew, OrderDuration.RequiredEmployees(minutes));
        Assert.Equal(cash, BookingPolicy.AllowsCash(signedIn: true, OrderDuration.RequiredEmployees(minutes)));
    }

    /// <summary>
    /// Per-room minutes count once per room and once per bathroom, the count the per-room price
    /// multiplies, for a service selected directly and for one inside a package alike; the level then
    /// lengthens the whole.
    /// </summary>
    [Theory]
    [InlineData(0, DirtinessLevel.Normal, 90)]
    [InlineData(3, DirtinessLevel.Normal, 135)]
    [InlineData(3, DirtinessLevel.Heavy, 176)]
    public void Per_Room_Minutes_Scale_With_The_Home_Then_The_Level_Applies(
        int unitCount, DirtinessLevel level, int expected)
    {
        var bundle = Package.Create("Bundle", "Bundle");
        bundle.AddService(NewService("packaged", 30, minutesPerRoom: 5));

        var estimate = OrderDuration.EstimateMinutes(
            [NewService("direct", 60, minutesPerRoom: 10)],
            [bundle],
            unitCount,
            BookingPolicy.DirtinessSurchargeRate(level));

        Assert.Equal(expected, estimate);
    }

    private static Service NewService(string id, int minutes, int minutesPerRoom = 0)
    {
        var service = Service.Create("category-1", id, id, minutes, minutesPerRoom);
        service.Id = id;
        return service;
    }
}
