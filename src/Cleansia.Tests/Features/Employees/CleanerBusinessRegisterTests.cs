using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.BusinessRegistry;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Owner ruling 2026-10-05: approval's register check, and the same check on an approved cleaner's changed
/// company ID (IČO), map one register answer to one refusal. Every other save refuses only a number the
/// register does not hold.
/// </summary>
public class CleanerBusinessRegisterTests
{
    private static readonly BusinessRegistryRecord InForce =
        new(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: true);

    private static readonly BusinessRegistryRecord Ceased =
        new(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true);

    private static readonly BusinessRegistryRecord NoTradeLicence =
        new(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false);

    private static readonly BusinessRegistryRecord CeasedWithoutTradeLicence =
        new(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: false);

    public static TheoryData<BusinessRegistryRecord, bool, string?> Answers => new()
    {
        { BusinessRegistryRecord.NotConsulted, false, null },
        { BusinessRegistryRecord.NotConsulted, true, null },
        { InForce, false, null },
        { InForce, true, null },
        { BusinessRegistryRecord.NotRegistered, false, BusinessErrorMessage.RegistrationNumberNotRegistered },
        { BusinessRegistryRecord.NotRegistered, true, BusinessErrorMessage.RegistrationNumberNotRegistered },
        { BusinessRegistryRecord.Unavailable, false, null },
        { BusinessRegistryRecord.Unavailable, true, BusinessErrorMessage.EmployeeBusinessRegistryUnavailable },
        { Ceased, false, null },
        { Ceased, true, BusinessErrorMessage.EmployeeBusinessCeased },
        { NoTradeLicence, false, null },
        { NoTradeLicence, true, BusinessErrorMessage.EmployeeTradeLicenceInactive },
        { CeasedWithoutTradeLicence, false, null },
        { CeasedWithoutTradeLicence, true, BusinessErrorMessage.EmployeeBusinessCeased },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public void Each_Register_Answer_Maps_To_One_Refusal_Or_None(
        BusinessRegistryRecord record, bool approvalGrade, string? refusal)
    {
        Assert.Equal(refusal, CleanerBusinessRegister.Refusal(record, approvalGrade));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("27082440", "27082440", false)]
    [InlineData("27082440", " 27082440 ", false)]
    [InlineData(" 27082440", "27082440", false)]
    [InlineData("27082440", "12345678", true)]
    [InlineData(null, "27082440", true)]
    [InlineData("27082440", "", true)]
    [InlineData(null, "", true)]
    public void Only_A_Different_Number_After_Trimming_Is_A_Change(
        string? stored, string? sent, bool changes)
    {
        var employee = Employee.CreateWithUser(User.CreateWithPassword("ico@cleansia.test", "Password1", "Jana", "Nováková"));
        employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, stored, legalEntityName: null);

        Assert.Equal(changes, CleanerBusinessRegister.Changes(employee, sent));
    }
}
