namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// The mechanical seam tripwire: after the producer consolidation, the ONLY non-test construction
/// sites of <c>SendPushNotificationMessage</c> are the shared notify seam
/// (<c>NotificationProducer</c>) and the sitewide-promo fan-out (promo is excluded from feed v1),
/// plus the record's own declaring file. A new producer that hand-rolls the message — sending a
/// push without its feed row — trips this test instead of shipping silently.
/// </summary>
public class SendPushNotificationSeamTripwireTests
{
    private const string ConstructionMarker = "new SendPushNotificationMessage(";

    private static readonly string[] AllowedFiles =
    [
        Path.Combine("Cleansia.Core.AppServices", "Services", "NotificationProducer.cs"),
        Path.Combine("Cleansia.Functions.Core", "Handlers", "SendSitewidePromoFanoutHandler.cs"),
        Path.Combine("Cleansia.Core.Queue.Abstractions", "Messages", "SendPushNotificationMessage.cs"),
    ];

    private static readonly string[] ExcludedProjectPrefixes =
    [
        "Cleansia.Tests",
        "Cleansia.IntegrationTests",
        "Cleansia.HostTests",
        "Cleansia.TestUtilities",
    ];

    [Fact]
    public void Only_The_Notify_Seam_And_The_Promo_FanOut_Construct_The_Push_Message()
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
            "A push message is constructed outside the shared notify seam — that producer sends a push " +
            "WITHOUT its in-app feed row. Route it through INotificationProducer.NotifyAsync. Offenders:\n  " +
            string.Join("\n  ", offenders));

        // Guard against the pin hollowing out (a rename/move would otherwise make it vacuous).
        Assert.Contains(constructingFiles, f => f.EndsWith(AllowedFiles[0], StringComparison.Ordinal));
        Assert.Contains(constructingFiles, f => f.EndsWith(AllowedFiles[1], StringComparison.Ordinal));
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
