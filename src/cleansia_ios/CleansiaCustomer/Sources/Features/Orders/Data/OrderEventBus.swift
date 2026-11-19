import Combine
import Foundation

struct OrderEvent: Equatable {
    let orderId: String
}

/// The `OrderEventBus.kt` parity: the app delegate emits the order id of every order push, shown or
/// tapped, and an open order detail with that id refetches.
@MainActor
final class OrderEventBus {
    private let subject = PassthroughSubject<OrderEvent, Never>()

    nonisolated init() {}

    var events: AnyPublisher<OrderEvent, Never> {
        subject.eraseToAnyPublisher()
    }

    func emit(orderId: String) {
        subject.send(OrderEvent(orderId: orderId))
    }
}
