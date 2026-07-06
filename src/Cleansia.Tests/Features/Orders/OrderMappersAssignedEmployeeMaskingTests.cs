using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Pins the customer-caller masking of the assigned cleaner's personal data on order detail:
/// a customer sees the cleaner's FIRST NAME ONLY and NO phone number (mirrors GetOrderPhotos'
/// CapturedByEmployeeName masking), while partner/admin callers keep the full name + phone.
/// The DTO shape is shared across callers — only the values differ.
/// </summary>
public class OrderMappersAssignedEmployeeMaskingTests
{
    private const string FirstName = "Jana";
    private const string LastName = "Novakova";
    private const string Phone = "+420777123456";

    private static OrderEmployee BuildAssignment(Order order)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            FirstName = FirstName,
            LastName = LastName,
            PhoneNumber = Phone,
        });
        return OrderEmployee.Create(order, Employee.CreateWithUser(user));
    }

    [Fact]
    public void Customer_Caller_Gets_First_Name_Only_And_No_Phone()
    {
        var assignment = BuildAssignment(OrderMockFactory.Generate());

        var dto = assignment.MapToAssignedEmployeeDto(isCustomerCaller: true);

        Assert.Equal(FirstName, dto.FullName);
        Assert.Null(dto.PhoneNumber);
        Assert.Equal(assignment.Id, dto.Id);
        Assert.Equal(assignment.EmployeeId, dto.EmployeeId);
    }

    [Fact]
    public void Non_Customer_Caller_Keeps_Full_Name_And_Phone()
    {
        var assignment = BuildAssignment(OrderMockFactory.Generate());

        var dto = assignment.MapToAssignedEmployeeDto();

        Assert.Equal($"{FirstName} {LastName}", dto.FullName);
        Assert.Equal(Phone, dto.PhoneNumber);
    }

    [Fact]
    public void MapToDetail_For_Customer_Caller_Masks_Assigned_Employees()
    {
        var order = OrderMockFactory.Generate();
        order.AddAssignedEmployee(BuildAssignment(order));

        var detail = order.MapToDetail(isCustomerCaller: true);

        var employee = Assert.Single(detail.AssignedEmployees);
        Assert.Equal(FirstName, employee.FullName);
        Assert.Null(employee.PhoneNumber);
    }

    [Fact]
    public void MapToDetail_Default_Keeps_Full_Name_And_Phone()
    {
        var order = OrderMockFactory.Generate();
        order.AddAssignedEmployee(BuildAssignment(order));

        var detail = order.MapToDetail();

        var employee = Assert.Single(detail.AssignedEmployees);
        Assert.Equal($"{FirstName} {LastName}", employee.FullName);
        Assert.Equal(Phone, employee.PhoneNumber);
    }
}
