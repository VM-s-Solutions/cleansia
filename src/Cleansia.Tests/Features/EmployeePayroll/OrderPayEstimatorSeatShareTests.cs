using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The board and the job detail quote one seat of the job, raised by the dirtiness rate the order was
/// booked at: the order entity (detail, dashboard) and the list projection row (board) both carry the
/// crew size and the stored rate into the estimate, so a later change to <c>BookingPolicy</c> never
/// re-quotes a booked job.
/// </summary>
public class OrderPayEstimatorSeatShareTests
{
    private const string CurrencyId = "czk";
    private const string EmployeeId = "emp-1";
    private const string ServiceId = "svc-1";
    private const decimal JobPay = 500m;
    private const decimal EarlierHeavyRate = 0.60m;

    private static readonly Type Estimator =
        typeof(OrderFactory)
            .Assembly
            .GetType("Cleansia.Core.AppServices.Features.Orders.OrderPayEstimator")!;

    private static MethodInfo EstimateFor(Type source) =>
        Estimator.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Estimate" && m.GetParameters()[0].ParameterType == source);

    public static TheoryData<int, DirtinessLevel, decimal, decimal> Seats() => new()
    {
        { 1, DirtinessLevel.Normal, 0m, 500m },
        { 2, DirtinessLevel.Normal, 0m, 250m },
        { 1, DirtinessLevel.Increased, 0.15m, 575m },
        // 250 a seat, plus 30 % of the job's 500 split across two seats.
        { 2, DirtinessLevel.Heavy, 0.30m, 325m },
        // Booked when Heavy was 60 %: 250 a seat, plus 60 % of the job's 500 split across two seats.
        { 2, DirtinessLevel.Heavy, EarlierHeavyRate, 400m },
    };

    [Theory]
    [MemberData(nameof(Seats))]
    public void The_Detail_Quotes_One_Seat_Raised_By_The_Booked_Rate(
        int seats, DirtinessLevel level, decimal rate, decimal expected)
    {
        var order = OrderBookedAt(seats, level, rate);

        Assert.Equal(seats, order.RequiredEmployees);
        Assert.Equal(expected, EstimateDetail(order));
    }

    [Theory]
    [MemberData(nameof(Seats))]
    public void The_Board_Quotes_One_Seat_Raised_By_The_Booked_Rate(
        int seats, DirtinessLevel level, decimal rate, decimal expected)
    {
        Assert.Equal(expected, EstimateBoard(RowBookedAt(seats, level, rate)));
    }

    /// <summary>
    /// A one-seat Heavy job booked at 60 %: 500 + 60 % of 500 = 800. Today's 30 % would quote 650, so
    /// an estimate that reads <c>BookingPolicy</c> instead of the order fails here.
    /// </summary>
    [Fact]
    public void The_Detail_Quotes_The_Rate_The_Order_Was_Booked_At_Not_Todays()
    {
        Assert.NotEqual(BookingPolicy.HeavyDirtinessSurchargeRate, EarlierHeavyRate);

        Assert.Equal(800m, EstimateDetail(OrderBookedAt(seats: 1, DirtinessLevel.Heavy, EarlierHeavyRate)));
    }

    /// <summary>The board's twin of the case above, through the list projection row.</summary>
    [Fact]
    public void The_Board_Quotes_The_Rate_The_Order_Was_Booked_At_Not_Todays()
    {
        Assert.NotEqual(BookingPolicy.HeavyDirtinessSurchargeRate, EarlierHeavyRate);

        Assert.Equal(800m, EstimateBoard(RowBookedAt(seats: 1, DirtinessLevel.Heavy, EarlierHeavyRate)));
    }

    private static decimal? EstimateDetail(Order order) =>
        (decimal?)EstimateFor(typeof(Order)).Invoke(null, [order, EmployeeId, JobPayConfigs(), NoConfigs()]);

    private static decimal? EstimateBoard(OrderListRow row) =>
        (decimal?)EstimateFor(typeof(OrderListRow)).Invoke(null, [row, EmployeeId, JobPayConfigs(), NoConfigs()]);

    private static IReadOnlyList<EmployeePayConfig> JobPayConfigs() =>
        [EmployeePayConfig.CreateForService(ServiceId, JobPay, CurrencyId)];

    private static IReadOnlyList<EmployeePayConfig> NoConfigs() => [];

    private static Order OrderBookedAt(int seats, DirtinessLevel level, decimal rate)
    {
        var service = Service.Create("cat-1", "Standard clean", "Regular");
        service.Id = ServiceId;
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
        order.SetDirtinessSurcharge(level, 0m, rate);
        return order;
    }

    private static OrderListRow RowBookedAt(int seats, DirtinessLevel level, decimal rate)
    {
        var noTranslations = new Dictionary<string, Translation>();
        return new OrderListRow(
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
                new OrderListServiceRow(ServiceId, "Standard clean", "", 1000m, 0m, noTranslations,
                    new OrderListCategoryRow("cat-1", "regular", "Regular", "", 0, noTranslations))
            ],
            AssignedEmployees: [],
            RequiredEmployees: seats,
            MaxEmployees: seats,
            HasReview: false,
            CompletedAt: null,
            DirtinessLevel: level,
            DirtinessSurchargeAmount: 0m,
            DirtinessRate: rate);
    }
}
