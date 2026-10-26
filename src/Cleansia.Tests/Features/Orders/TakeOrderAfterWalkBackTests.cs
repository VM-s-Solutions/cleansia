using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
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
/// An order walked back to New is taken again as a second <c>New → Confirmed</c>. Its customer was told the
/// booking is being offered to cleaners again, so the re-take's e-mail answers that notice — "We've found you
/// a new cleaner" — instead of repeating the first take's "Cleaner found" (owner decision 2026-10-25). The
/// push is the usual "Cleaner found!", once per assignment.
/// </summary>
public class TakeOrderAfterWalkBackTests
{
    private const string OrderId = "order-retake-1";
    private const string LeaverId = "emp-retake-left";
    private const string TakerId = "emp-retake-new";
    private const string CustomerUserId = "user-retake-customer";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IWorkContractAcceptor> _workContractAcceptor = new();

    [Fact]
    public async Task Taking_A_Walked_Back_Order_Sends_The_New_Cleaner_Email()
    {
        var order = WalkedBack(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.Confirmed, maxEmployees: 1));
        Arrange(order, TakerId);

        var result = await Take();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.New, OrderStatus.Confirmed],
            order.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Status));
        // A guest booking, so the e-mail carries its own credential: a re-take after a walk-back is a second
        // issuance and must not be a dead link either.
        _emailService.Verify(
            e => e.SendOrderStatusUpdateEmailAsync(
                order.CustomerEmail!, order, "NewCleaner", It.IsAny<string>(), It.IsAny<CancellationToken>(), null,
                It.Is<string?>(token => !string.IsNullOrEmpty(token))),
            Times.Once);
        VerifyNoStatusEmail("Confirmed");
    }

    [Fact]
    public async Task Taking_A_New_Order_For_The_First_Time_Sends_The_Confirmed_Email()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 1);
        Arrange(order, TakerId);

        var result = await Take();

        Assert.True(result.IsSuccess);
        _emailService.Verify(
            e => e.SendOrderStatusUpdateEmailAsync(
                order.CustomerEmail!, order, "Confirmed", It.IsAny<string>(), It.IsAny<CancellationToken>(), null,
                It.IsAny<string?>()),
            Times.Once);
        VerifyNoStatusEmail("NewCleaner");
    }

    [Fact]
    public async Task Every_Take_After_A_Walk_Back_Sends_The_New_Cleaner_Email()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 1);
        foreach (var cleaner in new[] { "emp-retake-a", "emp-retake-b", TakerId })
        {
            Arrange(order, cleaner);
            _emailService.Invocations.Clear();

            Assert.True((await Take()).IsSuccess);

            var sent = Assert.Single(_emailService.Invocations);
            Assert.Equal(cleaner == "emp-retake-a" ? "Confirmed" : "NewCleaner", sent.Arguments[2]);
            if (cleaner != TakerId)
            {
                WalkedBack(order, cleaner);
            }
        }

        Assert.Equal(
            [
                OrderStatus.New, OrderStatus.Confirmed, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.New,
                OrderStatus.Confirmed,
            ],
            order.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Status));
    }

    [Fact]
    public async Task A_Re_Take_Of_An_Account_Booking_Pushes_Cleaner_Found_Once_For_The_New_Assignment()
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Confirmed, LeaverId, maxEmployees: 1, userId: CustomerUserId);
        WalkedBack(order, LeaverId);
        Arrange(order, TakerId);

        Assert.True((await Take()).IsSuccess);

        var taken = Assert.Single(order.AssignedEmployees);
        _notificationProducer.Verify(n => n.NotifyAsync(
                CustomerUserId,
                NotificationEventCatalog.OrderCleanerAssigned,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                AssignmentNotificationSubject.For(OrderId, taken.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationProducer.Verify(n => n.NotifyAsync(
                It.IsAny<string>(),
                NotificationEventCatalog.OrderCleanerAssigned,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _emailService.Verify(
            e => e.SendOrderStatusUpdateEmailAsync(
                order.CustomerEmail!, order, "NewCleaner", It.IsAny<string>(), It.IsAny<CancellationToken>(), null,
                null),
            Times.Once);
    }

    private static Order WalkedBack(Order order, string leaverId = LeaverId)
    {
        if (order.AssignedEmployees.All(a => a.EmployeeId != leaverId))
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(leaverId, ContractStatus.Approved)));
        }

        order.UnassignEmployee(leaverId);
        Assert.True(order.ReturnToBoardIfUnstaffed());
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        return order;
    }

    private void VerifyNoStatusEmail(string status) =>
        _emailService.Verify(
            e => e.SendOrderStatusUpdateEmailAsync(
                It.IsAny<string>(), It.IsAny<Order>(), status, It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<decimal?>(), It.IsAny<string?>()),
            Times.Never);

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<TakeOrder.Response>> Take() =>
        CreateHandler().Handle(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

    private void Arrange(Order order, string takerId)
    {
        var taker = ValidatorTestHelpers.BuildEmployee(takerId, ContractStatus.Approved);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _employeeRepository.Setup(r => r.GetByIdAsync(takerId, It.IsAny<CancellationToken>())).ReturnsAsync(taker);
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(takerId);
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
