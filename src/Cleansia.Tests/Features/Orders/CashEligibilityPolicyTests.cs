using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-24: cash only for a signed-in customer whose booking REQUIRES exactly one
/// cleaner. The crew is <c>ceil(minutes / 120)</c> with a floor of one, the same count the order is
/// staffed with; spare seats never make a second cleaner required.
/// </summary>
public class CashEligibilityPolicyTests
{
    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(120, 1)]
    [InlineData(121, 2)]
    [InlineData(240, 2)]
    [InlineData(241, 3)]
    public void The_Required_Crew_Is_One_Cleaner_Per_Started_Two_Hours_And_Never_Zero(int minutes, int expected)
    {
        Assert.Equal(expected, OrderDuration.RequiredEmployees(minutes));
    }

    [Theory]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, false)]
    [InlineData(true, 3, false)]
    public void Cash_Is_Allowed_Only_To_A_Signed_In_Customer_On_A_One_Cleaner_Job(
        bool signedIn, int requiredEmployees, bool expected)
    {
        Assert.Equal(expected, BookingPolicy.AllowsCash(signedIn, requiredEmployees));
    }

    [Theory]
    [InlineData(120, true)]
    [InlineData(121, false)]
    public void The_Boundary_Sits_Between_120_And_121_Minutes_For_A_Signed_In_Customer(int minutes, bool expected)
    {
        Assert.Equal(expected, BookingPolicy.AllowsCash(signedIn: true, OrderDuration.RequiredEmployees(minutes)));
    }

    [Fact]
    public void Spare_Seats_Raise_The_Seat_Count_But_Not_The_Required_Crew()
    {
        var order = Order.Create(
            "Customer", "customer@example.com", "+420000000000",
            Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1, bathrooms: 1, DateTime.UtcNow.AddDays(2), PaymentType.Cash, 1000m, "czk", PaymentStatus.Pending,
            userId: "user-1");
        order.UpdateEstimatedTime(120);

        order.CalculateRequiredEmployees(spareSeats: 2);

        Assert.Equal(3, order.MaxEmployees);
        Assert.Equal(1, order.RequiredEmployees);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(120, 1)]
    [InlineData(121, 2)]
    public void The_Order_Is_Staffed_With_The_Same_Count(int minutes, int expected)
    {
        var order = Order.Create(
            "Customer", "customer@example.com", "+420000000000",
            Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1, bathrooms: 1, DateTime.UtcNow.AddDays(2), PaymentType.Card, 1000m, "czk", PaymentStatus.Pending);
        order.UpdateEstimatedTime(minutes);

        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);

        Assert.Equal(expected, order.RequiredEmployees);
        Assert.Equal(OrderDuration.RequiredEmployees(minutes), order.RequiredEmployees);
    }
}
