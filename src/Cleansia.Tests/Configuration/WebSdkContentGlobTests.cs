using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Guards the fix for the self-nesting build output.
///
/// <para>
/// <c>dotnet ef</c> on macOS creates a directory whose NAME LITERALLY CONTAINS A BACKSLASH —
/// <c>bin\Debug</c>. MSBuild's glob walker enumerates that entry, normalizes the backslash to a
/// slash, and then reads the REAL <c>bin/Debug</c>. The SDK's <c>bin/**</c> prune never fires,
/// because the entry is not NAMED <c>bin</c>. So <c>Microsoft.NET.Sdk.Web</c>'s default
/// <c>**\*.json</c> content glob ingests the project's own output and copies it one level deeper
/// every build — reaching the test projects through <c>ProjectReference</c>, which is why their
/// nested chains contained another project's files.
/// </para>
/// <para>
/// It crossed the macOS path limit and failed builds with <c>MSB3021: path is too long</c> for four
/// separate agents before the cause was found. Two candidate fixes were ruled out BY EXPERIMENT: a
/// <c>Directory.Build.props</c> exclude (the prune happens during the walk, not after it) and an
/// escaped literal pattern (MSBuild normalizes backslashes, so no pattern can name the aliased
/// entry). The only fix is to turn the default glob off and list the files.
/// </para>
/// <para>
/// This test exists because that fix is an INVISIBLE PROPERTY in a csproj. A new Web host added from
/// scratch would silently reintroduce the glob, and nothing would fail until someone's build path
/// grew past the OS limit again.
/// </para>
/// </summary>
public class WebSdkContentGlobTests
{
    // The attribute is followed by '>' on the same line — anchoring to end-of-line finds nothing.
    private static readonly Regex WebSdk = new(
        @"Sdk\s*=\s*""Microsoft\.NET\.Sdk\.Web""",
        RegexOptions.Compiled);

    private static readonly Regex DefaultContentItemsOff = new(
        """<EnableDefaultContentItems>\s*false\s*</EnableDefaultContentItems>""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Walk up to the directory holding the solution, the same way
    /// <see cref="StartupSeedScriptSyncTests"/> reaches a repo file from outside its own tree.
    /// </summary>
    private static DirectoryInfo SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(
            dir is not null,
            $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}. "
                + "If the solution moved, update this test.");

        return dir!;
    }

    [Fact]
    public void Every_Web_Sdk_Host_Disables_The_Default_Content_Glob()
    {
        var root = SourceRoot();

        var webHosts = root
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (f.Name, Text: File.ReadAllText(f.FullName)))
            .Where(p => WebSdk.IsMatch(p.Text))
            .ToList();

        // If this trips, the discovery is broken rather than the projects — a silent zero would make
        // every assertion below vacuously true.
        Assert.True(
            webHosts.Count >= 5,
            $"Expected at least the five Web SDK hosts, found {webHosts.Count}. "
                + "Discovery is broken, not the projects.");

        var offenders = webHosts
            .Where(p => !DefaultContentItemsOff.IsMatch(p.Text))
            .Select(p => p.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These Web SDK projects still use the default content glob: {string.Join(", ", offenders)}. "
                + "Microsoft.NET.Sdk.Web globs **\\*.json, which ingests the project's own bin/Debug via "
                + "the backslash-named directory dotnet ef creates on macOS, and copies it one level "
                + "deeper every build. Set <EnableDefaultContentItems>false</EnableDefaultContentItems> "
                + "and list the appsettings files explicitly — do NOT try to exclude the directory, "
                + "which was ruled out by experiment.");
    }
}
