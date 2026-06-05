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
/// T-0109 (EMP-GAP-01): StartOrder previously had NO ContractStatus gate, so a
/// rejected cleaner already assigned to a Confirmed order could start it. These
/// cases add the same approval gate used by Take / Complete.
/// </summary>
public class StartOrderApprovalGateTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly StartOrder.Validator _validator;

    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    public StartOrderApprovalGateTests()
    {
        _validator = new StartOrder.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object);
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]   // AC2: rejected cleaner cannot start
    [InlineData(ContractStatus.Pending)]    // AC4
    [InlineData(ContractStatus.Terminated)] // AC4
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        ArrangeStartableOrder(employeeStatus: status);

        var result = await _validator.ValidateAsync(new StartOrder.Command(OrderId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
    }

    [Fact]
    public async Task When_Cleaner_Approved_And_All_Rules_Pass_Then_Valid()
    {
        // AC5: approved + assigned + no in-progress conflict → passes.
        ArrangeStartableOrder(employeeStatus: ContractStatus.Approved);

        var result = await _validator.ValidateAsync(new StartOrder.Command(OrderId));

        Assert.True(result.IsValid);
    }

    private void ArrangeStartableOrder(ContractStatus employeeStatus)
    {
        // Confirmed order already assigned to this cleaner (only one order
        // total, so no other in-progress order exists).
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, EmployeeId);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
