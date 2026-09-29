using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Functions.Functions;

namespace Cleansia.Tests.Functions;

/// <summary>
/// With storage on managed identity (prod, E-4) the Functions host has no storage connection string: it
/// binds each <c>[QueueTrigger(Connection = X)]</c> identity-based from the app settings
/// <c>X__queueServiceUri</c> and <c>X__credential</c>, and it prefers a <c>ConnectionStrings__X</c> entry
/// when one exists. A trigger whose connection name has no such pair in the template, or still has a
/// connection string beside it, listens to nothing in production — no error, the queue just fills.
/// </summary>
public class QueueTriggerIdentityConnectionTests
{
    private const int MinimumQueueTriggers = 18;

    [Fact]
    public void Every_queue_trigger_connection_binds_identity_based_under_managed_identity()
    {
        var connections = QueueTriggerConnections();
        var settings = ManagedIdentityStorageSettings();

        foreach (var name in connections)
        {
            Assert.Matches($@"(?m)^\s*{name}__queueServiceUri:\s*storage\.outputs\.queueEndpoint\s*$", settings);
            Assert.Matches($@"(?m)^\s*{name}__credential:\s*'managedidentity'\s*$", settings);
        }

        Assert.DoesNotContain("ConnectionStrings__", settings, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> QueueTriggerConnections()
    {
        var triggers = typeof(SendEmailFunction).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType.Name == "QueueTriggerAttribute")
            .ToList();

        Assert.True(
            triggers.Count >= MinimumQueueTriggers,
            $"Expected at least {MinimumQueueTriggers} queue triggers, found {triggers.Count} — the reflection walk stopped seeing them.");

        // A trigger without a Connection binds AzureWebJobsStorage, which functionApp.bicep already binds
        // identity-based, so only the named connections need settings of their own.
        return triggers
            .Select(attribute => attribute.NamedArguments
                .Where(argument => argument.MemberName == "Connection")
                .Select(argument => (string?)argument.TypedValue.Value)
                .SingleOrDefault())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string ManagedIdentityStorageSettings()
    {
        var main = File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep"));
        var block = Regex.Match(
            main,
            @"var storageSettings = storageManagedIdentityEnabled\s*\?\s*\{(?<settings>[^}]*)\}",
            RegexOptions.Singleline);

        Assert.True(block.Success, "main.bicep no longer declares the storageSettings managed-identity branch.");
        return block.Groups["settings"].Value;
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
