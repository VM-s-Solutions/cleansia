using System.Text.Json;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// TC-REVOKE-USER-7 (ADR-0027 D7) — the reset-cutoff check reuses the DeviceRevocation kill switch and
/// interval (the SHARED switch), and is installed on the two MOBILE hosts only. This pins two things a
/// silent edit could regress:
/// <list type="bullet">
///   <item>the shared <c>DeviceRevocation:Enabled = true</c> / <c>RefreshSeconds &lt;= 30</c> values
///   still govern (raw-file, so no booted-host overlay can mask a real-file revert), and</item>
///   <item><c>AddUserRevocationEnforcement</c> appears in exactly the two mobile hosts'
///   <c>ServiceExtensions</c> and in NEITHER of the three web hosts (a web host installing a
///   revocation directory would be an ADR-0027 D8 scope violation).</item>
/// </list>
/// Changing the shared values requires a superseding ADR (which then moves this pin, in lockstep with
/// <see cref="DeviceRevocationConfigPinTests"/>).
/// </summary>
public class UserRevocationWiringPinTests
{
    private static readonly string[] MobileHosts = ["Cleansia.Web.Mobile.Partner", "Cleansia.Web.Mobile.Customer"];
    private static readonly string[] WebHosts = ["Cleansia.Web.Partner", "Cleansia.Web.Admin", "Cleansia.Web.Customer"];

    [Theory]
    [InlineData("Cleansia.Web.Mobile.Partner")]
    [InlineData("Cleansia.Web.Mobile.Customer")]
    public void Shared_DeviceRevocation_Bounds_Govern_The_User_Check_Per_Mobile_Host(string hostProject)
    {
        var solutionDir = RequireSolutionDirectory();

        foreach (var fileName in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var path = Path.Combine(solutionDir, hostProject, fileName);
            Assert.True(File.Exists(path), $"Host settings not found: {path}");

            var (enabled, refreshSeconds) = ReadDeviceRevocation(path);

            Assert.True(enabled,
                $"{hostProject}/{fileName}: DeviceRevocation:Enabled is false. It is the SHARED kill switch " +
                "for both the device AND the reset-cutoff checks (ADR-0027 D7) - disabling it is a security " +
                "regression requiring a superseding ADR.");

            Assert.True(refreshSeconds <= 30,
                $"{hostProject}/{fileName}: DeviceRevocation:RefreshSeconds is {refreshSeconds}, must be <= 30. " +
                "It is the SHARED enforcement-latency bound for both mobile revocation checks (ADR-0027 D7).");
        }
    }

    [Theory]
    [InlineData("Cleansia.Web.Mobile.Partner")]
    [InlineData("Cleansia.Web.Mobile.Customer")]
    public void Mobile_Hosts_Install_The_User_Revocation_Enforcement(string hostProject)
    {
        var wiring = ReadServiceExtensions(hostProject);

        Assert.Contains("AddUserRevocationEnforcement", wiring);
        // T-0420 (ADR-0026 CH-10) moved the per-request OnTokenValidated hook into the SHARED mobile
        // JWT registration, so the pin follows the indirection: the host must call the shared
        // registration, and the shared registration must still run BOTH revocation checks on every
        // validated token.
        Assert.Contains("AddCleansiaMobileJwt", wiring);
        var shared = ReadSharedMobileJwtSource();
        Assert.Contains("EnforceUserRevocation", shared);
        Assert.Contains("EnforceDeviceRevocation", shared);
    }

    [Theory]
    [InlineData("Cleansia.Web.Partner")]
    [InlineData("Cleansia.Web.Admin")]
    [InlineData("Cleansia.Web.Customer")]
    public void Web_Hosts_Install_No_Revocation_Directory(string hostProject)
    {
        var wiring = ReadServiceExtensions(hostProject);

        Assert.DoesNotContain("AddUserRevocationEnforcement", wiring);
        Assert.DoesNotContain("EnforceUserRevocation", wiring);
        // The device directory is mobile-only too (ADR-0026) - pin both so a web host can never gain a
        // per-request-adjacent revocation directory without tripping this test.
        Assert.DoesNotContain("AddDeviceRevocationEnforcement", wiring);
        Assert.DoesNotContain("EnforceDeviceRevocation", wiring);
        // Nor via the T-0420 indirection: the shared mobile JWT registration carries both
        // OnTokenValidated revocation hooks, so a web host must never call it either.
        Assert.DoesNotContain("AddCleansiaMobileJwt", wiring);
    }

    [Fact]
    public void Exactly_The_Two_Mobile_Hosts_Wire_The_User_Check()
    {
        var installing = MobileHosts.Concat(WebHosts)
            .Where(h => ReadServiceExtensions(h).Contains("AddUserRevocationEnforcement"))
            .OrderBy(h => h)
            .ToArray();

        Assert.Equal(MobileHosts.OrderBy(h => h).ToArray(), installing);
    }

    private static string ReadServiceExtensions(string hostProject)
    {
        var solutionDir = RequireSolutionDirectory();
        var path = Path.Combine(solutionDir, hostProject, "Extensions", "ServiceExtensions.cs");
        Assert.True(File.Exists(path), $"ServiceExtensions not found: {path}");
        return File.ReadAllText(path);
    }

    private static string ReadSharedMobileJwtSource()
    {
        var solutionDir = RequireSolutionDirectory();
        var path = Path.Combine(solutionDir, "Cleansia.Config", "Services", "ServiceExtensions.cs");
        Assert.True(File.Exists(path), $"Shared mobile JWT registration not found: {path}");
        return File.ReadAllText(path);
    }

    private static (bool Enabled, int RefreshSeconds) ReadDeviceRevocation(string path)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        if (!doc.RootElement.TryGetProperty("DeviceRevocation", out var section))
        {
            Assert.Fail(
                $"DeviceRevocation section missing from {path} - the host would fall back to the code default, " +
                "taking the SHARED enforcement bounds out from under the ADR-0027 D7 pin.");
        }

        Assert.True(section.TryGetProperty("Enabled", out var enabled), $"DeviceRevocation:Enabled missing from {path}");
        Assert.True(section.TryGetProperty("RefreshSeconds", out var refresh), $"DeviceRevocation:RefreshSeconds missing from {path}");

        return (enabled.GetBoolean(), refresh.GetInt32());
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
