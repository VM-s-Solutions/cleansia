using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The board and the job detail quote one seat of the job, raised by the dirtiness level: the order
/// entity (detail, dashboard) and the list projection row (board) both carry the crew size and the
/// level into the estimate.
/// </summary>
public class OrderPayEstimatorSeatShareTests
{
    private const string CurrencyId = "czk";
    private const string EmployeeId = "emp-1";

    private static readonly Type Estimator =
        typeof(Cleansia.Core.AppServices.Features.Orders.OrderFactory)
            .Assembly
            .GetType("Cleansia.Core.AppServices.Features.Orders.OrderPayEstimator")!;

    private static MethodInfo EstimateFor(Type source) =>
        Estimator.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Estimate" && m.GetParameters()[0].ParameterType == source);

    public static TheoryData<int, DirtinessLevel, decimal> Seats() => new()
    {
        { 1, DirtinessLevel.Normal, 500m },
        { 2, DirtinessLevel.Normal, 250m },
        { 1, DirtinessLevel.Increased, 650m },
        // 250 a seat, plus 60 % of the job's 500 split across two seats.
        { 2, DirtinessLevel.Heavy, 400m },
    };

    [Theory]
    [MemberData(nameof(Seats))]
    public void The_Detail_Quotes_One_Seat_Raised_By_The_Level(int seats, DirtinessLevel level, decimal expected)
    {
        var service = Service.Create("cat-1", "Standard clean", "Regular");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Prague", "10000", "CZ"),
            rooms: 1,
            bathrooms: 0,
            cleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending);
        order.AddSelectedServices([OrderService.Create(order, service, 1000m, 0m, 1000m)]);
        order.UpdateEstimatedTime(seats * 120).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(level, 0m);

        var estimate = (decimal?)EstimateFor(typeof(Order)).Invoke(null,
        [
            order,
            EmployeeId,
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig> { EmployeePayConfig.CreateForService(service.Id, 500m, CurrencyId) },
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig>()
        ]);

        Assert.Equal(seats, order.RequiredEmployees);
        Assert.Equal(expected, estimate);
    }

    [Theory]
    [MemberData(nameof(Seats))]
    public void The_Board_Quotes_One_Seat_Raised_By_The_Level(int seats, DirtinessLevel level, decimal expected)
    {
        const string serviceId = "svc-1";
        var noTranslations = new Dictionary<string, Translation>();
        var row = new OrderListRow(
            Id: "order-1",
            CustomerName: "Cust",
            CustomerEmail: "c@example.com",
            CustomerPhone: "+420123456789",
            Address: null,
            DisplayOrderNumber: "1",
            Rooms: 1,
            Bathrooms: 0,
            ExtraSlugs: [],
            CleaningDateTime: new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            PaymentType: PaymentType.Card,
            PaymentStatus: PaymentStatus.Paid,
            TotalPrice: 1000m,
            TierDiscountAmount: null,
            MembershipDiscountAmount: null,
            PromoDiscountAmount: null,
            CreditAppliedAmount: 0m,
            EstimatedTime: seats * 120,
            OrderStatus: OrderStatus.New,
            CurrencyId: CurrencyId,
            Currency: new OrderListCurrencyRow(CurrencyId, "CZK", "Kc", "Czech koruna", true),
            SelectedPackages: [],
            SelectedServices:
            [
                new OrderListServiceRow(serviceId, "Standard clean", "", 1000m, 0m, noTranslations,
                    new OrderListCategoryRow("cat-1", "regular", "Regular", "", 0, noTranslations))
            ],
            AssignedEmployees: [],
            RequiredEmployees: seats,
            MaxEmployees: seats,
            HasReview: false,
            CompletedAt: null,
            DirtinessLevel: level);

        var estimate = (decimal?)EstimateFor(typeof(OrderListRow)).Invoke(null,
        [
            row,
            EmployeeId,
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig> { EmployeePayConfig.CreateForService(serviceId, 500m, CurrencyId) },
            (IReadOnlyList<EmployeePayConfig>)new List<EmployeePayConfig>()
        ]);

        Assert.Equal(expected, estimate);
    }
}
