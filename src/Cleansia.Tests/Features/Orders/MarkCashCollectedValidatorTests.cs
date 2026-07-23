using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// MarkCashCollected lets the assigned cleaner record that they collected the cash owed for a CASH order.
/// It is gated so that only an Approved, assigned cleaner may collect, only CASH orders qualify, and it is
/// idempotent (an already-Paid order can't be re-collected).
/// </summary>
public class MarkCashCollectedValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly MarkCashCollected.Validator _validator;

    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    public MarkCashCollectedValidatorTests()
    {
        _validator = new MarkCashCollected.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object);
    }

    [Fact]
    public async Task When_All_Rules_Pass_Then_Valid()
    {
        Arrange(PaymentType.Cash, PaymentStatus.Pending, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_OrderId_Empty_Then_Required()
    {
        Arrange(PaymentType.Cash, PaymentStatus.Pending, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task When_Order_Not_Found_Then_OrderNotFound()
    {
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotFound);
    }

    [Fact]
    public async Task When_Order_Is_Card_Payment_Then_OrderNotCashPayment()
    {
        Arrange(PaymentType.Card, PaymentStatus.Pending, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotCashPayment);
    }

    [Fact]
    public async Task When_Cash_Already_Collected_Then_OrderCashAlreadyCollected()
    {
        Arrange(PaymentType.Cash, PaymentStatus.Paid, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashAlreadyCollected);
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Pending)]
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        Arrange(PaymentType.Cash, PaymentStatus.Pending, status, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
    }

    [Fact]
    public async Task When_Cleaner_Not_Assigned_Then_EmployeeNotAssignedToOrder()
    {
        Arrange(PaymentType.Cash, PaymentStatus.Pending, ContractStatus.Approved, assigned: false);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotAssignedToOrder);
    }

    private void Arrange(PaymentType paymentType, PaymentStatus paymentStatus, ContractStatus employeeStatus, bool assigned)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, assigned ? EmployeeId : "other-emp", paymentType, paymentStatus);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
