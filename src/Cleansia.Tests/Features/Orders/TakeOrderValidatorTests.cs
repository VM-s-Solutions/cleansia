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

/// <summary>
/// The order-action gate must let only an
/// <see cref="ContractStatus.Approved"/> cleaner take an order. A cleaner
/// the admin rejected (or who is still pending / terminated) must be turned
/// away with <see cref="BusinessErrorMessage.EmployeeNotApproved"/> and no
/// assignment created.
/// </summary>
public class TakeOrderValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly TakeOrder.Validator _validator;

    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    public TakeOrderValidatorTests()
    {
        _validator = new TakeOrder.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object);
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]   // rejected cleaner cannot take
    [InlineData(ContractStatus.Pending)]    // pending cleaner cannot take
    [InlineData(ContractStatus.Terminated)] // terminated cleaner cannot take
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        ArrangeTakeableOrder(employeeStatus: status);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
        // The approval failure must NOT masquerade as documents_missing.
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeDocumentsMissing);
    }

    [Fact]
    public async Task When_Cleaner_Approved_And_All_Rules_Pass_Then_Valid()
    {
        // approved cleaner satisfying every existing rule still passes.
        ArrangeTakeableOrder(employeeStatus: ContractStatus.Approved);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        Assert.True(result.IsValid);
    }

    private void ArrangeTakeableOrder(ContractStatus employeeStatus)
    {
        // Confirmed order with an open spot, NOT yet assigned to this cleaner.
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetEmployeeOrderCountThisWeekAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                EmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
