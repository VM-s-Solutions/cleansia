import Combine
import Foundation

/// Container-lived unread count behind the bell: fetched when the bell's screen
/// loads and on app-foreground, bumped locally on a foreground push, decremented
/// optimistically on a row tap. Registered in the `SessionScopedCacheRegistry`
/// so every wipe trigger clears it. → /mobile-app/patterns#session-wipe
@MainActor
public final class NotificationBadgeModel: ObservableObject, SessionScopedCache {
    @Published public private(set) var unreadCount = 0

    private let client: NotificationFeedClient
    private let feedEventKeys: Set<String>
    private var refreshing = false
    private var sessionGeneration = 0

    /// `feedEventKeys` are the event keys the app's feed shows a row for.
    public init(client: NotificationFeedClient, feedEventKeys: Set<String>) {
        self.client = client
        self.feedEventKeys = feedEventKeys
    }

    public static func label(for count: Int) -> String? {
        guard count > 0 else { return nil }
        return count > 99 ? "99+" : "\(count)"
    }

    /// Failures keep the last value silently — the badge is a hint, and a
    /// signed-out or offline fetch must never snackbar or crash.
    public func refresh() async {
        if refreshing { return }
        let generation = sessionGeneration
        refreshing = true
        defer { if generation == sessionGeneration { refreshing = false } }
        let result = await client.unreadCount()
        guard generation == sessionGeneration, case let .success(count) = result else { return }
        unreadCount = max(0, count)
    }

    /// A push received while the app runs bumps the badge without a refetch —
    /// only for keys that actually get a feed row (unknown keys don't).
    public func notePushReceived(eventKey: String) {
        guard feedEventKeys.contains(eventKey) else { return }
        unreadCount += 1
    }

    public func noteMarkedRead() {
        unreadCount = max(0, unreadCount - 1)
    }

    public func clear() async {
        sessionGeneration += 1
        refreshing = false
        unreadCount = 0
    }
}
