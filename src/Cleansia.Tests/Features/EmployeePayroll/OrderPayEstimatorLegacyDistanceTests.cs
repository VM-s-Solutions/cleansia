using System.Globalization;
using System.Reflection;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The pay a cleaner is shown before taking a job ignores a stored travel distance and a legacy
/// kilometre rate, exactly as the pay they are later written does. Driven through the entity overload
/// of <c>OrderPayEstimator.Estimate</c>, which the order detail and the dashboard use and which funnels
/// into the same primitive the paged list and the available-jobs preview call.
/// </summary>
public class OrderPayEstimatorLegacyDistanceTests
{
    private const string CurrencyId = "czk";
    private const string EmployeeId = "emp-1";

    private static readonly MethodInfo EstimateForOrder =
        typeof(Cleansia.Core.AppServices.Features.Orders.OrderFactory)
            .Assembly
            .GetType("Cleansia.Core.AppServices.Features.Orders.OrderPayEstimator")!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Estimate" && m.GetParameters()[0].ParameterType == typeof(Order));

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("12.5")]
    public void The_Estimate_Is_Base_And_Extras_Whatever_The_Stored_Distance(string? distance)
    {
        var service = Service.Create("cat-1", "Standard clean", "Regular");
        var shared = EmployeePayConfig.CreateForService(
            service.Id, basePay: 300m, currencyId: CurrencyId, extraPerRoom: 10m, extraPerBathroom: 10m);
        var own = EmployeePayConfig.CreateForService(
            service.Id, basePay: 420m, currencyId: CurrencyId, extraPerRoom: 30m, extraPerBathroom: 20m,
            employeeId: EmployeeId);
        SetLegacyDistanceRate(shared, 7m);
        SetLegacyDistanceRate(own, 5m);

        var order = OrderWith(service, rooms: 3, bathrooms: 2);
        if (distance is not null)
        {
            typeof(Order).GetProperty(nameof(Order.TravelDistance))!
                .SetValue(order, decimal.Parse(distance, CultureInfo.InvariantCulture));
        }

        var estimate = (decimal?)EstimateForOrder.Invoke(null,
        [
            order,
            EmployeeId,
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig> { shared, own },
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig>()
        ]);

        // The cleaner's own rate, not the shared one: 420 + 2×30 + 2×20.
        Assert.Equal(520m, estimate);
    }

    private static void SetLegacyDistanceRate(EmployeePayConfig config, decimal rate) =>
        typeof(EmployeePayConfig).GetProperty(nameof(EmployeePayConfig.DistanceRatePerKm))!
            .SetValue(config, rate);

    private static Order OrderWith(Service service, int rooms, int bathrooms)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Prague", "10000", "CZ"),
            rooms: rooms,
            bathrooms: bathrooms,
            cleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending);
        order.AddSelectedServices([OrderService.Create(order, service, 1000m, 0m, 1000m)]);
        return order;
    }
}
