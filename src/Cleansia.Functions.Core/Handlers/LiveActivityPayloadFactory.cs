using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Pure translator from one <see cref="SendLiveActivityUpdateMessage"/> (+ the order's terminal status,
/// when ending) to the ActivityKit <see cref="LiveActivityPush"/> the dumb APNs client sends
/// (ADR-0029 D2/D4). No I/O — the single place the content-state, stale-date, and dismissal-date rules
/// live, so TC-LA-0/1/6 pin them here.
/// </summary>
public static class LiveActivityPayloadFactory
{
    private const int ContentStateVersion = 1;
    private const string AttributesType = "CleanOrderAttributes";

    // The content-state status STRINGS (ADR-0029 D4 — the widget decodes status as a string and maps
    // unknown values to a generic in-service presentation).
    private const string StatusOnTheWay = "onTheWay";
    private const string StatusInProgress = "inProgress";
    private const string StatusCompleted = "completed";
    private const string StatusCancelled = "cancelled";

    // ADR-0029 D2 (CH-D2-3): a booked-long clean never renders stale mid-service; a genuinely stuck one
    // flips to the widget's isStale presentation instead of lying.
    private static readonly TimeSpan MinStaleAfterNow = TimeSpan.FromHours(4);
    private static readonly TimeSpan StaleAfterScheduledEnd = TimeSpan.FromHours(1);

    // ADR-0029 D2 (owner-ratified): a Completed card stays glanceable for 30 min then leaves; a
    // Cancelled card dismisses immediately (a dead order must not linger).
    private static readonly TimeSpan CompletedLinger = TimeSpan.FromMinutes(30);

    // A phase window that is already elapsed renders as a permanently-clamped 00:00 — the exact freeze
    // the phase fields exist to fix — so every window gets at least this much runway.
    private static readonly TimeSpan MinPhaseWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Build the push for this transition. <paramref name="currentStatus"/> is the order's persisted
    /// <c>CurrentStatus</c>, read by the consumer ONLY for an <c>end</c> event (the message's
    /// <c>EventKey</c> is <c>end</c> for both Completed and Cancelled — they differ in status string and
    /// dismissal window); it is ignored for start/update.
    /// </summary>
    public static LiveActivityPush Build(SendLiveActivityUpdateMessage message, OrderStatus? currentStatus, DateTimeOffset now)
    {
        var isCancelled = message.EventKey == LiveActivityEventKeys.End && currentStatus == OrderStatus.Cancelled;

        var status = message.EventKey switch
        {
            LiveActivityEventKeys.Start => StatusOnTheWay,
            LiveActivityEventKeys.Update => StatusInProgress,
            LiveActivityEventKeys.End => isCancelled ? StatusCancelled : StatusCompleted,
            _ => StatusInProgress,
        };

        var (phaseStart, phaseEnd) = PhaseWindow(status, message);

        var contentState = new LiveActivityContentState(
            V: ContentStateVersion,
            Status: status,
            OrderNumber: message.OrderNumber,
            ScheduledStart: message.ScheduledStart,
            ScheduledEnd: message.ScheduledEnd,
            PhaseStart: phaseStart,
            PhaseEnd: phaseEnd);

        var staleDate = StaleDate(message.ScheduledEnd, now);

        DateTimeOffset? dismissalDate = message.EventKey == LiveActivityEventKeys.End
            ? (isCancelled ? now : now + CompletedLinger)
            : null;

        var isStart = message.EventKey == LiveActivityEventKeys.Start;

        return new LiveActivityPush(
            Event: message.EventKey,
            ContentState: contentState,
            Timestamp: message.TransitionAtUtc,
            StaleDate: staleDate,
            DismissalDate: dismissalDate,
            AttributesType: isStart ? AttributesType : null,
            Attributes: isStart ? new LiveActivityStartAttributes(message.OrderNumber) : null);
    }

    /// <summary>
    /// The ACTUAL phase window the widget's countdown is anchored to, replacing the booked window that
    /// clamps to a frozen 00:00 once it elapses (a cleaner who runs late is the common case).
    ///
    /// <para>The phase START is <see cref="SendLiveActivityUpdateMessage.TransitionAtUtc"/> — already the
    /// <c>OrderStatusTrack.CreatedOn</c> of THIS transition, i.e. the order's own persisted record of when
    /// the cleaner moved. No new source: a redelivered message therefore rebuilds the identical window,
    /// and re-reading the history in the consumer could only disagree with the timestamp APNs already
    /// orders the update by.</para>
    /// </summary>
    private static (DateTimeOffset? Start, DateTimeOffset? End) PhaseWindow(string status, SendLiveActivityUpdateMessage message)
    {
        var start = message.TransitionAtUtc;

        return status switch
        {
            // Expected arrival: the booked start, unless the cleaner left so late that it is already
            // behind us — then a floor from departure.
            StatusOnTheWay => (start, Later(message.ScheduledStart, start + MinPhaseWindow)),

            // The booked duration IS the order's estimated time (the producer derives scheduledEnd as
            // cleaningDateTime + EstimatedTime), re-anchored to when work actually started.
            StatusInProgress => (start, start + Longer(message.ScheduledEnd - message.ScheduledStart, MinPhaseWindow)),

            // Completed/cancelled: nothing is counting down, and a stale window would keep the card ticking.
            _ => (null, null),
        };
    }

    private static DateTimeOffset Later(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private static TimeSpan Longer(TimeSpan left, TimeSpan right) => left > right ? left : right;

    private static DateTimeOffset StaleDate(DateTimeOffset scheduledEnd, DateTimeOffset now)
    {
        var floor = now + MinStaleAfterNow;
        var afterEnd = scheduledEnd + StaleAfterScheduledEnd;
        return afterEnd > floor ? afterEnd : floor;
    }
}
