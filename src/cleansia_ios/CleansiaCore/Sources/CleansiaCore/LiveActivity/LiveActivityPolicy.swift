import Foundation

/// The two states a clean's Live Activity can END in. The raw values ARE the content-state wire strings
/// the widget decodes and the backend's `LiveActivityPayloadFactory` writes.
public enum LiveActivityTerminalStatus: String, Sendable, CaseIterable {
    case completed
    case cancelled
}

/// When the ended card leaves the lock screen.
public enum LiveActivityDismissal: Equatable, Sendable {
    case immediate
    case after(Date)
}

/// The staleness + dismissal rules of ADR-0029 D2. It lives in Core because BOTH writers of a card must
/// agree: the app's own `LiveActivityCoordinator` and the backend's `LiveActivityPayloadFactory` (whose
/// constants these mirror) — a local update that disagreed with a pushed one would make the card jump
/// depending on which landed last.
public enum LiveActivityPolicy {
    /// A completed card stays glanceable this long, then leaves.
    public static let completedLinger: TimeInterval = 30 * 60

    private static let minStaleAfterNow: TimeInterval = 4 * 60 * 60
    private static let staleAfterScheduledEnd: TimeInterval = 60 * 60

    /// `max(now + 4h, scheduledEnd + 1h)`: a six-hour booked clean never renders stale mid-service, while
    /// a genuinely stuck one eventually says so instead of lying. Anchored on the BOOKED end, never on a
    /// countdown target — a stale date in the past makes the system draw a placeholder over the card.
    public static func staleDate(scheduledEnd: Date, now: Date) -> Date {
        max(now.addingTimeInterval(minStaleAfterNow), scheduledEnd.addingTimeInterval(staleAfterScheduledEnd))
    }

    /// A dead order must not linger; a finished one is worth one last glance.
    public static func dismissal(for status: LiveActivityTerminalStatus, now: Date) -> LiveActivityDismissal {
        switch status {
        case .cancelled: .immediate
        case .completed: .after(now.addingTimeInterval(completedLinger))
        }
    }
}
