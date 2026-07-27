import CleansiaPartnerApi
import Foundation

/// What the timer hero shows for an order, resolved from status + history.
/// Rendering lives in `OrderTimerCard`; everything here is pure so the clock
/// semantics are unit-testable without a view.
enum OrderTimerPhase: Equatable {
    /// Confirmed, start time still ahead.
    case countdown(secondsRemaining: Int)
    /// Confirmed, the scheduled start has already passed.
    case scheduled(Date)
    /// On the way — the scheduled arrival time.
    case arriving(Date)
    /// In progress — seconds since the job actually started.
    case elapsed(seconds: Int)
    /// Completed — how long the job took, and when it finished.
    case completed(durationMinutes: Int?, finishedAt: Date?)
}

/// Clock math for the order-detail timer hero.
///
/// **Completed duration is `completedAt − startedAt`, never `now − startedAt`.**
/// Android's `OrderTimerCard.kt` computes `now - startedAt` for Completed and
/// relies on its ticker being idle in that phase to freeze the value — but a
/// fresh composition re-reads the wall clock, so opening a week-old completed
/// order there renders "Done in 168h 0m". iOS anchors both ends of the interval,
/// so the number is stable no matter when the order is re-opened. (Android needs
/// the same fix; it is deliberately not made in this branch.)
enum OrderTimer {
    /// Confirmed splits into a "heading out soon" headline inside the last half
    /// hour before the scheduled start (Android `tracker_headline_confirmed_soon`).
    static let headingOutSoonThreshold = 30 * 60

    static func isHeadingOutSoon(secondsRemaining: Int) -> Bool {
        secondsRemaining >= 0 && secondsRemaining < headingOutSoonThreshold
    }

    /// The EARLIEST InProgress stamp, not the latest: an order that is bounced
    /// back and re-started must not reset the cleaner's clock. Taking the minimum
    /// (rather than the first element) also survives an unsorted history and
    /// entries that carry no timestamp.
    static func startedAt(_ history: [OrderStatusTrackDto]) -> Date? {
        history.filter { $0.statusEnum == ._4 }.compactMap(\.createdOn).min()
    }

    /// `completedAt` is the authoritative DB column the domain sets at completion;
    /// the last Completed history entry is the fallback for older rows that
    /// predate it.
    static func finishedAt(_ order: OrderDetail) -> Date? {
        order.completedAt ?? order.statusHistory.filter { $0.statusEnum == ._5 }.compactMap(\.createdOn).max()
    }

    /// Minutes between the start and the finish, or nil when either anchor is
    /// missing or the interval is negative (clock skew between the two writes).
    /// nil means "render no duration", never "0m".
    static func completedDurationMinutes(_ order: OrderDetail, finishedAt finished: Date?) -> Int? {
        guard let started = startedAt(order.statusHistory), let finished else { return nil }
        let seconds = finished.timeIntervalSince(started)
        guard seconds >= 0 else { return nil }
        return Int(seconds / 60)
    }

    /// nil = no card at all.
    ///
    /// New/Pending render nothing: the scheduled datetime they would show is
    /// already printed twice above (compact header + metadata row), so a third
    /// copy is noise. Cancelled renders nothing (Android early-returns too). A
    /// phase whose anchor is missing — Confirmed/OnTheWay without a scheduled
    /// time, InProgress without a start stamp, Completed with neither a duration
    /// nor a finish time — also collapses to nil rather than showing "—" or a
    /// bogus 00:00.
    static func phase(for order: OrderDetail, now: Date) -> OrderTimerPhase? {
        switch order.status {
        case ._2: confirmedPhase(order, now: now)
        case ._3: order.cleaningDateTime.map(OrderTimerPhase.arriving)
        case ._4: inProgressPhase(order, now: now)
        case ._5: completedPhase(order)
        default: nil
        }
    }

    /// "1:42:08" past an hour, "02:08" for shorter jobs (1:1 with Android's
    /// `formatElapsedClock`).
    static func elapsedClock(seconds: Int) -> String {
        let total = max(0, seconds)
        let hours = total / 3600
        let minutes = (total % 3600) / 60
        let remainder = total % 60
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, remainder)
            : String(format: "%02d:%02d", minutes, remainder)
    }

    private static func confirmedPhase(_ order: OrderDetail, now: Date) -> OrderTimerPhase? {
        guard let scheduled = order.cleaningDateTime else { return nil }
        let remaining = scheduled.timeIntervalSince(now)
        return remaining > 0 ? .countdown(secondsRemaining: Int(remaining)) : .scheduled(scheduled)
    }

    private static func inProgressPhase(_ order: OrderDetail, now: Date) -> OrderTimerPhase? {
        guard let started = startedAt(order.statusHistory) else { return nil }
        return .elapsed(seconds: max(0, Int(now.timeIntervalSince(started))))
    }

    private static func completedPhase(_ order: OrderDetail) -> OrderTimerPhase? {
        let finished = finishedAt(order)
        let minutes = completedDurationMinutes(order, finishedAt: finished)
        guard minutes != nil || finished != nil else { return nil }
        return .completed(durationMinutes: minutes, finishedAt: finished)
    }
}
