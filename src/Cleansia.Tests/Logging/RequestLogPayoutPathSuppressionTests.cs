using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// ADR-0034 AC8 / S6 — the payout value is never logged, CHECKED rather than assumed.
///
/// <para>The hosts slice request and response bodies into Information-level logs and redact by field
/// NAME. <c>accountNumber</c>, <c>iban</c>, <c>swift</c> and <c>holderName</c> are in none of those token
/// lists, and the guard that catches unmasking
/// (<see cref="RedactionUnmaskedFreeTextGuardTests"/>) says in its own doc-comment that a secret whose
/// field name is simply absent from the token list is caught by <b>nothing</b>. So the payout routes are
/// suppressed wholesale, on every host, and that is pinned here.</para>
///
/// <para><b>The named list below is not the control — <see cref="EveryRouteCarryingAPayoutIdentifier_IsSuppressedOnEveryHost"/>
/// is.</b> A hand-written list of routes is exactly the artefact that goes stale: it was written when the
/// payout routes shipped, and it did not mention <c>/api/v1/AdminGdpr/export/{userId}</c>, which returns
/// another user's entire subject-access export — payout block included — and which
/// <c>pathValue.Contains("/gdpr")</c> never matched, because no slash precedes "gdpr" inside
/// "AdminGdpr". Same shape as <c>/auth/</c> missing <c>/api/AdminAuth/…</c>. The derived guard found it;
/// the list could not have. Keep the list for legibility, and let the guard be the thing that fails.</para>
/// </summary>
public class RequestLogPayoutPathSuppressionTests
{
    /// <summary>
    /// The member names that identify where money lands. Wider than <c>PayoutDtoSurfaceTests</c>' set:
    /// that test governs which DTOs may DECLARE an identifier, this one governs what may reach a log, and
    /// the beneficiary name as their bank knows it belongs in the second question even though it is not
    /// an account identifier.
    /// </summary>
    private static readonly Regex PayoutIdentifierName = new(
        "^(iban|bic|swift|accountnumber|accountprefix|bankaccountnumber|bankcode|holdername|maskedaccount|provideraccountref)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Theory]
    [InlineData("/api/Employee/UpdateBankDetails")]
    [InlineData("/api/Employee/GetMyPayoutDetails")]
    [InlineData("/api/AdminEmployee/emp-1/payout-details")]
    [InlineData("/api/AdminEmployee/emp-1/payout-details/reveal")]
    [InlineData("/api/v1/Gdpr/export")]
    // Found by the derived guard below, not by hand.
    [InlineData("/api/v1/AdminGdpr/export/emp-1")]
    public void Payout_Carrying_Routes_Are_Suppressed_On_Every_Host(string route)
    {
        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware)
        {
            var suppressed = (bool)middleware
                .GetMethod("IsSensitivePath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new PathString(route)])!;

            Assert.True(suppressed, $"{route} must be in IsSensitivePath on {middleware.FullName}");
        }
    }

    /// <summary>
    /// Derived from the wire surface, so a new route that returns a payout identifier joins the rule the
    /// moment it exists. ADR-0034 D6 chose plaintext storage with the audit trail as the compensating
    /// control; that trade only holds while the value stays out of every other sink, and a log is a sink
    /// with different retention, different access control and — once an error tracker ships log context —
    /// a different company.
    /// </summary>
    [Fact]
    public void EveryRouteCarryingAPayoutIdentifier_IsSuppressedOnEveryHost()
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
                foreach (var member in WireSurface.FlattenedMembers(dto, depth: 0)
                             .Where(m => m.Type == typeof(string) && PayoutIdentifierName.IsMatch(m.Name)))
                {
                    var key = $"{dto.DeclaringType?.Name}.{dto.Name}.{member.Name}";
                    if (!findings.TryGetValue(key, out var routes))
                    {
                        findings[key] = routes = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    routes.Add(route);
                }
            }
        }

        Assert.True(findings.Count == 0,
            $"{findings.Count} wire member(s) carrying a payout identifier reach an Information-level log. " +
            "Field-name redaction is not the fix here (ADR-0034 D6 chose wholesale suppression, because " +
            "holderName is free text no denylist reaches) — add the route to IsSensitivePath on all five " +
            "hosts.\n  " +
            string.Join("\n  ", findings.Select(f => $"{f.Key}  ←  {string.Join(" , ", f.Value)}")));
    }

    /// <summary>Anti-vacuity: the guard is looking at a corpus that really does contain payout DTOs.</summary>
    [Fact]
    public void Guard_ActuallySeesThePayoutDtos()
    {
        var payoutMembers = WireSurface.RoutesWithTheirWireTypes()
            .SelectMany(r => r.WireTypes)
            .Distinct()
            .SelectMany(t => WireSurface.FlattenedMembers(t, depth: 0))
            .Count(m => PayoutIdentifierName.IsMatch(m.Name));

        Assert.True(payoutMembers >= 5,
            $"expected the wire walk to reach the payout DTO family; saw {payoutMembers} identifier members");
    }
}
