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
/// T-0109 (EMP-GAP-01): CompleteOrder's "approval" gate was the misnamed
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
    [InlineData(ContractStatus.Rejected)]   // AC3: rejected cleaner cannot complete
    [InlineData(ContractStatus.Pending)]    // AC4
    [InlineData(ContractStatus.Terminated)] // AC4
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        ArrangeCompletableOrder(employeeStatus: status);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
        // The approval failure must NOT masquerade as documents_missing (AC6).
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeDocumentsMissing);
    }

    [Fact]
    public async Task When_Cleaner_Approved_And_All_Rules_Pass_Then_Valid()
    {
        // AC5: approved + assigned + after photos present + profile complete.
        ArrangeCompletableOrder(employeeStatus: ContractStatus.Approved);

        var result = await _validator.ValidateAsync(new CompleteOrder.Command(OrderId));

        Assert.True(result.IsValid);
    }

    private void ArrangeCompletableOrder(ContractStatus employeeStatus)
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, EmployeeId);
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
