using System.Text.RegularExpressions;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The mechanical guard that makes the contact-identity denylist fail closed.
///
/// <para><b>Why a denylist needs a guard at all.</b> The hosts' <c>RequestLoggingMiddleware</c> slices
/// request and response bodies into Information-level logs, and the only thing standing between a new
/// PII-bearing DTO member and those logs is that the middleware's regex happens to cover its name. That
/// is the same defect class as a comment asserting an invariant: correct on the day it was written, and
/// silently wrong afterwards. The regex is shaped rather than enumerated (<c>[A-Za-z]*email</c>), which
/// covers a great deal of the future — but "a great deal" is not "all", and the residue is what this
/// test is for.</para>
///
/// <para><b>The rule.</b> For every route on the five hosts, every wire DTO member whose NAME is
/// PII-shaped must be one of: matched by the live redaction regexes, on a route suppressed by
/// <c>IsSensitivePath</c> on all five hosts, or on <see cref="StructurallyNotPii"/> with a written
/// reason. Otherwise CI is red, naming the DTO, the member and the routes that emit it.</para>
///
/// <para><b>It was measured before it was written, and the measurement is the argument.</b> Against the
/// pre-fix middleware this guard reported <b>152 members across 80+ routes</b> — the caller's own
/// profile, every admin user and employee list and detail, every order's customer contact block, the
/// referral lists, and the whole subject-access export. <c>GET /api/User/GetCurrent</c> was one row of
/// 152. Anyone tempted to fix a PII log leak by adding one path to <c>IsSensitivePath</c> should read
/// that number first.</para>
///
/// <para><b>What it cannot see.</b> A member whose name does not say what it holds — <c>Notes</c>,
/// <c>Description</c>, <c>Reason</c> — carries narrative PII that no name-shaped heuristic can reach.
/// Those are the sibling guard's territory (<see cref="RedactionUnmaskedFreeTextGuardTests"/>), and
/// their answer is wholesale route suppression, not redaction. A credential whose name was never in the
/// token list is a third class, covered by neither.</para>
///
/// <para>Sibling guards in this idiom: <c>RateLimitCoverageGuardTests</c>,
/// <c>AnonymousAllowListExhaustivenessTests</c>, <c>FrozenPermissionMapTests</c>.</para>
/// </summary>
public class RequestLogPiiSurfaceGuardTests
{
    /// <summary>
    /// Names that say the member holds a person's contact identity. Whole-name matching would be too
    /// narrow (<c>customerEmail</c>, <c>emergencyContactPhone</c>, <c>referrerFirstName</c> all exist),
    /// so this is a substring heuristic — deliberately wider than the production regex, because a guard
    /// that flags something the middleware does not cover is doing its job.
    /// </summary>
    private static readonly Regex PiiShapedName = new(
        "(email|phone|mobilenumber|firstname|lastname|fullname|middlename|surname|givenname|birthdate|dateofbirth)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Only these member types can carry the value. A <c>bool</c> named <c>IsEmailConfirmed</c> holds no
    /// email, and the middleware's value group (a quoted string or <c>null</c>) cannot match it anyway.
    /// </summary>
    private static readonly Type[] PiiCarryingTypes =
        [typeof(string), typeof(DateOnly), typeof(DateTime), typeof(DateTimeOffset)];

    /// <summary>
    /// Members whose name is PII-shaped but which structurally cannot hold PII. Keep this short and
    /// reasoned: every entry is a claim a human can argue with.
    ///
    /// <para><b>A newly-added member may NEVER be silenced by adding it here</b> — that is the whole
    /// point of the guard. An entry earns its place by being provably not the thing the name suggests.</para>
    /// </summary>
    private static readonly HashSet<string> StructurallyNotPii = new(StringComparer.OrdinalIgnoreCase)
    {
        // The primary key of an EmailTemplate row, e.g. "01HZX9…" — an id that happens to start with the
        // word "email". It is also why the production regex anchors *email with no suffix: a name ending
        // in "Id" must not be redacted, or the admin email-template screens lose their correlation key.
        "EmailTemplateId",
    };

    [Fact]
    public void EveryPiiShapedWireMember_IsRedactedOrItsRouteIsSuppressed()
    {
        var findings = new SortedDictionary<string, SortedSet<string>>();

        foreach (var (route, dtoTypes) in WireSurface.RoutesWithTheirWireTypes())
        {
            if (WireSurface.IsSensitivePathOnEveryHost(route))
            {
                continue;
            }

            foreach (var dto in dtoTypes)
            {
                foreach (var member in UnprotectedPiiMembers(dto))
                {
                    var key = $"{dto.DeclaringType?.Name}.{dto.Name}.{member}";
                    if (!findings.TryGetValue(key, out var routes))
                    {
                        findings[key] = routes = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    routes.Add(route);
                }
            }
        }

        Assert.True(findings.Count == 0,
            $"{findings.Count} wire member(s) whose name is PII-shaped reach an Information-level log raw. " +
            "Fix by covering the name in ContactIdentityFieldRegex on all five hosts, or by suppressing " +
            "the route in IsSensitivePath on all five hosts. Adding to StructurallyNotPii is only for a " +
            "member that provably does not hold what its name says.\n  " +
            string.Join("\n  ", findings.Select(f => $"{f.Key}  ←  {string.Join(" , ", f.Value)}")));
    }

    /// <summary>
    /// Anti-vacuity. A reflection-driven guard that discovers nothing passes silently, so a guard that
    /// inspects nothing is a non-run rather than a pass. Bounds are wide enough not to be a maintenance
    /// tax and tight enough that a broken walk reddens.
    /// </summary>
    [Fact]
    public void Guard_ActuallyInspectsTheWireSurface()
    {
        var routes = WireSurface.RoutesWithTheirWireTypes();
        var membersWalked = routes
            .SelectMany(r => r.WireTypes)
            .Distinct()
            .SelectMany(t => WireSurface.FlattenedMembers(t, depth: 0))
            .Count();

        Assert.InRange(routes.Count, 400, 1000);
        Assert.InRange(membersWalked, 1000, 20000);
        Assert.NotEmpty(WireSurface.ReadContactIdentityTokens());
    }

    /// <summary>
    /// The shape claim, stated as behaviour rather than as a pattern. These are the exact member names
    /// the 152-finding measurement turned up; a future edit that narrows the alternation back to literal
    /// names reddens here, naming the one it dropped.
    /// </summary>
    [Theory]
    [InlineData("email", true)]
    [InlineData("customerEmail", true)]
    [InlineData("actorEmail", true)]
    [InlineData("recipientEmail", true)]
    [InlineData("referredEmail", true)]
    [InlineData("phone", true)]
    [InlineData("phoneNumber", true)]
    [InlineData("customerPhone", true)]
    [InlineData("emergencyContactPhone", true)]
    [InlineData("firstName", true)]
    [InlineData("referrerFirstName", true)]
    [InlineData("lastName", true)]
    [InlineData("fullName", true)]
    [InlineData("birthDate", true)]
    // The boundary that decides the *email-with-no-suffix shape: an id must survive so the admin screens
    // keep a correlation key, and a language code is not a name.
    [InlineData("emailTemplateId", false)]
    [InlineData("preferredLanguageName", false)]
    [InlineData("bankName", false)]
    public void TheContactIdentityShape_CoversTheseNamesAndStopsAtThose(string memberName, bool expected) =>
        Assert.Equal(expected, WireSurface.IsRedacted(memberName));

    private static IEnumerable<string> UnprotectedPiiMembers(Type dto) =>
        WireSurface.FlattenedMembers(dto, depth: 0)
            .Where(m => PiiShapedName.IsMatch(m.Name)
                        && PiiCarryingTypes.Contains(m.Type)
                        && !StructurallyNotPii.Contains(m.Name)
                        && !WireSurface.IsRedacted(m.Name))
            .Select(m => m.Name);
}
