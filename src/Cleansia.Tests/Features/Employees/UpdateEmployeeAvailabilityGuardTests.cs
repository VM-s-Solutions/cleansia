using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// An absent availability must leave the cleaner's week alone.
///
/// <para><b>What this guards.</b> <c>UpdateEmployee</c> used to hand
/// <c>ConvertAvailability(command.Availability)</c> straight to the aggregate, and that helper turns
/// <c>null</c> into an EMPTY dictionary — so any caller that simply did not send the field cleared the
/// schedule. Nothing exposed that while the partner web profile form still carried an availability
/// editor and posted the current value back on every save. Removing that editor (it was dead UI; the
/// real one is the dedicated <c>UpdateAvailability</c> endpoint) made the web form exactly such a
/// caller, which is what turned a latent bug into a live one.</para>
///
/// <para><b>Null and empty are deliberately different.</b> Null is "I am not talking about
/// availability"; an empty dictionary is a caller saying "clear it". Collapsing the two is the bug.</para>
/// </summary>
public class UpdateEmployeeAvailabilityGuardTests
{
    private static readonly Dictionary<string, List<TimeRange>> WorkingWeek = new()
    {
        ["Monday"] = [new TimeRange { Start = new TimeSpan(8, 0, 0), End = new TimeSpan(12, 0, 0) }],
        ["Friday"] = [new TimeRange { Start = new TimeSpan(9, 0, 0), End = new TimeSpan(17, 30, 0) }],
    };

    private static Employee WithSchedule()
    {
        var user = User.CreateWithPassword("cleaner@cleansia.cz", "Password1", "Clea", "Ner");
        user.Id = "user-1";

        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";
        employee.UpdateAvailability(new Dictionary<string, List<TimeRange>>(WorkingWeek));

        return employee;
    }

    /// <summary>
    /// The line the guard actually turns on. `employee.Availability` is a read-only projection, so the
    /// handler copies it — this pins that the copy is the STORED week and not an empty map.
    /// </summary>
    [Fact]
    public void An_Absent_Availability_Resolves_To_What_Is_Already_Stored()
    {
        var resolved = UpdateEmployee.Handler.ResolveAvailability(WithSchedule(), sent: null);

        Assert.Equal(2, resolved.Count);
        Assert.Equal(new TimeSpan(8, 0, 0), resolved["Monday"].Single().Start);
    }

    /// <summary>
    /// The other half, and the reason the guard tests `is null` rather than `?.Any() != true`: an empty
    /// dictionary is a caller deliberately clearing the week, and must still clear it.
    /// </summary>
    [Fact]
    public void An_Explicitly_Empty_Availability_Still_Clears_The_Week()
    {
        var resolved = UpdateEmployee.Handler.ResolveAvailability(
            WithSchedule(),
            sent: new Dictionary<string, List<UpdateEmployee.TimeRangeDto>>());

        Assert.Empty(resolved);
    }

    /// <summary>
    /// Anti-vacuity: the aggregate really does hold a week, so the first test is not asserting against
    /// an employee that was empty to begin with.
    /// </summary>
    [Fact]
    public void The_Fixture_Really_Has_A_Week_To_Lose()
    {
        Assert.Equal(2, WithSchedule().Availability.Count);
    }
}
