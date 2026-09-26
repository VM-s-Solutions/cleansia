using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;
using Cleansia.TestUtilities;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The accepted consequence of the walk-back, pinned rather than suppressed: an order returned to New
/// is taken again as a <c>New → Confirmed</c> transition, so the customer receives a second "your
/// order is confirmed" e-mail. A new cleaner IS a new confirmation and the e-mail is true when it is
/// sent; hiding it to avoid explaining the earlier one is the alternative this deliberately does not
/// take (the customer message on the walk-back itself is the owner's open question).
/// </summary>
public class TakeOrderAfterWalkBackTests
{
    private const string OrderId = "order-retake-1";
    private const string LeaverId = "emp-retake-left";
    private const string TakerId = "emp-retake-new";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();

    private readonly Mock<IWorkContractAcceptor> _workContractAcceptor = new();
    [Fact]
    public async Task Taking_A_Walked_Back_Order_Confirms_It_Again_And_Sends_The_Confirmed_Email_Again()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.Confirmed, maxEmployees: 1);
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(LeaverId, ContractStatus.Approved)));
        order.UnassignEmployee(LeaverId);
        Assert.True(order.ReturnToBoardIfUnstaffed());
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Arrange(order);

        var result = await CreateHandler().Handle(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.New, OrderStatus.Confirmed],
            order.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Status));
        // A guest booking, so the "a cleaner has taken your job" e-mail carries its own credential —
        // a re-take after a walk-back is a second issuance and must not be a dead link either.
        _emailService.Verify(
            e => e.SendOrderStatusUpdateEmailAsync(
                order.CustomerEmail!, order, "Confirmed", It.IsAny<string>(), It.IsAny<CancellationToken>(), null,
                It.Is<string?>(token => !string.IsNullOrEmpty(token))),
            Times.Once);
    }

    private void Arrange(Order order)
    {
        var taker = ValidatorTestHelpers.BuildEmployee(TakerId, ContractStatus.Approved);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _employeeRepository.Setup(r => r.GetByIdAsync(TakerId, It.IsAny<CancellationToken>())).ReturnsAsync(taker);
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TakerId);
    }

    private TakeOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            _notificationProducer.Object,
            _emailService.Object,
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            _workContractAcceptor.Object,
            NullLogger<TakeOrder.Handler>.Instance);
}
