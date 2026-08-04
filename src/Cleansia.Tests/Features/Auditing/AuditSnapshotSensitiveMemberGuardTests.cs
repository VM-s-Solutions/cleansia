using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D4.1 / ADR-0034 D6 — an admin-action audit snapshot is ids-only, and no sensitive VALUE is
/// ever copied into <c>AdminActionAudit.BeforeJson</c>/<c>AfterJson</c>.
///
/// <para><b>Why this exists as a type walk rather than as another value assertion.</b>
/// <c>EmployeeUserAuditCoverageTests</c> already asserts that one specific IBAN does not appear in the
/// JSON of the handlers it drives. That is a true statement about the handlers somebody thought to
/// drive, with the value somebody thought to seed — the concern was recognised in exactly one place. The
/// audit trail is the compensating control for storing payout details in plaintext, so an audit row that
/// copies the value turns the control into a second copy of the thing it is compensating for. This walks
/// the whole snapshot family instead, so a new one joins the rule by existing.</para>
///
/// <para><c>AuditContext.RecordChange</c> takes <c>object</c> and serializes it whole, which is exactly
/// why the discipline has to live on the snapshot TYPES: nothing at the seam can tell a redacted record
/// from an unredacted one.</para>
/// </summary>
public class AuditSnapshotSensitiveMemberGuardTests
{
    /// <summary>
    /// Where the money lands, and who the person is. Both are forbidden in a snapshot for the same
    /// reason — the row exists to say WHAT happened to WHICH subject, not to reproduce the subject.
    /// </summary>
    private static readonly Regex ForbiddenMemberName = new(
        "^(iban|bic|swift|accountnumber|accountprefix|bankaccountnumber|bankcode|holdername|provideraccountref" +
        "|email|.*email|phone|.*phone.*|firstname|lastname|fullname|birthdate|passportid)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void No_Audit_Snapshot_Record_Declares_A_Payout_Identifier_Or_Contact_Identity()
    {
        var offenders = SnapshotTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => ForbiddenMemberName.IsMatch(p.Name))
                .Select(p => $"{t.DeclaringType?.Name}.{t.Name}.{p.Name}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0012 D4.1: an audit snapshot carries ids and outcomes, never the subject's payout " +
            "destination or contact identity — the audit row is the compensating control for holding " +
            "those in plaintext, not another place to hold them.\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Anti-vacuity: a walk that finds no snapshot types would pass silently forever.</summary>
    [Fact]
    public void Guard_ActuallySeesTheSnapshotFamily()
    {
        var types = SnapshotTypes();

        Assert.InRange(types.Count, 8, 100);
        Assert.Contains(types, t => t.Name == "RevealSnapshot");
    }

    /// <summary>
    /// The convention is the name: a record ending in <c>Snapshot</c> declared inside a feature class is
    /// what handlers hand to <c>IAuditContext.RecordChange</c>. Naming is the only static handle here —
    /// the seam's parameter is <c>object</c> — so a snapshot that does not follow it is outside this
    /// guard, which is the reason the convention is worth keeping.
    /// </summary>
    private static List<Type> SnapshotTypes() =>
        typeof(IAuditContext).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Snapshot", StringComparison.Ordinal)
                        && t != typeof(AuditSnapshot)
                        && t is { IsClass: true, IsAbstract: false })
            .ToList();
}
