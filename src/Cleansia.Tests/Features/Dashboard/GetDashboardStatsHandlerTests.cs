using System.Linq.Expressions;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Moq;

namespace Cleansia.Tests.Features.Dashboard;

/// <summary>
/// DTO regression for the batched <see cref="GetDashboardStats.Handler"/>: the four completion
/// counts now arrive via ONE grouped query (<see
/// cref="IOrderRepository.CountCompletedForEmployeeWindowsAsync"/>) and the three earnings windows
/// via ONE fetch (<see cref="IOrderRepository.GetCompletedOrdersInEitherRangeAsync"/>) partitioned
/// in memory — the response DTO must be field-for-field what the previous per-window round trips
/// produced, and the legacy per-window methods must not be called at all.
/// </summary>
public class GetDashboardStatsHandlerTests
{
    private const string CallerEmployeeId = "emp-dash-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _employeeInvoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolutionService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public GetDashboardStatsHandlerTests()
    {
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _session.Setup(s => s.GetTimeZoneId()).Returns((string?)null);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);

        _orderRepository
            .SetupSequence(r => r.GetCountAsync(
                It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(9)   // available orders
            .ReturnsAsync(4);  // active orders
        _orderRepository
            .Setup(r => r.CountCompletedForEmployeeWindowsAsync(
                CallerEmployeeId,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompletedOrderWindowCounts(ThisMonth: 7, LastMonth: 5, Today: 1, Week: 3));
        _orderRepository
            .Setup(r => r.GetCompletedOrdersInEitherRangeAsync(
                CallerEmployeeId,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
        _orderRepository
            .Setup(r => r.GetAverageRatingForEmployeeAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((4.5, 12));

        _orderEmployeePayRepository
            .Setup(r => r.SumPendingEarningsAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(750m);
        _orderEmployeePayRepository
            .Setup(r => r.GetTotalPayByOrderIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        _currencyResolutionService
            .Setup(s => s.ResolveCurrencyCodeForEmployeeAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZK");
    }

    private GetDashboardStats.Handler CreateHandler() =>
        (GetDashboardStats.Handler)Activator.CreateInstance(
            typeof(GetDashboardStats.Handler),
            _orderRepository.Object,
            _employeeInvoiceRepository.Object,
            _orderEmployeePayRepository.Object,
            _payConfigRepository.Object,
            _payPeriodRepository.Object,
            _orderAccessService.Object,
            _currencyResolutionService.Object,
            _session.Object)!;

    private static Order CompletedOrder(string orderId, DateTime completedAtUtc)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = orderId });
        typeof(Order).GetProperty(nameof(Order.CompletedAt))!.SetValue(order, completedAtUtc);
        return order;
    }

    [Fact]
    public async Task Dto_Fields_Map_The_Batched_Counts_And_Aggregates()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStats.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(9, dto.AvailableOrdersCount);
        Assert.Equal(4, dto.MyActiveOrdersCount);
        Assert.Equal(7, dto.ThisMonthCompletedOrders);
        Assert.Equal(5, dto.LastMonthCompletedOrders);
        Assert.Equal(1, dto.TodayCompletedCount);
        Assert.Equal(3, dto.WeekCompletedCount);
        Assert.Equal(750m, dto.CurrentPeriodEarnings);
        Assert.Null(dto.CurrentPayPeriodStart);
        Assert.Null(dto.CurrentPayPeriodEnd);
        Assert.Null(dto.NextPayoutDate);
        Assert.Equal(4.5, dto.AverageRating);
        Assert.Equal(12, dto.RatingCount);
        Assert.Null(dto.LatestInvoiceStatus);
        Assert.Equal("CZK", dto.CurrencyCode);
    }

    [Fact]
    public async Task Earnings_Windows_Are_Partitioned_From_The_Single_Fetch()
    {
        // UTC session (GetTimeZoneId → null) so the test's window math equals the handler's:
        // an order completed mid-today lands in today AND this week; one completed mid-last-month
        // lands only in the last-month window.
        var todayNoonUtc = DateTime.UtcNow.Date.AddHours(12);
        var lastMonthMidUtc = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 12, 0, 0, DateTimeKind.Utc).AddMonths(-1).AddDays(14);

        var todayOrder = CompletedOrder("dash-today", todayNoonUtc);
        var lastMonthOrder = CompletedOrder("dash-last-month", lastMonthMidUtc);
        _orderRepository
            .Setup(r => r.GetCompletedOrdersInEitherRangeAsync(
                CallerEmployeeId,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { todayOrder, lastMonthOrder });
        _orderEmployeePayRepository
            .Setup(r => r.GetTotalPayByOrderIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>
            {
                ["dash-today"] = 500m,
                ["dash-last-month"] = 300m,
            });
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStats.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(500m, dto.TodayEarnings);
        Assert.Equal(500m, dto.WeekEarnings);
        Assert.Equal(300m, dto.LastMonthEarnings);
    }

    [Fact]
    public async Task Legacy_Per_Window_Round_Trips_Are_Not_Used()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStats.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.CountCompletedForEmployeeWindowsAsync(
            CallerEmployeeId,
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _orderRepository.Verify(r => r.GetCompletedOrdersInEitherRangeAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _orderRepository.Verify(r => r.CountCompletedForEmployeeBetweenAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NonAdmin_With_No_Resolvable_Employee_Returns_EmployeeNotFound()
    {
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStats.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
    }
}
