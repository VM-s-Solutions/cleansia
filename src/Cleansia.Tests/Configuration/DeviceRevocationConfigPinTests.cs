using System.Text.Json;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// TC-REVOKE-NOW-7 (ADR-0026 D8) - the device-revocation kill switch and refresh interval are
/// security bounds, not tuning knobs. The two mobile hosts must carry <c>Enabled = true</c> and
/// <c>RefreshSeconds &lt;= 30</c>; a silent flip of either is a silent security regression. Like the
/// TC-REVOKE-TTL-4 pin this parses the RAW appsettings files from the repo (NOT a booted host), so no
/// HostTests overlay can mask a real-file revert. Changing either value requires a superseding ADR,
/// which then moves this pin.
/// </summary>
public class DeviceRevocationConfigPinTests
{
    [Theory]
    [InlineData("Cleansia.Web.Mobile.Partner")]
    [InlineData("Cleansia.Web.Mobile.Customer")]
    public void DeviceRevocation_Is_Pinned_Per_Mobile_Host(string hostProject)
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory from the test base directory.");

        foreach (var fileName in new[] { "appsettings.json", "appsettings.Production.json" })
        {
            var path = Path.Combine(solutionDir!, hostProject, fileName);
            Assert.True(File.Exists(path), $"Host settings not found: {path}");

            var (enabled, refreshSeconds) = ReadDeviceRevocation(path);

            Assert.True(enabled,
                $"{hostProject}/{fileName}: DeviceRevocation:Enabled is false. This is the device-revocation " +
                "enforcement kill switch (ADR-0026) - disabling it is a security regression requiring a superseding ADR.");

            Assert.True(refreshSeconds <= 30,
                $"{hostProject}/{fileName}: DeviceRevocation:RefreshSeconds is {refreshSeconds}, must be <= 30. " +
                "This is the enforcement-latency bound (ADR-0026) - loosening it requires a superseding ADR.");
        }
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
                "taking the enforcement bounds out from under the ADR-0026 pin.");
        }

        Assert.True(section.TryGetProperty("Enabled", out var enabled), $"DeviceRevocation:Enabled missing from {path}");
        Assert.True(section.TryGetProperty("RefreshSeconds", out var refresh), $"DeviceRevocation:RefreshSeconds missing from {path}");

        return (enabled.GetBoolean(), refresh.GetInt32());
    }

    // Mirrors AccessTokenTtlConfigPinTests - walk up until a *.sln is found.
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
