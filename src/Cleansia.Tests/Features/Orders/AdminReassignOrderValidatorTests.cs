using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// An admin placement meets the gates a cleaner meets on the partner side: the target is approved,
/// works in the order's market and has no other job overlapping it. Without them a pending cleaner
/// or one from another country could be put on a job that then stalls at start.
/// </summary>
public class AdminReassignOrderValidatorTests
{
    private const string OrderId = "order-placement-1";
    private const string TargetId = "emp-target";
    private const string Market = "country-cz";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Order _order;

    public AdminReassignOrderValidatorTests()
    {
        _order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", Market),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "owner-user");
        _order.Id = OrderId;
        _order.SetMaxEmployees(2);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(() => new[] { _order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private AdminReassignOrder.Validator Validator() => new(_orderRepository.Object, _employeeRepository.Object);

    private static AdminReassignOrder.Command Placement() => new(OrderId, FromEmployeeId: null, TargetId);

    private Employee ArrangeTarget(ContractStatus status = ContractStatus.Approved, string? workCountryId = Market)
    {
        var user = User.CreateWithPassword(TargetId + "@x.test", "x", "Emp", "Loyee");
        user.Id = TargetId + "-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = TargetId;
        employee.UpdateContractStatus(status);
        if (workCountryId is not null)
        {
            employee.AssignWorkCountry(workCountryId);
        }

        _employeeRepository
            .Setup(r => r.GetByIdAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        return employee;
    }

    private async Task<string> SingleErrorAsync()
    {
        var result = await Validator().ValidateAsync(Placement());
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(AdminReassignOrder.Command.ToEmployeeId), error.PropertyName);
        return error.ErrorMessage;
    }

    [Fact]
    public async Task An_Approved_Cleaner_Of_The_Market_Who_Is_Free_Passes()
    {
        ArrangeTarget();

        var result = await Validator().ValidateAsync(Placement());

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Missing_Cleaner_Is_Refused_As_Not_Found()
    {
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, await SingleErrorAsync());
    }

    [Theory]
    [InlineData(ContractStatus.Pending)]
    [InlineData(ContractStatus.Rejected)]
    [InlineData(ContractStatus.Terminated)]
    [InlineData(ContractStatus.Active)]
    public async Task A_Cleaner_Who_Is_Not_Approved_Is_Refused(ContractStatus status)
    {
        ArrangeTarget(status);

        Assert.Equal(BusinessErrorMessage.ReassignEmployeeNotApproved, await SingleErrorAsync());
    }

    [Fact]
    public async Task An_Approved_Cleaner_Who_Left_The_Platform_Is_Refused()
    {
        ArrangeTarget().Deactivated("gdpr-erasure", DateTimeOffset.UtcNow);

        Assert.Equal(BusinessErrorMessage.ReassignEmployeeNotApproved, await SingleErrorAsync());
    }

    [Fact]
    public async Task A_Cleaner_Working_In_Another_Market_Is_Refused()
    {
        ArrangeTarget(workCountryId: "country-sk");

        Assert.Equal(BusinessErrorMessage.ReassignEmployeeOtherMarket, await SingleErrorAsync());
    }

    [Fact]
    public async Task A_Cleaner_With_No_Work_Country_Is_Refused()
    {
        ArrangeTarget(workCountryId: null);

        Assert.Equal(BusinessErrorMessage.ReassignEmployeeOtherMarket, await SingleErrorAsync());
    }

    [Fact]
    public async Task A_Cleaner_With_An_Overlapping_Job_Is_Refused()
    {
        ArrangeTarget();
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                TargetId, _order.CleaningDateTime, _order.EstimatedTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Assert.Equal(BusinessErrorMessage.ReassignEmployeeBusy, await SingleErrorAsync());
    }
}
