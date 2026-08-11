using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Functions;

namespace Cleansia.Tests.Functions;

/// <summary>
/// What the queue-polling budget is actually priced against, and the two ways that number can be wrong.
///
/// <para>The Functions extension configures polling for ALL queues at once — <c>host.json</c> has no
/// per-queue override — so the cost of <c>maxPollingInterval</c> is (listeners × polls), and the listener
/// count is therefore part of the setting's meaning rather than trivia. It is derived here from the
/// triggers instead of being written down, because a hand-typed count in a comment goes stale silently.</para>
///
/// <para>The second failure is the one that already happened: <c>storage.bicep</c> declares itself to
/// mirror <c>QueueNames.cs</c> and had drifted by one — <c>live-activity-dispatch</c> and its poison
/// companion existed in code, in the triggers and in ADR-0029, but were never provisioned. Nothing broke,
/// because both the producer and the listener create a missing queue on first use, which is exactly why
/// the drift survived: the only visible symptom is that two queues are unmanaged by the template that
/// claims to own them.</para>
/// </summary>
public class QueueListenerInventoryTests
{
    private static IReadOnlyList<string> DeclaredQueueNames() =>
        typeof(QueueNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> TriggeredQueueNames() =>
        typeof(SendEmailFunction).Assembly.GetTypes()
            .Where(type => type.Namespace == "Cleansia.Functions.Functions")
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType.Name == "QueueTriggerAttribute")
            .Select(attribute => (string)attribute.ConstructorArguments[0].Value!)
            .Order(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void EveryDeclaredQueueIsProvisionedByTheStorageTemplate()
    {
        var bicep = File.ReadAllText(RepoPath("deploy", "bicep", "modules", "storage.bicep"));
        var block = Regex.Match(bicep, @"var queueBaseNames = \[(?<body>[^\]]*)\]", RegexOptions.Singleline);

        Assert.True(block.Success, "storage.bicep no longer declares a queueBaseNames array.");

        var provisioned = Regex.Matches(block.Groups["body"].Value, @"'(?<name>[^']+)'")
            .Select(match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(DeclaredQueueNames(), provisioned);
    }

    [Fact]
    public void EveryQueueTriggerNamesADeclaredQueueOrItsPoisonCompanion()
    {
        var declared = DeclaredQueueNames();
        var expected = declared.Concat(declared.Select(name => $"{name}-poison"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, TriggeredQueueNames());
    }

    /// <summary>
    /// The figure the polling interval is costed against: one listener per declared queue plus one per
    /// poison companion. If this moves, the telemetry arithmetic behind
    /// <see cref="HostJsonTelemetryCostTests"/> moves with it.
    /// </summary>
    [Fact]
    public void TheListenerCountIsTwicePerDeclaredQueue()
    {
        Assert.Equal(DeclaredQueueNames().Count * 2, TriggeredQueueNames().Count);
        Assert.Equal(14, TriggeredQueueNames().Count);
    }

    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }
}
