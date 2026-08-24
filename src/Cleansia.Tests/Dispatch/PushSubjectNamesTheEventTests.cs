using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The rule the whole collision family taught us: <b>a push's dedup subject must name the EVENT, never
/// its parent.</b>
///
/// <para><c>MessageKeys.Push(userId, eventKey, subject)</c> is enforced UNIQUE on the outbox as
/// <c>(QueueName, MessageKey)</c>, and the violation lands inside the pipeline's <c>CommitAsync</c> —
/// after the handler returned, where no handler-level <c>catch</c> can reach it. So a collision is not
/// a missing notification: it rolls the business transaction back. What that cost, four times over:</para>
/// <list type="bullet">
///   <item><c>order.cleaner_assigned</c> keyed on the order — the SECOND cleaner taking an ordinary
///   two-seat job got a 500 and lost the seat, so the job could never be fully crewed.</item>
///   <item><c>dispute.reply</c> keyed on the dispute — the second staff reply in any conversation was
///   never saved, and support saw a 500.</item>
///   <item><c>order.refunded</c> keyed on the order, from three different handlers — the second refund
///   an order ever saw failed AFTER Stripe had settled.</item>
///   <item><c>order.preferred_offer_closed</c> keyed on the order — a second reservation, which the
///   aggregate explicitly supports, collided with the first.</item>
/// </list>
///
/// <para><b>Why this test is source-reading rather than behavioural.</b> Each of those four was covered
/// by a handler test that passed throughout, because a mocked producer records the call and never
/// consults an index. Only bytes in Postgres can catch the collision itself
/// (<see cref="AssignmentPushKeyCollisionTests"/> does that for one family), and no reasonable suite
/// enumerates every pair of producers. So this reads the CALL SITES and fails on the shape that has
/// been wrong every time: a subject that is exactly a bare parent identifier.</para>
/// </summary>
public class PushSubjectNamesTheEventTests
{
    /// <summary>
    /// A subject argument that is exactly one of these, with nothing composed onto it, is the shape
    /// that produced all four defects. A composed subject — <c>$"{order.Id}:{round}"</c>, an
    /// <c>assignment.Id</c>, a <c>refund.RefundId</c> — is fine and does not match.
    /// </summary>
    private static readonly Regex BareParentSubject = new(
        @"^\s*(order|dispute)\.(Id|OrderId)\s*,\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Files whose subject is legitimately the parent because the event happens ONCE per parent, so a
    /// repeat genuinely IS the same notification and collapsing it is the intended behaviour. Each entry
    /// is a claim that the event cannot recur for one parent — add to this list only with that argument.
    ///
    /// <para>Two reasons qualify, and the guard cannot see either — which is why they are written down
    /// rather than detected: the event is TERMINAL for its parent (an order is cancelled once), or the
    /// RECIPIENT varies so the user segment of the key already separates the events.</para>
    /// </summary>
    private static readonly HashSet<string> DeliberatelyKeyedOnTheParent = new(StringComparer.Ordinal)
    {
        // Terminal transitions. Each is a one-way move on the order's status axis, refused a second
        // time by the transition guard, so a repeated notice is genuinely the same news.
        "CancelOrder.cs",
        "CompleteOrder.cs",
        "StartOrder.cs",
        "NotifyOnTheWay.cs",
        "ConfirmRecurringOrder.cs",
        "AutoCancelStaleRecurringOrders.cs",
        "CleanupStalePendingOrders.cs",

        // Swept reminders, each suppressed by its own stamp on the row it reminds about, so the sweep
        // cannot re-send for the same order however many times it ticks.
        "SendPreCleaningReminders.cs",
        "SendRecurringOrderReminders.cs",

        // The RECIPIENT discriminates: this loops the crew and sends one per cleaner, so two rows mean
        // two user segments rather than one repeated key — and an order is cancelled once regardless.
        "OrderAssignmentCancellationNotifier.cs",

        // Stripe webhooks, already idempotent upstream on the provider's event id: a redelivery is the
        // same payment event and collapsing it is the intended behaviour.
        "HandlePaymentNotification.cs",
    };

    [Fact]
    public void No_Push_Subject_Is_A_Bare_Parent_Identifier()
    {
        var featureFiles = Directory
            .EnumerateFiles(FeaturesDirectory(), "*.cs", SearchOption.AllDirectories)
            .ToList();

        Assert.True(
            featureFiles.Count >= 100,
            $"Expected the Features tree; found only {featureFiles.Count} files. The walk drifted.");

        var offenders = new List<string>();
        foreach (var file in featureFiles)
        {
            if (DeliberatelyKeyedOnTheParent.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!BareParentSubject.IsMatch(lines[index]))
                {
                    continue;
                }

                // Only the argument immediately before the cancellation token of a NotifyAsync call is
                // the subject; the same text appears harmlessly as a dozen other arguments.
                if (IsSubjectOfANotifyCall(lines, index))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}  {lines[index].Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These push subjects are a bare parent identifier. Two events about one parent then mint one "
                + "key, and the outbox's unique index turns the second into a rolled-back transaction — "
                + "not a missing push. Name the EVENT instead (the assignment row, the message, the "
                + "refund, the round), or add the file to DeliberatelyKeyedOnTheParent with the argument "
                + "for why the event cannot recur:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// The subject sits directly before <c>cancellationToken</c>, and the call opens with
    /// <c>NotifyAsync(</c> a few lines above. Both are checked so an unrelated argument that happens to
    /// read <c>order.Id,</c> does not trip the guard.
    /// </summary>
    private static bool IsSubjectOfANotifyCall(string[] lines, int index)
    {
        var next = index + 1 < lines.Length ? lines[index + 1] : string.Empty;
        if (!next.Contains("cancellationToken", StringComparison.Ordinal))
        {
            return false;
        }

        for (var back = index; back >= Math.Max(0, index - 14); back--)
        {
            if (lines[back].Contains("NotifyAsync(", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FeaturesDirectory()
    {
        var assembly = Path.GetDirectoryName(typeof(TakeOrder).Assembly.Location)!;
        var directory = new DirectoryInfo(assembly);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory.");
        var features = Path.Combine(directory!.FullName, "Cleansia.Core.AppServices", "Features");
        Assert.True(Directory.Exists(features), $"Features directory not found at {features}.");
        return features;
    }
}
