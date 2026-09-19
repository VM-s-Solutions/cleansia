using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// <c>IgnoreQueryFilters()</c> is a deliberate, enumerated bypass: the <c>*IgnoringTenant*</c> lookups
/// for system triggers and the anonymous identity paths (login, register pre-check, social, the legacy
/// confirm link — each pinned by a secret or by the globally unique email, ADR-0061 D4/D5.1), plus the
/// login-lockout / reset-budget charges that must land for tenant-stamped accounts on anonymous
/// requests. This source-level guard pins that <c>IgnoreQueryFilters</c> appears only in the enumerated
/// bypass methods.
/// </summary>
public class UserRepositoryTokenLookupTenantTests
{
    private static string ReadRepositorySource()
    {
        // Cleansia.Api.sln lives in the src/ directory, so its folder IS the source root.
        var srcRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (srcRoot is not null && !File.Exists(Path.Combine(srcRoot.FullName, "Cleansia.Api.sln")))
        {
            srcRoot = srcRoot.Parent;
        }

        Assert.NotNull(srcRoot);
        var path = Path.Combine(srcRoot!.FullName, "Cleansia.Infra.Database", "Repositories", "UserRepository.cs");
        Assert.True(File.Exists(path), $"UserRepository.cs not found at {path}");
        return File.ReadAllText(path);
    }

    // The actual EF call (leading dot + open paren) — so a method's comment mentioning the word
    // "IgnoreQueryFilters" can't trip the assertion; only a real invocation counts.
    private const string IgnoreCall = @"\.IgnoreQueryFilters\(";

    // The legacy confirm link is opened anonymously against a stamped row (ADR-0061 D4): the read MUST
    // bypass, and the server-issued 128-bit hash is the pin.
    [Fact]
    public void Legacy_ConfirmationCode_Read_Ignores_Tenant_Filter()
    {
        var source = ReadRepositorySource();

        var body = ExtractMethodBody(source, "GetByConfirmationCodeIgnoringTenantAsync");
        Assert.True(Regex.IsMatch(body, IgnoreCall), "GetByConfirmationCodeIgnoringTenantAsync must call IgnoreQueryFilters()");
    }

    // IgnoreQueryFilters() is only CALLED in the enumerated bypass methods (the named cross-tenant /
    // anonymous-login lookups and the anonymous lockout/reset-budget charges).
    // Adding a bypass anywhere else must consciously extend this list.
    [Fact]
    public void IgnoreQueryFilters_Is_Confined_To_The_Enumerated_Bypass_Methods()
    {
        var source = ReadRepositorySource();

        var occurrences = Regex.Matches(source, IgnoreCall).Count;
        var inNamedBypasses = new[]
            {
                "GetByIdIgnoringTenantAsync",
                // An order's operator can differ from its recipient's account; delivery resolves only that account's company.
                "GetNotificationRecipientTenantAsync",
                // An admin event names its company as an argument (the order's, the webhook's) while the
                // caller's override may name another; the read pins TenantId to that argument.
                "GetActiveAdministratorsAsync",
                "GetByEmailIgnoringTenantAsync",
                "ExistsWithEmailIgnoringTenantAsync",
                // Sign in with Apple is anonymous and resolves the account by the verified Apple sub —
                // same bypass rationale as the anonymous email lookup above.
                "GetByAppleIdIgnoringTenantAsync",
                // Google sign-in is anonymous for the same reason and now resolves by the verified
                // Google sub before falling back to the email — same bypass rationale as Apple.
                "GetByGoogleIdIgnoringTenantAsync",
                // The legacy 128-bit confirm link: anonymous request, stamped row, hash is the pin.
                "GetByConfirmationCodeIgnoringTenantAsync",
                "RecordFailedLoginAsync",
                "TryChargeResetPasswordCodeAttemptAsync",
                // The OTP confirm branch resolves the account by email (anonymous), so its budget
                // charge must land for tenant-stamped accounts too — mirrors the reset charge above.
                "TryChargeConfirmationCodeAttemptAsync",
            }
            .Sum(method => Regex.Matches(ExtractMethodBody(source, method), IgnoreCall).Count);

        Assert.Equal(inNamedBypasses, occurrences);
    }

    [Fact]
    public void Administrators_Bypass_Is_Pinned_To_The_Named_Company_And_The_Eligible_Administrators()
    {
        var body = ExtractMethodBody(ReadRepositorySource(), "GetActiveAdministratorsAsync");
        Assert.Matches(@"u\.TenantId\s*==\s*tenantId", body);
        Assert.Matches(@"u\.Profile\s*==\s*UserProfile\.Administrator", body);
        Assert.Contains("u.IsActive", body);
        Assert.Contains("u.IsEmailConfirmed", body);
        Assert.Matches(@"!u\.Email\.EndsWith\(User\.AnonymisedEmailSuffix\)", body);
        Assert.DoesNotContain(".Include(", body);
        Assert.Single(Regex.Matches(body, IgnoreCall));
    }

    [Fact]
    public void Notification_Recipient_Bypass_Is_Pinned_To_The_User_And_Projects_Only_Their_Company()
    {
        var body = ExtractMethodBody(ReadRepositorySource(), "GetNotificationRecipientTenantAsync");
        Assert.Matches(@"\.Where\(u\s*=>\s*u\.Id\s*==\s*userId\)", body);
        Assert.Matches(@"\.Select\(u\s*=>\s*u\.TenantId\)", body);
        Assert.Contains(".FirstOrDefaultAsync(cancellationToken)", body);
        Assert.DoesNotContain(".Include(", body);
        Assert.Single(Regex.Matches(body, IgnoreCall));
    }

    // Extracts a single method's brace-balanced body by name.
    private static string ExtractMethodBody(string source, string methodName)
    {
        var sigIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(sigIndex >= 0, $"method {methodName} not found");

        var braceStart = source.IndexOf('{', sigIndex);
        Assert.True(braceStart >= 0, $"opening brace for {methodName} not found");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(braceStart, i - braceStart + 1);
                }
            }
        }

        Assert.Fail($"unbalanced braces while extracting {methodName}");
        return string.Empty;
    }
}
