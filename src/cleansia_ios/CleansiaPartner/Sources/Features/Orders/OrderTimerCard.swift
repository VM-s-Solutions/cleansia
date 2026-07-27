import CleansiaCore
import SwiftUI

/// The hero line above the segmented tracker bar: a one-line eyebrow plus the
/// phase-appropriate big number — a live elapsed clock while cleaning, a
/// countdown before the start, the arrival time on the way, and the job duration
/// once done (`OrderTimerCard.kt` parity).
///
/// Rendered only for Confirmed / OnTheWay / InProgress / Completed. New, Pending
/// and Cancelled render nothing — see `OrderTimer.phase(for:now:)` for why.
struct OrderTimerCard: View {
    let order: OrderDetail
    let locale: Locale

    /// Phase-aware ticking, matching Android: 1 Hz while the job runs so the
    /// clock does not lurch, once a minute for the countdown, and no timeline at
    /// all in the static phases so a settled order costs nothing.
    private var tickInterval: TimeInterval? {
        switch order.status {
        case ._4: 1
        case ._2: 60
        default: nil
        }
    }

    var body: some View {
        // Only the text block ticks — wrapping the whole sheet section would
        // re-lay out the scroll content once a second.
        if let tickInterval {
            TimelineView(.periodic(from: .now, by: tickInterval)) { context in
                content(now: context.date)
            }
        } else {
            content(now: Date())
        }
    }

    @ViewBuilder
    private func content(now: Date) -> some View {
        if let phase = OrderTimer.phase(for: order, now: now) {
            VStack(alignment: .leading, spacing: 2) {
                Text(headline(phase))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                // The brand face has no monospaced digits, so the 1 Hz clock
                // shifts width a little as digits change; Android accepted the
                // same jitter rather than dropping to a system mono font.
                Text(primary(phase))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .lineLimit(1)
                if let finishedAt = finishedAtSubline(phase) {
                    Text(L10n.Orders.trackerFinishedAt(OrdersFormat.relativeDateTime(finishedAt, locale: locale)))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            // No bottom inset: the tracker bar is rendered directly below in a
            // zero-spacing stack so the two read as one hero block.
            .frame(maxWidth: .infinity, alignment: .leading)
            .id(locale.identifier)
        }
    }

    private func headline(_ phase: OrderTimerPhase) -> String {
        switch phase {
        case let .countdown(secondsRemaining):
            OrderTimer.isHeadingOutSoon(secondsRemaining: secondsRemaining)
                ? L10n.Orders.trackerHeadlineConfirmedSoon
                : L10n.Orders.trackerHeadlineConfirmed
        case .scheduled:
            L10n.Orders.trackerHeadlineConfirmed
        case .arriving:
            L10n.Orders.trackerHeadlineOnTheWay
        case .elapsed:
            L10n.Orders.trackerHeadlineInProgress
        case .completed:
            L10n.Orders.trackerHeadlineDone
        }
    }

    private func primary(_ phase: OrderTimerPhase) -> String {
        switch phase {
        case let .countdown(secondsRemaining):
            L10n.Orders.trackerCountdownStartsIn(durationText(minutes: secondsRemaining / 60))
        case let .scheduled(date):
            OrdersFormat.relativeDateTime(date, locale: locale)
        case let .arriving(date):
            L10n.Orders.trackerSubtitleOnTheWayArriving(OrdersFormat.timeOnly(date, locale: locale))
        case let .elapsed(seconds):
            OrderTimer.elapsedClock(seconds: seconds)
        case let .completed(durationMinutes, finishedAt):
            durationMinutes.map { L10n.Orders.trackerCompletedIn(durationText(minutes: $0)) }
                ?? OrdersFormat.relativeDateTime(finishedAt, locale: locale)
        }
    }

    /// The "Finished at …" sub-line only earns its place when the big number is
    /// the duration; with no duration the primary line already IS the completion
    /// timestamp, and printing it twice reads like a bug.
    private func finishedAtSubline(_ phase: OrderTimerPhase) -> Date? {
        guard case let .completed(durationMinutes, finishedAt) = phase, durationMinutes != nil else { return nil }
        return finishedAt
    }

    private func durationText(minutes: Int) -> String {
        let hours = minutes / 60
        let remainder = minutes % 60
        return hours > 0
            ? L10n.Orders.durationHoursMinutes(hours, remainder)
            : L10n.Orders.durationMinutesOnly(remainder)
    }
}
