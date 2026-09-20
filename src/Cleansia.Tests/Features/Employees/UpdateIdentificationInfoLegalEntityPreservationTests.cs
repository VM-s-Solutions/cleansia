using System.Reflection;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The mobile partial save owes the same preservation as the full profile save: a cleaner whose row an
/// operator set to a company can only send <c>NaturalPerson</c>, and that must not demote the row.
/// The handler is internal and no project has InternalsVisibleTo, so it is built the way
/// <c>CompanyVatLeverTests</c> builds its own.
/// </summary>
public class UpdateIdentificationInfoLegalEntityPreservationTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string CompanyName = "Uklid s.r.o.";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private Employee ArrangeStored(EmployeeEntityType entityType, string? legalEntityName)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";
        employee.UpdateBusinessIdentity(entityType, "87654321", legalEntityName);

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        return employee;
    }

    private async Task<BusinessResult<UpdateIdentificationInfo.Response>> HandleAsync(EmployeeEntityType entityType, string? legalEntityName)
    {
        var handler = Activator.CreateInstance(
            typeof(UpdateIdentificationInfo).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _employeeRepository.Object, _session.Object)!;
        var command = new UpdateIdentificationInfo.Command(
            EmployeeId: "emp-1",
            NationalityId: "cz",
            PassportId: "AB12345",
            EntityType: entityType,
            BusinessCountryId: "cz",
            RegistrationNumber: "12345678",
            LegalEntityName: legalEntityName);

        return await (Task<BusinessResult<UpdateIdentificationInfo.Response>>)handler.GetType()
            .GetMethod("Handle")!.Invoke(handler, [command, CancellationToken.None])!;
    }

    [Fact]
    public async Task A_Company_Row_Keeps_Its_Type_And_Name_When_The_Cleaner_Saves_As_A_Natural_Person()
    {
        var employee = ArrangeStored(EmployeeEntityType.LegalEntity, CompanyName);

        var result = await HandleAsync(EmployeeEntityType.NaturalPerson, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.LegalEntity, employee.EntityType);
        Assert.Equal(CompanyName, employee.LegalEntityName);
        Assert.Equal("12345678", employee.RegistrationNumber);
        Assert.Equal("AB12345", employee.PassportId);
        Assert.Equal("cz", employee.NationalityId);
    }

    [Fact]
    public async Task A_Company_Row_Keeps_Its_Stored_Name_Whatever_Name_The_Command_Carries()
    {
        var employee = ArrangeStored(EmployeeEntityType.LegalEntity, CompanyName);

        var result = await HandleAsync(EmployeeEntityType.LegalEntity, "Something Else s.r.o.");

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.LegalEntity, employee.EntityType);
        Assert.Equal(CompanyName, employee.LegalEntityName);
    }

    [Fact]
    public async Task A_Natural_Person_Row_Is_Written_From_The_Command()
    {
        var employee = ArrangeStored(EmployeeEntityType.NaturalPerson, null);

        var result = await HandleAsync(EmployeeEntityType.NaturalPerson, "ignored");

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.NaturalPerson, employee.EntityType);
        Assert.Null(employee.LegalEntityName);
        Assert.Equal("12345678", employee.RegistrationNumber);
    }
}
