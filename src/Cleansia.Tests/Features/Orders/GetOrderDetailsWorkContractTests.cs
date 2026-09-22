using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D4 (Verification #5) — the detail lists the acceptances of the CURRENT seats, keyed on the
/// seat so the client pairs each with its crew entry, with no name of its own; the crew and the customer
/// read it full, a browsing cleaner reads it empty.
/// </summary>
public sealed class GetOrderDetailsWorkContractTests
{
    private const string OrderId = "order-detail-wc-1";
    private const string CrewEmployeeId = "emp-detail-wc-crew";
    private const string BrowserEmployeeId = "emp-detail-wc-browser";

    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    private GetOrderDetails.Handler CreateHandler() =>
        new(
            _orderAccessService.Object,
            _userSessionProvider.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _orderPhotoRepository.Object,
            Mock.Of<IEmployeeRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<ITenantRepository>(),
            _expressWaiverConsumer.Object,
            Mock.Of<IUserMembershipRepository>(),
            _acceptanceRepository.Object);

    private (Order Order, OrderEmployee Seat) ArrangeEmployeeCaller(string caller, bool entitled)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = OrderId, CurrentStatus = OrderStatus.Confirmed });
        order.SetMaxEmployees(2);
        var user = User.CreateWithPassword("crew@example.com", "x", "Petra", "Svobodova");
        user.Id = $"{CrewEmployeeId}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = CrewEmployeeId;
        var seat = OrderEmployee.Create(order, employee);
        order.AddAssignedEmployee(seat);

        _orderAccessService.Setup(a => a.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderAccessService.Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderAccessService.Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(entitled);
        _orderAccessService.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _orderAccessService.Setup(a => a.IsCustomerCaller()).Returns(false);
        _userSessionProvider
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _orderEmployeePayRepository
            .Setup(r => r.GetByOrderAndEmployeeAsync(OrderId, caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.EmployeePayroll.OrderEmployeePay?)null);
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(It.IsAny<IEnumerable<string>>(), caller, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payConfigRepository
            .Setup(r => r.GetPackageConfigsForOrderAsync(It.IsAny<IEnumerable<string>>(), caller, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var row = new WorkContractAcceptanceRow(
            "01ACCEPT0000000000000000A1", OrderId, order.DisplayOrderNumber, seat.Id, CrewEmployeeId,
            WorkContractTestData.TextIdCs, WorkContractTestData.Version, "cs",
            new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero), "cleansia.mobile", "203.0.113.9", "Pixel 8", "d", "{}");
        _acceptanceRepository
            .Setup(r => r.GetForSeatsAsync(It.Is<IReadOnlyCollection<string>>(ids => ids.Single() == seat.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([row]);

        return (order, seat);
    }

    [Fact]
    public async Task The_Crew_Reads_The_Acceptances_Of_The_Current_Seats_Keyed_On_The_Seat_With_No_Name()
    {
        var (_, seat) = ArrangeEmployeeCaller(CrewEmployeeId, entitled: true);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var acceptance = Assert.Single(result.Value!.WorkContractAcceptances!);
        Assert.Equal("01ACCEPT0000000000000000A1", acceptance.Id);
        Assert.Equal(seat.Id, acceptance.OrderEmployeeId);
        Assert.Equal(CrewEmployeeId, acceptance.EmployeeId);
        Assert.Equal(WorkContractTestData.Version, acceptance.DocumentVersion);
        Assert.Equal("cs", acceptance.Language);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero), acceptance.AcceptedOn);
        Assert.DoesNotContain(typeof(Core.AppServices.Features.Orders.DTOs.WorkContractAcceptanceDto).GetProperties(),
            p => p.Name.Contains("Name", StringComparison.Ordinal));
        _acceptanceRepository.Verify(
            r => r.GetForSeatsAsync(It.Is<IReadOnlyCollection<string>>(ids => ids.Single() == seat.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Browsing_Cleaner_Reads_It_Empty()
    {
        ArrangeEmployeeCaller(BrowserEmployeeId, entitled: false);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!.WorkContractAcceptances!);
    }
}
