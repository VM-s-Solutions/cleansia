using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The browser and the API must name the SAME Google client. <c>GoogleTokenVerifier</c> validates a single
/// audience, so if the id the bundle initialises GSI with ever diverges from the one the API pins, every
/// web Google sign-in fails with <c>auth.invalid_google_token</c> — server-side, with nothing in the
/// browser to suggest a configuration problem. The two values live in different languages, different
/// directories and different deploy pipelines, so nothing but this pin holds them together.
///
/// The deployed value comes from the Bicep parameter file rather than <c>appsettings.json</c> on purpose:
/// App Service replaces its whole appSettings collection on every provision, so a hand-set portal value is
/// destroyed by the next deploy, and a value committed to <c>appsettings.json</c> would be inherited by
/// PROD — handing prod an audience minted for dev.
/// </summary>
public class WebSocialAudienceConfigPinTests
{
    [Fact]
    public void Dev_Google_Client_Id_Matches_The_Browser_Bundle()
    {
        var deployed = BicepParam("weu.dev.bicepparam", "googleWebClientId");
        var browser = EnvironmentValue("environment.staging.ts", "googleClientId");

        Assert.False(string.IsNullOrWhiteSpace(browser),
            "environment.staging.ts has no googleClientId — the DEV bundle would render no Google button.");

        Assert.Equal(browser, deployed);
    }

    /// <summary>
    /// DEV builds the nx `staging` configuration and PROD builds `production` (deploy-azure.yml), so the
    /// prod bundle and the prod Bicep parameter are the pair that must agree there. Both are empty today,
    /// which is the correct fail-closed state until prod gets its own client — the point of this pin is
    /// that they move together, never that prod stays off.
    /// </summary>
    [Fact]
    public void Prod_Google_Client_Id_Matches_The_Browser_Bundle()
    {
        var deployed = BicepParam("weu.prod.bicepparam", "googleWebClientId");
        var browser = EnvironmentValue("environment.prod.ts", "googleClientId");

        Assert.Equal(browser, deployed);
    }

    /// <summary>
    /// The shared base file must stay blank so an environment that forgets its parameter fails closed
    /// instead of silently falling back to whatever the last environment committed.
    /// </summary>
    [Theory]
    [InlineData("Google", "ClientId")]
    [InlineData("Apple", "WebServicesId")]
    public void Customer_Web_Host_Ships_No_Baked_In_Audience(string section, string key)
    {
        var path = Path.Combine(SolutionDirectory(), "Cleansia.Web.Customer", "appsettings.json");

        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        Assert.True(doc.RootElement.TryGetProperty(section, out var node),
            $"appsettings.json is missing the {section} section; the config would not bind.");

        Assert.True(node.TryGetProperty(key, out var value),
            $"appsettings.json is missing {section}:{key}.");

        Assert.True(string.IsNullOrEmpty(value.GetString()),
            $"{section}:{key} must be blank in the shared appsettings.json — supply it per environment " +
            "from the Bicep parameter file, or PROD inherits DEV's audience on any deploy that omits it.");
    }

    private static string BicepParam(string file, string name)
    {
        var path = Path.Combine(RepositoryRoot(), "deploy", "bicep", file);
        Assert.True(File.Exists(path), $"Bicep parameter file not found: {path}");

        // A commented-out parameter means "unset", which binds the main.bicep default of ''.
        var match = Regex.Match(
            File.ReadAllText(path),
            $@"^\s*param\s+{Regex.Escape(name)}\s*=\s*'([^']*)'",
            RegexOptions.Multiline);

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string EnvironmentValue(string file, string name)
    {
        var path = Path.Combine(
            SolutionDirectory(), "Cleansia.App", "apps", "cleansia.app", "src", "environments", file);
        Assert.True(File.Exists(path), $"Environment file not found: {path}");

        // Prettier wraps a long value onto its own line, so the quote may not be on the key's line.
        var match = Regex.Match(File.ReadAllText(path), $@"{Regex.Escape(name)}:\s*'([^']*)'");
        Assert.True(match.Success, $"{file} has no {name} entry.");

        return match.Groups[1].Value;
    }

    // Mirrors AppleAudienceIsolationConfigPinTests — walk up until a *.sln is found.
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

    // deploy/ is a sibling of src/, so the repo root is one level above the solution directory.
    private static string RepositoryRoot()
    {
        var root = Directory.GetParent(SolutionDirectory());
        Assert.False(root is null, "Could not locate the repository root above the solution directory.");
        return root!.FullName;
    }
}
