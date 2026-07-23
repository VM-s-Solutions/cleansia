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
    case notifyOnTheWay
    case startOrder
    case markCashCollected
    case completeOrder

    var affectedPanes: [OrdersPane] {
        switch self {
        case .takeOrder, .notifyOnTheWay: [.available, .active]
        case .startOrder, .markCashCollected: [.active]
        case .completeOrder: [.active, .history]
        }
    }
}

/// Per-pane + per-order freshness watermarks (the `OrdersRepository.kt:159-192`
/// staleness parity). A pane/order is "fresh" within `window` of its last
/// `markFresh`; `ensureFreshOrCached` callers skip the network while warm so a
/// resume from the detail screen is a no-op (no spinner flash). User pulls
/// bypass this entirely. `MainActor`-isolated — only the VM touches it.
@MainActor
final class OrdersStaleness: SessionScopedCache {
    private let window: TimeInterval
    private let now: () -> Date
    private var paneMarks: [OrdersPane: Date] = [:]
    private var orderMarks: [String: Date] = [:]

    init(window: TimeInterval = 30, now: @escaping () -> Date = Date.init) {
        self.window = window
        self.now = now
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
        paneMarks.removeAll()
        orderMarks.removeAll()
    }

    private func isStale(_ mark: Date?) -> Bool {
        guard let mark else { return true }
        return now().timeIntervalSince(mark) >= window
    }
}
