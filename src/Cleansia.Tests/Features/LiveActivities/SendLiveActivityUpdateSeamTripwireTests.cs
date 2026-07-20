namespace Cleansia.Tests.Features.LiveActivities;

/// <summary>
/// The mechanical seam tripwire (ADR-0029 D2, the <c>SendPushNotificationSeamTripwireTests</c> pattern
/// verbatim): the ONLY non-test construction site of <c>SendLiveActivityUpdateMessage</c> is the
/// sibling live-activity seam (<c>LiveActivityProducer</c>), plus the record's own declaring file. A
/// handler that hand-rolls the message — bypassing the token gate and the single tripwire-pinned
/// envelope/key construction — trips this test instead of shipping silently.
/// </summary>
public class SendLiveActivityUpdateSeamTripwireTests
{
    private const string ConstructionMarker = "new SendLiveActivityUpdateMessage(";

    private static readonly string[] AllowedFiles =
    [
        Path.Combine("Cleansia.Core.AppServices", "Services", "LiveActivityProducer.cs"),
        Path.Combine("Cleansia.Core.Queue.Abstractions", "Messages", "SendLiveActivityUpdateMessage.cs"),
    ];

    private static readonly string[] ExcludedProjectPrefixes =
    [
        "Cleansia.Tests",
        "Cleansia.IntegrationTests",
        "Cleansia.HostTests",
        "Cleansia.TestUtilities",
    ];

    [Fact]
    public void Only_The_LiveActivity_Producer_Constructs_The_Update_Message()
    {
        var srcRoot = RequireSolutionDirectory();
        var constructingFiles = ProductionProjectDirectories(srcRoot)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(file => File.ReadAllText(file).Contains(ConstructionMarker, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(srcRoot, file))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var offenders = constructingFiles
            .Where(file => !AllowedFiles.Any(allowed => file.EndsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        Assert.True(offenders.Count == 0,
            "A live-activity update message is constructed outside the LiveActivityProducer seam — that " +
            "producer bypasses the token gate and the single envelope/key construction. Route it through " +
            "ILiveActivityProducer.NotifyOrderTransitionAsync. Offenders:\n  " +
            string.Join("\n  ", offenders));

        // Guard against the pin hollowing out (a rename/move would otherwise make it vacuous).
        Assert.Contains(constructingFiles, f => f.EndsWith(AllowedFiles[0], StringComparison.Ordinal));
    }

    private static IEnumerable<string> ProductionProjectDirectories(string srcRoot)
    {
        return Directory.EnumerateDirectories(srcRoot, "Cleansia.*", SearchOption.TopDirectoryOnly)
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                // Cleansia.App is the Angular monorepo (node_modules) — no C# and slow to walk.
                return name != "Cleansia.App"
                    && !ExcludedProjectPrefixes.Any(p => name.Equals(p, StringComparison.Ordinal));
            });
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
