using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The ARES check defaults on, and the Development settings that switch it off never load on a deployed
/// host, because every deployed host runs as Production. So the deployed DEV hosts asked ares.gov.cz about
/// every test cleaner's made-up IČO and refused to approve them. The template switches it off everywhere but
/// prod, on every API host (owner default 2026-10-04).
/// </summary>
public class AresDeployedSwitchTests
{
    [Fact]
    public void Every_Api_Host_Checks_Ares_In_Prod_Only()
    {
        var bicep = WithoutComments(File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep")));

        var apiBaseSettings = Regex.Match(
            bicep, @"var\s+apiBaseSettings\s*=\s*union\(\{(?<body>.*?)\}\s*,\s*storageSettings", RegexOptions.Singleline);
        Assert.True(apiBaseSettings.Success, "Could not find the `apiBaseSettings` var in deploy/bicep/main.bicep.");

        Assert.Matches(
            @"Ares__Enabled\s*:\s*env\s*==\s*'prod'\s*\?\s*'true'\s*:\s*'false'",
            apiBaseSettings.Groups["body"].Value);
    }

    private static string RepoPath(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine([SolutionDirectory(), "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.False(directory is null, "Could not locate the solution directory from the test base directory.");
        return directory!.FullName;
    }

    private static string WithoutComments(string template) =>
        Regex.Replace(
            template,
            @"(?<text>'''.*?'''|'(?:\\.|[^'\\\r\n])*')|/\*.*?\*/|//[^\n]*",
            match => match.Groups["text"].Success ? match.Value : string.Empty,
            RegexOptions.Singleline);
}
