using System.Text.Json;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The native/web Apple audience split is a security boundary held up entirely by per-host configuration.
/// <c>AppleTokenVerifier</c> accepts every audience its host lists, and the customer WEB host answers a
/// successful <c>AppleAuth</c> with HttpOnly session cookies — so a web host that also listed the native
/// <c>BundleId</c> would let a captured iOS <c>(identityToken, rawNonce)</c> pair POSTed to its API mint a
/// browser session. Nothing in the type system prevents that; one added JSON key does it silently.
///
/// So the shapes are pinned per host, against the RAW appsettings files from the repo (NOT a booted host),
/// mirroring the TC-REVOKE-TTL-4 / TC-REVOKE-NOW-7 pins so no HostTests overlay can mask a real-file
/// change: mobile hosts carry <c>BundleId</c> only, the customer web host carries <c>WebServicesId</c>
/// only, and no host anywhere carries both. Widening this requires a superseding ADR, which then moves
/// this pin.
///
/// The deployed values come from Azure App Service settings (<c>Apple__BundleId</c> /
/// <c>Apple__WebServicesId</c>), which are out-of-repo and NOT covered here — the same rule applies there
/// and is an owner runbook item.
/// </summary>
public class AppleAudienceIsolationConfigPinTests
{
    private const string BundleIdKey = "BundleId";
    private const string WebServicesIdKey = "WebServicesId";

    [Fact]
    public void Customer_Web_Host_Configures_The_Web_Services_Id_And_Never_The_Native_BundleId()
    {
        foreach (var path in HostSettingsFiles("Cleansia.Web.Customer"))
        {
            var apple = ReadAppleSection(path);
            if (apple is null)
            {
                continue;
            }

            Assert.True(apple.Value.TryGetProperty(WebServicesIdKey, out _),
                $"{path}: Apple:WebServicesId is missing. Without it the customer web host has no Apple " +
                "audience at all and every web Sign in with Apple fails closed.");

            Assert.False(apple.Value.TryGetProperty(BundleIdKey, out _),
                $"{path}: Apple:BundleId must NOT be set on the customer web host. This host issues HttpOnly " +
                "session cookies, so accepting the native audience would let a captured iOS identity token " +
                "mint a browser session.");
        }
    }

    [Theory]
    [InlineData("Cleansia.Web.Mobile.Customer")]
    [InlineData("Cleansia.Web.Mobile.Partner")]
    public void Mobile_Hosts_Configure_The_Native_BundleId_And_Never_The_Web_Services_Id(string hostProject)
    {
        foreach (var path in HostSettingsFiles(hostProject))
        {
            var apple = ReadAppleSection(path);
            if (apple is null)
            {
                continue;
            }

            Assert.False(apple.Value.TryGetProperty(WebServicesIdKey, out _),
                $"{path}: Apple:WebServicesId must NOT be set on a native host — the Services ID is the " +
                "browser flow's audience and has no business being accepted here.");
        }
    }

    // The general invariant behind both pins, so a host added later inherits it without a new test.
    [Fact]
    public void No_Host_Configures_Both_Apple_Audiences()
    {
        foreach (var path in AllHostSettingsFiles())
        {
            var apple = ReadAppleSection(path);
            if (apple is null)
            {
                continue;
            }

            var hasBundleId = apple.Value.TryGetProperty(BundleIdKey, out var bundleId)
                && !string.IsNullOrWhiteSpace(bundleId.GetString());
            var hasWebServicesId = apple.Value.TryGetProperty(WebServicesIdKey, out var webServicesId)
                && !string.IsNullOrWhiteSpace(webServicesId.GetString());

            Assert.False(hasBundleId && hasWebServicesId,
                $"{path}: a host must accept EITHER the native bundle id OR the web Services ID, never both. " +
                "Accepting both collapses the isolation between the two flows.");
        }
    }

    private static JsonElement? ReadAppleSection(string path)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return doc.RootElement.TryGetProperty("Apple", out var apple)
            ? apple.Clone()
            : null;
    }

    private static IEnumerable<string> AllHostSettingsFiles()
        => Directory.EnumerateDirectories(SolutionDirectory(), "Cleansia.Web.*")
            .SelectMany(host => HostSettingsFiles(Path.GetFileName(host)!));

    private static IEnumerable<string> HostSettingsFiles(string hostProject)
    {
        // Top-level only: bin/ carries build copies of the same files and would double every assertion.
        var directory = Path.Combine(SolutionDirectory(), hostProject);
        Assert.True(Directory.Exists(directory), $"Host project not found: {directory}");

        return Directory.EnumerateFiles(directory, "appsettings*.json", SearchOption.TopDirectoryOnly);
    }

    // Mirrors AccessTokenTtlConfigPinTests - walk up until a *.sln is found.
    private static string SolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.False(dir is null, "Could not locate the solution directory from the test base directory.");
        return dir!.FullName;
    }
}
