using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Decision 45 (2026-09-28) — the contract for work binds the operating company and one cleaner, so the
/// price its facts carry is that cleaner's reward for one seat as the board quotes it, never what the
/// customer pays for the order.
/// </summary>
public sealed class WorkContractFactsBuilderTests
{
    private const string OrderId = "order-facts-1";
    private const string ServiceId = "svc-facts-1";
    private const string CallerId = "emp-facts-caller";
    private const string OtherId = "emp-facts-other";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Order _order;

    public WorkContractFactsBuilderTests()
    {
        var currency = CreateOrderTestData.DefaultCurrency();
        var service = Service.Create("cat-1", "Standard clean", "Regular");
        service.Id = ServiceId;
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 0,
            cleaningDateTime: new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddSelectedServices([OrderService.Create(order, service, 1500m, 0m, 1500m)]);
        order.UpdateEstimatedTime(240).CalculateRequiredEmployees(spareSeats: 0);
        _order = order;
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var shared = EmployeePayConfig.CreateForService(ServiceId, 600m, currency.Id);
        var callersOwn = EmployeePayConfig.CreateForService(ServiceId, 800m, currency.Id, employeeId: CallerId);
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.Is<IEnumerable<string>>(ids => ids.Single() == ServiceId), CallerId,
                It.Is<IReadOnlyCollection<string>>(c => c.Single() == currency.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([shared, callersOwn]);
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.Is<IEnumerable<string>>(ids => ids.Single() == ServiceId), OtherId,
                It.Is<IReadOnlyCollection<string>>(c => c.Single() == currency.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([shared]);
    }

    private WorkContractFactsBuilder CreateBuilder() => new(_orderRepository.Object, _payConfigRepository.Object);

    [Fact]
    public async Task The_Price_Is_The_Named_Cleaners_Reward_For_One_Of_The_Two_Seats_Not_The_Order_Total()
    {
        var callers = await CreateBuilder().BuildAsync(OrderId, CallerId, CancellationToken.None);
        var others = await CreateBuilder().BuildAsync(OrderId, OtherId, CancellationToken.None);

        Assert.Equal(400m, callers!.TotalPrice);
        Assert.Equal(300m, others!.TotalPrice);
        Assert.Equal("CZK", callers.CurrencyCode);
        Assert.Equal("Standard clean", callers.Services.Single().Name);
    }

    /// <summary>
    /// The caller's 800 over two seats is 400; a heavy job booked at 60 % adds 60 % of 800 split the same
    /// way, 240. Today's 30 % would add 120, so the reward reads the order's rate, not the policy's.
    /// </summary>
    [Fact]
    public async Task The_Reward_Is_Raised_By_The_Rate_The_Order_Was_Booked_At()
    {
        _order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 900m, 0.60m);

        var facts = await CreateBuilder().BuildAsync(OrderId, CallerId, CancellationToken.None);

        Assert.Equal(640m, facts!.TotalPrice);
    }

    [Fact]
    public async Task A_Cleaner_No_Rate_Covers_Is_Stated_No_Reward_Rather_Than_The_Customers_Price()
    {
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), "emp-facts-unrated", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var facts = await CreateBuilder().BuildAsync(OrderId, "emp-facts-unrated", CancellationToken.None);

        Assert.Equal(0m, facts!.TotalPrice);
    }

    [Fact]
    public async Task An_Order_That_Does_Not_Exist_Has_No_Facts()
    {
        Assert.Null(await CreateBuilder().BuildAsync("order-nowhere", CallerId, CancellationToken.None));
    }
}
