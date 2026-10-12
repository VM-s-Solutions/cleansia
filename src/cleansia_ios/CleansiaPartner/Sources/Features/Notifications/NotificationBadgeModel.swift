import CleansiaCore
import Combine
import Foundation

/// Container-lived unread count behind the dashboard bell (FD-AC5): fetched on
/// dashboard load + app-foreground, bumped locally on a foreground push,
/// decremented optimistically on a row tap. Registered in the
/// `SessionScopedCacheRegistry` so sign-out / forced-401 wipes it (E9/S11).
@MainActor
final class NotificationBadgeModel: ObservableObject, SessionScopedCache {
    @Published private(set) var unreadCount = 0

    private let client: NotificationFeedClient
    private var refreshing = false
    private var sessionGeneration = 0

    init(client: NotificationFeedClient) {
        self.client = client
    }

    static func label(for count: Int) -> String? {
        guard count > 0 else { return nil }
        return count > 99 ? "99+" : "\(count)"
    }

    /// Failures keep the last value silently — the badge is a hint, and a
    /// signed-out or offline fetch must never snackbar or crash.
    func refresh() async {
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
    func notePushReceived(eventKey: String) {
        guard PartnerFeedEventKeys.contains(eventKey) else { return }
        unreadCount += 1
    }

    func noteMarkedRead() {
        unreadCount = max(0, unreadCount - 1)
    }

    func clear() async {
        sessionGeneration += 1
        refreshing = false
        unreadCount = 0
    }
}
