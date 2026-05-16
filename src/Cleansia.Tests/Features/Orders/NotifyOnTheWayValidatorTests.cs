using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

public class NotifyOnTheWayValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly NotifyOnTheWay.Validator _validator;

    public NotifyOnTheWayValidatorTests()
    {
        _validator = new NotifyOnTheWay.Validator(_orderRepository.Object, _accessService.Object);
    }

    [Fact]
    public async Task When_OrderId_Empty_Then_Required()
    {
        var result = await _validator.ValidateAsync(new NotifyOnTheWay.Command(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(NotifyOnTheWay.Command.OrderId)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task When_Order_Does_Not_Exist_Then_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.ExistsAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _validator.ValidateAsync(new NotifyOnTheWay.Command("missing"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotFound);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task When_CurrentStatus_NotConfirmed_Then_OrderNotConfirmed(OrderStatus currentStatus)
    {
        const string orderId = "order-1";
        const string employeeId = "emp-1";

        var order = ValidatorTestHelpers.BuildOrder(orderId, currentStatus, employeeId);

        _orderRepository.Setup(r => r.ExistsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var result = await _validator.ValidateAsync(new NotifyOnTheWay.Command(orderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotConfirmed);
    }

    [Fact]
    public async Task When_CurrentStatus_Confirmed_And_Employee_Assigned_Then_Valid()
    {
        const string orderId = "order-1";
        const string employeeId = "emp-1";

        var order = ValidatorTestHelpers.BuildOrder(orderId, OrderStatus.Confirmed, employeeId);

        _orderRepository.Setup(r => r.ExistsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

        var result = await _validator.ValidateAsync(new NotifyOnTheWay.Command(orderId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_Employee_NotAssigned_Then_EmployeeNotAssignedToOrder()
    {
        const string orderId = "order-1";
        const string assignedEmployeeId = "emp-1";
        const string callerEmployeeId = "emp-2";

        var order = ValidatorTestHelpers.BuildOrder(orderId, OrderStatus.Confirmed, assignedEmployeeId);

        _orderRepository.Setup(r => r.ExistsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(callerEmployeeId);

        var result = await _validator.ValidateAsync(new NotifyOnTheWay.Command(orderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
    }
}
