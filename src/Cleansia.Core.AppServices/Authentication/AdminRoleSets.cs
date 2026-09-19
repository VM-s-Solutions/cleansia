using System.Security.Claims;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Authentication;

/// <summary>
/// The one place an administrator set is spelled. The matrix is a lattice — every admin permission is
/// "the least role that has it, and everyone above" — so four sets and "any" cover it; the one event
/// that belongs to two branches (a chargeback: Support answers the bank, the Accountant reconciles the
/// money) is exactly their union, which is any administrator. The authorization handlers ask about a
/// principal; the admin notifier asks about a row. Both fail closed.
/// </summary>
public static class AdminRoleSets
{
    public const string ClaimType = "admin_role";

    public static readonly IReadOnlySet<AdminRole> AdministratorOnly = new HashSet<AdminRole>
    {
        AdminRole.Administrator,
    };

    public static readonly IReadOnlySet<AdminRole> ManagerOrAbove = new HashSet<AdminRole>
    {
        AdminRole.Administrator, AdminRole.Manager,
    };

    public static readonly IReadOnlySet<AdminRole> SupportOrAbove = new HashSet<AdminRole>
    {
        AdminRole.Administrator, AdminRole.Manager, AdminRole.Support,
    };

    public static readonly IReadOnlySet<AdminRole> AccountantOrAbove = new HashSet<AdminRole>
    {
        AdminRole.Administrator, AdminRole.Manager, AdminRole.Accountant,
    };

    public static readonly IReadOnlySet<AdminRole> Any = new HashSet<AdminRole>(Enum.GetValues<AdminRole>());

    /// <summary>
    /// The set a physical-policy name denotes. <see cref="PhysicalPolicy.AdminOnly"/> is any administrator;
    /// any other name is a programming error, never a silent "nobody" or "everybody".
    /// </summary>
    public static IReadOnlySet<AdminRole> For(string physicalPolicy) => physicalPolicy switch
    {
        PhysicalPolicy.AdminOnly => Any,
        PhysicalPolicy.AdministratorOnly => AdministratorOnly,
        PhysicalPolicy.ManagerOrAbove => ManagerOrAbove,
        PhysicalPolicy.SupportOrAbove => SupportOrAbove,
        PhysicalPolicy.AccountantOrAbove => AccountantOrAbove,
        _ => throw new ArgumentOutOfRangeException(nameof(physicalPolicy), physicalPolicy, "Not an administrator set."),
    };

    /// <summary>
    /// Fail-closed: not an administrator, no claim, an unparseable claim, or a role outside the set is false.
    /// </summary>
    public static bool Admits(ClaimsPrincipal user, IReadOnlySet<AdminRole> set) =>
        user.IsInRole(UserProfile.Administrator.ToString())
        && Enum.TryParse<AdminRole>(user.FindFirst(ClaimType)?.Value, out var role)
        && Enum.IsDefined(role)
        && set.Contains(role);
}
