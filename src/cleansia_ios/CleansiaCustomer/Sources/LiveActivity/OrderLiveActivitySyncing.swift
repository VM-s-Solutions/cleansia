import CleansiaCore
import Foundation

/// The order-tracking screen's seam onto the Live Activity lifecycle. Keeps `OrderDetailViewModel` free of
/// ActivityKit + the iOS 16.2 availability gate (and unit-testable): the VM decides start-vs-end from the
/// order status; this bridge just forwards to `LiveActivityCoordinator`.
protocol OrderLiveActivitySyncing: Sendable {
    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow)
    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow)
    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus)
}

struct LiveActivityBridge: OrderLiveActivitySyncing {
    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.start(
                orderId: orderId,
                orderNumber: orderNumber,
                status: status,
                window: window
            )
        }
    }

    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.update(
                orderId: orderId,
                orderNumber: orderNumber,
                status: status,
                window: window
            )
        }
    }

    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.end(orderId: orderId, orderNumber: orderNumber, status: status)
        }
    }
}
