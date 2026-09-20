using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// A cleaner's own profile save cannot name <c>LegalEntity</c> (owner ruling 2026-09-20), so a cleaner
/// whose row an operator set to a company can only send <c>NaturalPerson</c> — and the save must not
/// read that as a demotion. The stored pair (entity type, legal entity name) survives the save
/// whatever the command carries; a natural person's row is written from the command as before.
/// </summary>
public class UpdateEmployeeLegalEntityPreservationTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string CompanyName = "Uklid s.r.o.";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private UpdateEmployee.Handler CreateHandler() => new(
        _employeeRepository.Object,
        new Mock<IEmployeeDocumentRepository>().Object,
        _session.Object,
        new Mock<IBlobContainerClientFactory>().Object,
        new Mock<IAddressGeocoder>().Object,
        new Mock<IConsentService>().Object);

    private Employee ArrangeStored(EmployeeEntityType entityType, string? legalEntityName)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.Id = "user-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";
        employee.UpdateBusinessIdentity(entityType, "87654321", legalEntityName);

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        return employee;
    }

    private static UpdateEmployee.Command Command(EmployeeEntityType entityType, string? legalEntityName) => new(
        EmployeeId: "emp-1",
        FirstName: "First",
        LastName: "Last",
        BirthDate: new DateOnly(1990, 1, 1),
        Street: "Main Street 10",
        City: "Prague",
        ZipCode: "11000",
        CountryId: "cz",
        State: null,
        NationalityId: "cz",
        Phone: "+420123456789",
        PassportId: "AB12345",
        EntityType: entityType,
        RegistrationNumber: "12345678",
        LegalEntityName: legalEntityName,
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: true);

    [Fact]
    public async Task A_Company_Row_Keeps_Its_Type_And_Name_When_The_Cleaner_Saves_As_A_Natural_Person()
    {
        var employee = ArrangeStored(EmployeeEntityType.LegalEntity, CompanyName);

        var result = await CreateHandler().Handle(Command(EmployeeEntityType.NaturalPerson, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.LegalEntity, employee.EntityType);
        Assert.Equal(CompanyName, employee.LegalEntityName);
        Assert.Equal("12345678", employee.RegistrationNumber);
        Assert.Equal("AB12345", employee.PassportId);
    }

    [Fact]
    public async Task A_Company_Row_Keeps_Its_Stored_Name_Whatever_Name_The_Command_Carries()
    {
        var employee = ArrangeStored(EmployeeEntityType.LegalEntity, CompanyName);

        var result = await CreateHandler().Handle(Command(EmployeeEntityType.LegalEntity, "Something Else s.r.o."), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.LegalEntity, employee.EntityType);
        Assert.Equal(CompanyName, employee.LegalEntityName);
    }

    [Fact]
    public async Task A_Natural_Person_Row_Is_Written_From_The_Command()
    {
        var employee = ArrangeStored(EmployeeEntityType.NaturalPerson, null);

        var result = await CreateHandler().Handle(Command(EmployeeEntityType.NaturalPerson, "ignored"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeEntityType.NaturalPerson, employee.EntityType);
        Assert.Null(employee.LegalEntityName);
        Assert.Equal("12345678", employee.RegistrationNumber);
    }
}
