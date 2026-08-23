using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Specifications;

/// <summary>
/// Who may be told about a job they hold. One predicate, shared by every cleaner-facing reminder.
///
/// <para><b>It exists because the three reminder keys shipped with two different answers.</b> The
/// day-ahead digest selected <c>Approved || Active</c>; the per-job sweep filtered the cleaner on
/// nothing at all and reminded whoever was on the assignment row. Both are wrong in the same
/// direction: an admin who rejects or terminates a cleaner does not take them off their live orders
/// (<c>RejectEmployee</c> leaves the rows in place), so the sweep would tell somebody the platform has
/// just barred from working that their job starts in two hours — for work <c>StartOrder</c> would then
/// refuse to let them start.</para>
///
/// <para><b><c>Approved</c> exactly, never <c>Approved || Active</c>.</b> <c>StartOrder</c> and
/// <c>TakeOrder</c> both test <c>== Approved</c>, so an <c>Active</c> cleaner is turned away at the
/// door; telling them how many jobs they have tomorrow promises work they cannot take. <c>Active</c>
/// has no production writer today, which is why the divergence was invisible rather than harmless —
/// it stays in the enum, and this predicate simply does not admit it.</para>
///
/// <para>The <c>User.IsActive</c> conjunct is the deactivated-account case: the contract can still read
/// <c>Approved</c> long after the login is gone.</para>
/// </summary>
public static class JobReminderRecipient
{
    /// <summary>The SQL form — compose it into the sweep's own <c>Where</c> so the filter runs in the database.</summary>
    public static Expression<Func<Employee, bool>> Predicate { get; } =
        employee => employee.User!.IsActive && employee.ContractStatus == ContractStatus.Approved;

    private static readonly Func<Employee, bool> CompiledPredicate = Predicate.Compile();

    /// <summary>
    /// The in-memory form, for a sweep that reaches its cleaners through an assignment row rather than
    /// selecting them. Compiled from <see cref="Predicate"/> rather than restated, so the two cannot
    /// drift — which is the whole failure this type exists to end.
    /// </summary>
    public static bool IsEligible(Employee? employee) =>
        employee is not null && employee.User is not null && CompiledPredicate(employee);
}
