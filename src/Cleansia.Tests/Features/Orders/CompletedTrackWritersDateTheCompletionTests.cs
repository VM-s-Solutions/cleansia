using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The revenue report reads <c>Order.CompletedAt</c>, and <c>Order.AddOrderStatus</c> persists
/// <c>CurrentStatus = Completed</c> with <c>CompletedAt = null</c> without complaint. An order in that
/// state is revenue of no month, and the only symptom is money missing from a report — which is how the
/// administrator's override shipped. Two writers date the completion today: <c>CompleteOrder</c> through
/// <c>Order.CompleteOrder</c>, the override through <c>Order.MarkCompletedAt</c>. This scan makes a third
/// that forgets go red at the seam where it is written, in a message that names the file.
///
/// <para>A writer is a completion writer when the status it appends is not a literal other than
/// <c>Completed</c> and is not the order's own <c>CurrentStatus</c> — the same-value row that
/// re-advertises a seat cannot make an order Completed that was not.</para>
/// </summary>
public sealed class CompletedTrackWritersDateTheCompletionTests
{
    private static readonly Regex TrackCreate = new(
        @"OrderStatusTrack\.Create\(\s*(?<status>[^,]+?)\s*,",
        RegexOptions.Compiled);

    private static readonly Regex NonCompletedLiteral = new(
        @"^OrderStatus\.(?!Completed$)\w+$",
        RegexOptions.Compiled);

    private static readonly Regex ReappendOfCurrent = new(
        @"^\w+\.CurrentStatus$",
        RegexOptions.Compiled);

    private static readonly string[] DatingCalls = ["CompleteOrder(", "MarkCompletedAt("];

    private static readonly string[] KnownCompletionWriters =
    [
        "Cleansia.Core.AppServices/Features/Orders/AdminOverrideOrderStatus.cs",
        "Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs",
    ];

    [Fact]
    public void Every_Writer_That_Can_Make_An_Order_Completed_Dates_The_Completion()
    {
        var undated = CompletionWriters()
            .Where(w => !DatingCalls.Any(call => w.Text.Contains(call, StringComparison.Ordinal)))
            .Select(w => w.File)
            .ToList();

        Assert.True(
            undated.Count == 0,
            "These files append a track that can be Completed without dating the completion — the order "
            + "becomes revenue of no month. Call Order.CompleteOrder or Order.MarkCompletedAt in the same "
            + "handler:\n  " + string.Join("\n  ", undated));
    }

    [Fact]
    public void The_Scan_Sees_Exactly_The_Known_Completion_Writers()
    {
        Assert.Equal(
            KnownCompletionWriters.Order(StringComparer.Ordinal),
            CompletionWriters().Select(w => w.File).Order(StringComparer.Ordinal));
    }

    private static List<(string File, string Text)> CompletionWriters() =>
        ProductionSources()
            .Select(path => (File: Normalize(Path.GetRelativePath(SourceRoot(), path)), Text: File.ReadAllText(path)))
            .Where(f => TrackCreate.Matches(f.Text)
                .Select(m => m.Groups["status"].Value)
                .Any(status => !NonCompletedLiteral.IsMatch(status) && !ReappendOfCurrent.IsMatch(status)))
            .ToList();

    private static IEnumerable<string> ProductionSources() =>
        Directory.EnumerateDirectories(SourceRoot(), "Cleansia.*")
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                return !name.EndsWith("Tests", StringComparison.Ordinal)
                    && name != "Cleansia.TestUtilities"
                    && name != "Cleansia.App";
            })
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.EnumerateFiles("Cleansia.Api.sln").Any())
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Could not find Cleansia.Api.sln walking up from {AppContext.BaseDirectory}.");
        return dir!.FullName;
    }
}
