using System.Text.Json;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// ADR-0024 (TC-REVOKE-TTL-4) — the access-token TTL is the device-revocation latency bound, so it
/// is a security bound, not a tuning knob. The two mobile hosts carry 30 minutes; the three web
/// hosts deliberately stay at 1440 until their own follow-up ADR (web sessions carry no DeviceId
/// and are structurally device-unrevocable, so a web flip would be scope creep, not a fix).
///
/// This pin parses the RAW appsettings files from the repo — deliberately NOT a bound-config
/// assertion through a booted host: the HostTests harness layers appsettings.HostTests.json last
/// and its own AccessTokenExpMinutes would mask a silent revert of the real files. Both a mobile
/// revert to 1440 and a silent web-host flip away from 1440 fail here until a superseding ADR
/// moves the pin.
/// </summary>
public class AccessTokenTtlConfigPinTests
{
    [Theory]
    [InlineData("Cleansia.Web.Mobile.Partner", 30d)]
    [InlineData("Cleansia.Web.Mobile.Customer", 30d)]
    [InlineData("Cleansia.Web.Partner", 1440d)]
    [InlineData("Cleansia.Web.Admin", 1440d)]
    [InlineData("Cleansia.Web.Customer", 1440d)]
    public void AccessTokenExpMinutes_Is_Pinned_Per_Host(string hostProject, double expectedMinutes)
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory from the test base directory.");

        foreach (var fileName in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var path = Path.Combine(solutionDir!, hostProject, fileName);
            Assert.True(File.Exists(path), $"Host settings not found: {path}");

            var actual = ReadAccessTokenExpMinutes(path);
            Assert.True(
                actual == expectedMinutes,
                $"{hostProject}/{fileName}: JwtSettings:AccessTokenExpMinutes is {actual}, expected " +
                $"{expectedMinutes}. This value is the device-revocation latency bound (ADR-0024) — " +
                "changing it requires a superseding ADR, which then moves this pin.");
        }
    }

    private static double ReadAccessTokenExpMinutes(string path)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        if (doc.RootElement.TryGetProperty("JwtSettings", out var jwtSettings)
            && jwtSettings.TryGetProperty("AccessTokenExpMinutes", out var minutes))
        {
            return minutes.GetDouble();
        }

        Assert.Fail(
            $"JwtSettings:AccessTokenExpMinutes missing from {path} — the host would silently fall " +
            "back to the code default, taking the TTL out from under the ADR-0024 pin.");
        return double.NaN;
    }

    // Mirrors DatabaseMigrationExtensions.FindSolutionDirectory — walk up until a *.sln is found.
    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
