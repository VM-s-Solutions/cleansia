using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Features.Orders;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The one writer of <see cref="Core.Domain.Users.Employee.WeeklyOrderLimit"/>.
///
/// <para>A narrow command exists because <see cref="AdminUpdateEmployee"/> merges every field with
/// <c>?? existing</c>, so null there means "not supplied" — it cannot express <i>clear the cap</i>,
/// which is half of what this is for. These cases pin both directions.</para>
/// </summary>
public class AdminSetEmployeeWeeklyOrderLimitTests
{
    private const string EmployeeId = "emp-1";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IAuditContext> _auditContext = new();

    private AdminSetEmployeeWeeklyOrderLimit.Validator Validator() => new(_employeeRepository.Object);

    private AdminSetEmployeeWeeklyOrderLimit.Handler Handler() =>
        new(_employeeRepository.Object, _auditContext.Object);

    private Core.Domain.Users.Employee ArrangeEmployee(int? existingLimit)
    {
        var employee = ValidatorTestHelpers.BuildEmployee(
            EmployeeId, ContractStatus.Approved, weeklyOrderLimit: existingLimit);
        _employeeRepository.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        return employee;
    }

    [Fact]
    public async Task Setting_A_Limit_Caps_The_Cleaner()
    {
        var employee = ArrangeEmployee(existingLimit: null);

        var result = await Handler().Handle(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, employee.WeeklyOrderLimit);
    }

    /// <summary>
    /// The direction <see cref="AdminUpdateEmployee"/> structurally cannot express, and the reason this
    /// command exists at all.
    /// </summary>
    [Fact]
    public async Task A_Null_Limit_Clears_The_Cap_Back_To_Unlimited()
    {
        var employee = ArrangeEmployee(existingLimit: 3);

        var result = await Handler().Handle(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(employee.WeeklyOrderLimit);
    }

    /// <summary>
    /// The audit row carries the values, not just ids — an integer the platform chose is not the
    /// subject's personal data, so the trail can answer "what was it before".
    /// </summary>
    [Fact]
    public async Task The_Audit_Row_Records_The_Old_And_New_Values()
    {
        ArrangeEmployee(existingLimit: 5);
        AdminSetEmployeeWeeklyOrderLimit.WeeklyLimitSnapshot? before = null;
        AdminSetEmployeeWeeklyOrderLimit.WeeklyLimitSnapshot? after = null;
        _auditContext
            .Setup(a => a.RecordChange("User", It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, a2, _) =>
            {
                before = b as AdminSetEmployeeWeeklyOrderLimit.WeeklyLimitSnapshot;
                after = a2 as AdminSetEmployeeWeeklyOrderLimit.WeeklyLimitSnapshot;
            });

        await Handler().Handle(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, 2), CancellationToken.None);

        Assert.Equal(5, before?.WeeklyOrderLimit);
        Assert.Equal(2, after?.WeeklyOrderLimit);
    }

    [Fact]
    public async Task A_Missing_Employee_Is_A_Business_Failure_Not_A_Crash()
    {
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.Users.Employee?)null);

        var result = await Handler().Handle(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, 3), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_Cap_Below_One_Is_Refused(int limit)
    {
        ArrangeEmployee(existingLimit: null);

        var result = await Validator().ValidateAsync(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, limit));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.WeeklyOrderLimitInvalid);
    }

    /// <summary>
    /// Zero is refused rather than accepted-as-a-ban on purpose: a cleaner who may take nothing is a
    /// contract-status decision, and expressing it as a quiet number would hide it from every screen
    /// that reads the contract.
    /// </summary>
    [Fact]
    public async Task Clearing_The_Cap_Is_Valid_But_Zero_Is_Not()
    {
        ArrangeEmployee(existingLimit: 3);

        var cleared = await Validator().ValidateAsync(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, null));
        var zero = await Validator().ValidateAsync(
            new AdminSetEmployeeWeeklyOrderLimit.Command(EmployeeId, 0));

        Assert.True(cleared.IsValid);
        Assert.False(zero.IsValid);
    }
}
