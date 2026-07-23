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
/// CompleteOrder's "approval" gate was the misnamed
/// <c>HasUploadedDocumentsAsync</c> (only checked != Pending), so a rejected
/// cleaner assigned to an InProgress order could complete it (triggering
/// receipt / loyalty / pay). These cases assert the honest == Approved gate.
/// </summary>
public class CompleteOrderValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly CompleteOrder.Validator _validator;

    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    public CompleteOrderValidatorTests()
    {
        _validator = new CompleteOrder.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _orderPhotoRepository.Object,
            _accessService.Object);
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]   // rejected cleaner cannot complete
    [InlineData(ContractStatus.Pending)]
    [InlineData(ContractStatus.Terminated)]
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        ArrangeCompletableOrder(employeeStatus: status);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
        // The approval failure must NOT masquerade as documents_missing.
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeDocumentsMissing);
    }

    [Fact]
    public async Task When_Cleaner_Approved_And_All_Rules_Pass_Then_Valid()
    {
        // approved + assigned + after photos present + profile complete + payment settled.
        ArrangeCompletableOrder(employeeStatus: ContractStatus.Approved);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_Cash_Order_Not_Collected_Then_OrderCashNotCollected()
    {
        // Everything else valid, but the cash hasn't been collected yet (PaymentStatus.Pending) —
        // the cleaner must mark it collected first. This is the core money-safety gate.
        ArrangeCompletableOrder(ContractStatus.Approved, PaymentType.Cash, PaymentStatus.Pending);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashNotCollected);
    }

    [Fact]
    public async Task When_Card_Order_Payment_Not_Confirmed_Then_OrderPaymentNotConfirmed()
    {
        // Card charge hasn't cleared (Stripe webhook not yet arrived) — completion is blocked with the
        // card-specific message, not the cash one.
        ArrangeCompletableOrder(ContractStatus.Approved, PaymentType.Card, PaymentStatus.Pending);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderPaymentNotConfirmed);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashNotCollected);
    }

    [Fact]
    public async Task When_Cash_Order_Collected_Then_Valid()
    {
        // A cash order whose money has been collected (Paid) completes normally.
        ArrangeCompletableOrder(ContractStatus.Approved, PaymentType.Cash, PaymentStatus.Paid);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid);
    }

    private void ArrangeCompletableOrder(
        ContractStatus employeeStatus,
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
    {
        // A completable order is payment-SETTLED by default (Paid): completion is now gated on payment,
        // so the "all rules pass" case must model a collected cash / confirmed card order.
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, EmployeeId, paymentType, paymentStatus);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
