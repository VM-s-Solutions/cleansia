using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

public class StartOrderValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly StartOrder.Validator _validator;

    public StartOrderValidatorTests()
    {
        _validator = new StartOrder.Validator(_orderRepository.Object, _accessService.Object);
    }

    [Fact]
    public async Task When_OrderId_Empty_Then_Required()
    {
        var result = await _validator.ValidateAsync(new StartOrder.Command(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(StartOrder.Command.OrderId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task When_Order_Does_Not_Exist_Then_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.ExistsAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _validator.ValidateAsync(new StartOrder.Command("missing"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotFound);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    public async Task When_CurrentStatus_Confirmed_Or_OnTheWay_And_Employee_Assigned_Then_Valid(OrderStatus status)
    {
        const string orderId = "order-1";
        const string employeeId = "emp-1";

        var order = ValidatorTestHelpers.BuildOrder(orderId, status, employeeId);

        _orderRepository.Setup(r => r.ExistsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var result = await _validator.ValidateAsync(new StartOrder.Command(orderId));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task When_CurrentStatus_NotConfirmed_Or_OnTheWay_Then_OrderNotConfirmed(OrderStatus status)
    {
        const string orderId = "order-1";
        const string employeeId = "emp-1";

        var order = ValidatorTestHelpers.BuildOrder(orderId, status, employeeId);

        _orderRepository.Setup(r => r.ExistsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var result = await _validator.ValidateAsync(new StartOrder.Command(orderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotConfirmed);
    }

    [Fact]
    public async Task When_Employee_HasOtherOrderInProgress_Then_EmployeeAlreadyHasOrderInProgress()
    {
        const string targetOrderId = "order-target";
        const string otherInProgressOrderId = "order-other";
        const string employeeId = "emp-1";

        // Target order is Confirmed + assigned to this employee → first three rules pass.
        var target = ValidatorTestHelpers.BuildOrder(targetOrderId, OrderStatus.Confirmed, employeeId);
        // Sibling order: also assigned to this employee, status InProgress → fails the "no other in-progress" rule.
        var sibling = ValidatorTestHelpers.BuildOrder(otherInProgressOrderId, OrderStatus.InProgress, employeeId);

        _orderRepository.Setup(r => r.ExistsAsync(targetOrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { target, sibling }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var result = await _validator.ValidateAsync(new StartOrder.Command(targetOrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeAlreadyHasOrderInProgress);
    }
}
