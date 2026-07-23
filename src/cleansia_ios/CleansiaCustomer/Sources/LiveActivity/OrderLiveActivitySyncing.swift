import Foundation

/// The order-tracking screen's seam onto the Live Activity lifecycle. Keeps `OrderDetailViewModel` free of
/// ActivityKit + the iOS 16.2 availability gate (and unit-testable): the VM decides start-vs-end from the
/// order status; this bridge just forwards to `LiveActivityCoordinator`.
protocol OrderLiveActivitySyncing: Sendable {
    func start(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date)
    func update(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date)
    func end(orderId: String)
}

struct LiveActivityBridge: OrderLiveActivitySyncing {
    func start(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.start(
                orderId: orderId,
                orderNumber: orderNumber,
                status: status,
                scheduledStart: scheduledStart,
                scheduledEnd: scheduledEnd
            )
        }
    }

    func update(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.update(
                orderId: orderId,
                orderNumber: orderNumber,
                status: status,
                scheduledStart: scheduledStart,
                scheduledEnd: scheduledEnd
            )
        }
    }

    func end(orderId: String) {
        guard #available(iOS 16.2, *) else { return }
        Task { @MainActor in
            LiveActivityCoordinator.shared.end(orderId: orderId)
        }
    }
}
