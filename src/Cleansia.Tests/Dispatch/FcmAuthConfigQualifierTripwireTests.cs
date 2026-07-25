using System.Text.RegularExpressions;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// A source tripwire, in the <c>SendPushNotificationSeamTripwireTests</c> shape, guarding the
/// AuthConfig branch in <c>FcmPushDispatcher</c>'s per-token loop.
///
/// Why a source assertion rather than a behavioural test: FirebaseAdmin 3.4.0's
/// <c>BatchResponse</c>/<c>SendResponse</c>/<c>FirebaseMessagingException</c> constructors are all
/// <c>internal</c> to the SDK and the repo declares no <c>InternalsVisibleTo</c>, so the loop cannot be
/// driven from a test at all. Without this pin the regression below has NO mechanical guard.
///
/// The regression it guards: the branch once read
/// <code>if (failureClass == AuthConfig &amp;&amp; item.Exception?.MessagingErrorCode is null)</code>
/// on the theory that <c>ThirdPartyAuthError</c> (which classifies as AuthConfig but carries a NON-null
/// MessagingErrorCode) is a per-token, per-platform APNs problem rather than a host-wide one. On an
/// iOS-only fleet every token fails exactly that way, so the qualifier starved the counter,
/// <c>allFailedOnAuth</c> never became true, and a mis-scoped APNs key poison-looped through
/// maxDequeueCount — the precise failure the branch exists to prevent. The mixed-fleet safety the
/// qualifier was reaching for is already provided by <c>allFailedOnAuth</c>'s own gate (nothing
/// succeeded AND every failure was AuthConfig).
/// </summary>
public class FcmAuthConfigQualifierTripwireTests
{
    private static string DispatcherSource()
    {
        var srcRoot = RequireSolutionDirectory();
        var path = Path.Combine(srcRoot, "Cleansia.Infra.Clients", "Fcm", "FcmPushDispatcher.cs");
        Assert.True(File.Exists(path), $"FcmPushDispatcher.cs not found at {path} — this tripwire has gone vacuous.");

        return File.ReadAllText(path);
    }

    [Fact]
    public void AuthConfig_Branch_Is_Not_Re_Narrowed_By_A_Null_MessagingErrorCode_Check()
    {
        var source = DispatcherSource();

        // Match the condition with any whitespace/newlines between the two clauses.
        var reNarrowed = Regex.IsMatch(
            source,
            @"failureClass\s*==\s*IntegrationFailureClass\.AuthConfig\s*&&[^)]*MessagingErrorCode\s+is\s+null",
            RegexOptions.Singleline);

        Assert.False(reNarrowed,
            "The AuthConfig branch in FcmPushDispatcher's per-token loop has been re-narrowed to " +
            "failures with a null MessagingErrorCode. That excludes ThirdPartyAuthError — Apple " +
            "refusing the APNs auth key Firebase holds — which on an iOS-only fleet is EVERY token, so " +
            "the dispatcher stops reporting AuthConfig and the handler poison-loops a permanent " +
            "configuration fault. Mixed-fleet safety is already handled by allFailedOnAuth requiring " +
            "SuccessCount == 0 && authConfigFailures == failureCount.");
    }

    [Fact]
    public void ThirdPartyAuthError_Is_Still_Routed_To_The_Apns_Specific_Detail()
    {
        var source = DispatcherSource();

        Assert.Contains("MessagingErrorCode.ThirdPartyAuthError", source, StringComparison.Ordinal);
        Assert.Contains("ApnsAuthDetail(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AllFailedOnAuth_Still_Requires_Nothing_To_Have_Succeeded()
    {
        // The gate that makes dropping the qualifier safe. If this ever goes away, a mixed fleet whose
        // Android tokens delivered fine could be reported as a host-wide auth fault and acked.
        var source = DispatcherSource();

        Assert.Contains("response.SuccessCount == 0", source, StringComparison.Ordinal);
        Assert.Contains("authConfigFailures == failureCount", source, StringComparison.Ordinal);
    }

    private static string RequireSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate the solution directory from the test base directory.");
        return string.Empty;
    }
}
