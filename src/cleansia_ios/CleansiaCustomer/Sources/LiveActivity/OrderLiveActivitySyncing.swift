import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The order-tracking screen's seam onto the Live Activity lifecycle. Keeps `OrderDetailViewModel` free of
/// ActivityKit + the iOS 16.2 availability gate (and unit-testable): the VM decides start-vs-end from the
/// order status; this bridge just forwards to `LiveActivityCoordinator`.
@MainActor
protocol OrderLiveActivitySyncing: Sendable {
    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow)
    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow)
    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus)
    func resumeRegistrations()
    func tracks(orderId: String, orderNumber: String) -> Bool
}

extension OrderLiveActivitySyncing {
    func resumeRegistrations() {}
    func tracks(orderId _: String, orderNumber _: String) -> Bool {
        false
    }

    /// Shared by an order-detail read and foreground recovery from the existing order repository.
    func synchronize(_ order: CustomerOrderDetail) {
        guard !order.isGuestBooking, let orderId = order.id, !orderId.isBlank else { return }
        let orderNumber = order.displayOrderNumber ?? ""
        if let status = OrderStatusGroup.liveActivityStatus(order.status), let window = EtaWindow.forOrder(order) {
            start(orderId: orderId, orderNumber: orderNumber, status: status, window: window)
            update(orderId: orderId, orderNumber: orderNumber, status: status, window: window)
        } else if let terminal = OrderLiveActivityState.terminal(order.status) {
            end(orderId: orderId, orderNumber: orderNumber, status: terminal)
        }
    }
}

enum OrderLiveActivityState {
    static func terminal(_ status: OrderStatus?) -> LiveActivityTerminalStatus? {
        if OrderStatusGroup.isCompleted(status) { return .completed }
        if OrderStatusGroup.isCancelled(status) { return .cancelled }
        return nil
    }
}

struct LiveActivityBridge: OrderLiveActivitySyncing {
    // Default-argument construction is nonisolated; lifecycle operations remain on MainActor.
    // swiftlint:disable:next unneeded_synthesized_initializer
    nonisolated init() {}

    func tracks(orderId: String, orderNumber: String) -> Bool {
        guard #available(iOS 16.2, *) else { return false }
        return LiveActivityCoordinator.shared.tracks(orderId: orderId, orderNumber: orderNumber)
    }

    func resumeRegistrations() {
        guard #available(iOS 16.2, *) else { return }
        LiveActivityCoordinator.shared.resumeRegistrations()
    }

    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        guard #available(iOS 16.2, *) else { return }
        LiveActivityCoordinator.shared.start(orderId: orderId, orderNumber: orderNumber, status: status, window: window)
    }

    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        guard #available(iOS 16.2, *) else { return }
        LiveActivityCoordinator.shared.update(
            orderId: orderId,
            orderNumber: orderNumber,
            status: status,
            window: window
        )
    }

    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus) {
        guard #available(iOS 16.2, *) else { return }
        LiveActivityCoordinator.shared.end(orderId: orderId, orderNumber: orderNumber, status: status)
    }
}
