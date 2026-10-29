import CleansiaCore
import Combine
import Foundation

/// Recovers the current service window from the existing signed-in order refresh, including Home.
/// This adds no timer or list request. Details provide the actual phase timestamps; summaries alone
/// suffice to end a terminal card. Every suspension is guarded against a newer snapshot/session.
@MainActor
final class CustomerLiveActivityRecovery: SessionScopedCache {
    private let client: OrderClient
    private let repository: OrderRepository
    private let liveActivity: OrderLiveActivitySyncing
    private var observer: AnyCancellable?
    private var work: Task<Void, Never>?
    private var pendingOrders: [CustomerOrderSummary]?
    private var generation: UInt64 = 0
    private var sessionGeneration: UInt64 = 0
    private var sessionActive = false

    init(
        client: OrderClient,
        repository: OrderRepository,
        liveActivity: OrderLiveActivitySyncing = LiveActivityBridge()
    ) {
        self.client = client
        self.repository = repository
        self.liveActivity = liveActivity
        observer = repository.$orders.sink { [weak self] orders in self?.schedule(orders) }
    }

    func setSessionActive(_ active: Bool) {
        guard sessionActive != active else { return }
        sessionActive = active
        if active {
            schedule(repository.orders)
        } else {
            sessionGeneration &+= 1
            invalidate()
        }
    }

    func resume(afterRefresh result: ApiResult<Void>? = nil) {
        guard sessionActive else { return }
        liveActivity.resumeRegistrations()
        // Successful publication already scheduled recovery, even if its detail read has finished
        // before another parallel prefetch returns. A failed refresh may retry the cached snapshot.
        if case .some(.success) = result { return }
        schedule(repository.orders)
    }

    func refreshForForeground() async {
        guard sessionActive else { return }
        let session = sessionGeneration
        let result = await repository.refresh()
        guard sessionActive, sessionGeneration == session, !Task.isCancelled else { return }
        resume(afterRefresh: result)
    }

    func clear() async {
        sessionActive = false
        sessionGeneration &+= 1
        invalidate()
    }

    private func invalidate() {
        generation &+= 1
        work?.cancel()
        work = nil
        pendingOrders = nil
    }

    private func owns(_ current: UInt64) -> Bool {
        sessionActive && generation == current && !Task.isCancelled
    }

    private func schedule(_ orders: [CustomerOrderSummary]) {
        guard sessionActive else { return }
        let eligible = orders.filter { !$0.isGuestBooking && OrderStatusGroup.liveActivityStatus($0.status) != nil }
        let terminal = orders.filter {
            !$0.isGuestBooking && OrderLiveActivityState.terminal($0.status) != nil
                && liveActivity.tracks(orderId: $0.id, orderNumber: $0.displayOrderNumber ?? "")
        }
        let currentPage = terminal + Array(eligible.prefix(20))
        if pendingOrders == currentPage, work != nil { return }
        invalidate()
        pendingOrders = currentPage
        let current = generation
        work = Task { [weak self] in
            guard let self else { return }
            defer {
                if generation == current {
                    work = nil
                    pendingOrders = nil
                }
            }
            for order in currentPage where !order.isGuestBooking {
                guard owns(current) else { return }
                if let terminal = OrderLiveActivityState.terminal(order.status) {
                    liveActivity.end(orderId: order.id, orderNumber: order.displayOrderNumber ?? "", status: terminal)
                } else if OrderStatusGroup.liveActivityStatus(order.status) != nil {
                    let result = await client.getById(orderId: order.id)
                    guard owns(current) else { return }
                    if case let .success(detail) = result, detail.id == order.id {
                        liveActivity.synchronize(detail)
                    }
                }
            }
        }
    }
}
