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
/// MarkCashCollected lets the assigned cleaner record a cash collection on ANY order that is not yet
/// settled — including a card order whose Stripe webhook never arrived, which otherwise could not be
/// completed in the field at all. It is gated so that only an Approved, assigned cleaner may collect,
/// only while the order is InProgress (the cleaner is on site — matching both mobile UIs), and it is
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

    // The point of the change: an unpaid CARD order (the Stripe webhook never landed) is now a legal
    // target, so the cleaner can settle it in cash and complete the job on site. The card-vs-cash
    // reconciliation against live Stripe happens in the handler, not here.
    [Fact]
    public async Task When_Order_Is_Unpaid_Card_Payment_Then_Valid()
    {
        Arrange(PaymentType.Card, PaymentStatus.Pending, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(PaymentType.Cash)]
    [InlineData(PaymentType.Card)]
    public async Task When_Already_Paid_Then_OrderCashAlreadyCollected(PaymentType paymentType)
    {
        Arrange(paymentType, PaymentStatus.Paid, ContractStatus.Approved, assigned: true);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashAlreadyCollected);
    }

    // Cash can only change hands while the cleaner is on site. The gate applies to cash orders too, so
    // the backend now enforces what both mobile UIs already only offered during InProgress.
    [Theory]
    [InlineData(PaymentType.Cash, OrderStatus.Confirmed)]
    [InlineData(PaymentType.Cash, OrderStatus.OnTheWay)]
    [InlineData(PaymentType.Cash, OrderStatus.Completed)]
    [InlineData(PaymentType.Card, OrderStatus.Confirmed)]
    [InlineData(PaymentType.Card, OrderStatus.OnTheWay)]
    public async Task When_Order_Not_In_Progress_Then_OrderNotInProgress(
        PaymentType paymentType, OrderStatus currentStatus)
    {
        Arrange(paymentType, PaymentStatus.Pending, ContractStatus.Approved, assigned: true, currentStatus);

        var result = await _validator.ValidateAsync(new MarkCashCollected.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderNotInProgress);
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

    private void Arrange(
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        ContractStatus employeeStatus,
        bool assigned,
        OrderStatus currentStatus = OrderStatus.InProgress)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, currentStatus, assigned ? EmployeeId : "other-emp", paymentType, paymentStatus);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
