using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;

namespace Cleansia.Tests.Features.Employees;

public class EmployeeMappersUserIdTests
{
    [Fact]
    public void MapToAdminDetailDto_Surfaces_Audited_UserId()
    {
        var user = UserMockFactory.Generate();
        user.Id = "usr-audited-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        var dto = employee.MapToAdminDetailDto();

        Assert.Equal("usr-audited-1", dto.UserId);
    }

    [Fact]
    public void MapToAdminDetailDto_UserId_Is_The_User_Id_Not_The_Employee_Id()
    {
        var user = UserMockFactory.Generate();
        user.Id = "usr-audited-2";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-2";

        var dto = employee.MapToAdminDetailDto();

        Assert.Equal(user.Id, dto.UserId);
        Assert.NotEqual(employee.Id, dto.UserId);
    }
}
