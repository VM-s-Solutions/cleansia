import Combine
import Foundation

struct OrderEvent: Equatable {
    let orderId: String
}

/// Seam for a future push-triggered refetch (the `OrderEventBus.kt` parity). A
/// push handler would `emit(orderId)`; the OrderDetail VM filters to its own id
/// and refetches. Customer push registration is NOT built in this slice (that
/// was partner T-0311) — the 5-minute poller + refresh-on-appear cover refresh
/// until customer push lands, so nothing emits here yet.
final class OrderEventBus: Sendable {
    private let subject = PassthroughSubject<OrderEvent, Never>()

    var events: AnyPublisher<OrderEvent, Never> {
        subject.eraseToAnyPublisher()
    }

    func emit(orderId: String) {
        subject.send(OrderEvent(orderId: orderId))
    }
}
