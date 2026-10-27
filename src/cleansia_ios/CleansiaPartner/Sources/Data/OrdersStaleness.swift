import CleansiaCore
import Foundation

enum OrdersPane: CaseIterable {
    case available
    case active
    case history
}

/// Lifecycle mutation tag → the panes whose displayed data it changes (the
/// `OrdersRepository.kt:181-192` parity). The third pane is left warm so its
/// cache keeps serving on the next freshness check.
enum OrdersMutation {
    case takeOrder
    case acceptWorkContract
    case notifyOnTheWay
    case startOrder
    case markCashCollected
    case markMyPartDone
    case completeOrder
    case declinePreferredOffer
    case reportLockout
    case dropOrder

    var affectedPanes: [OrdersPane] {
        switch self {
        case .takeOrder, .notifyOnTheWay, .dropOrder: [.available, .active]
        case .acceptWorkContract, .startOrder, .markCashCollected: [.active]
        case .completeOrder: [.active, .history]
        // The order is back with the whole board the instant the hold ends, and the cleaner's own
        // jobs did not change.
        case .declinePreferredOffer: [.available]
        // The job stays as it is until an administrator confirms the report.
        case .reportLockout: []
        // The job stays in progress, and no list shows who on it has finished.
        case .markMyPartDone: []
        }
    }
}

/// Dashboard, per-pane + per-order freshness watermarks (the `OrdersRepository.kt:159-192`
/// staleness parity). A pane/order is "fresh" within `window` of its last
/// `markFresh`; `ensureFreshOrCached` callers skip the network while warm so a
/// resume from the detail screen is a no-op (no spinner flash). User pulls
/// bypass this entirely. `MainActor`-isolated.
@MainActor
final class OrdersStaleness: SessionScopedCache {
    private let window: TimeInterval
    private let now: () -> Date
    private var paneMarks: [OrdersPane: Date] = [:]
    private var orderMarks: [String: Date] = [:]
    private var dashboardMark: Date?
    private(set) var dashboardRevision = 0
    private(set) var sessionEpoch = 0

    init(window: TimeInterval = 30, now: @escaping () -> Date = Date.init) {
        self.window = window
        self.now = now
    }

    var isDashboardStale: Bool {
        guard let dashboardMark else { return true }
        return now().timeIntervalSince(dashboardMark) >= Self.dashboardWindow
    }

    func markDashboardFresh() {
        dashboardMark = now()
    }

    func invalidateDashboard() {
        dashboardRevision += 1
        dashboardMark = nil
    }

    func isPaneStale(_ pane: OrdersPane) -> Bool {
        isStale(paneMarks[pane])
    }

    func markPaneFresh(_ pane: OrdersPane) {
        paneMarks[pane] = now()
    }

    func isOrderStale(_ orderId: String) -> Bool {
        isStale(orderMarks[orderId])
    }

    func markOrderFresh(_ orderId: String) {
        orderMarks[orderId] = now()
    }

    func invalidateOrder(_ orderId: String) {
        orderMarks[orderId] = nil
    }

    func invalidatePanes(for mutation: OrdersMutation) {
        for pane in mutation.affectedPanes {
            paneMarks[pane] = nil
        }
    }

    func clear() async {
        sessionEpoch += 1
        invalidateDashboard()
        paneMarks.removeAll()
        orderMarks.removeAll()
    }

    private static let dashboardWindow: TimeInterval = 60

    private func isStale(_ mark: Date?) -> Bool {
        guard let mark else { return true }
        return now().timeIntervalSince(mark) >= window
    }
}
