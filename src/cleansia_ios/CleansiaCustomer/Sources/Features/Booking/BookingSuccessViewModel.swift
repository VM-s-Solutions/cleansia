import CleansiaCustomerApi
import Foundation

/// Loads the just-confirmed order so the success screen can show its arrival
/// window, address, total and live progress — Android parity (`BookingSuccess`
/// renders the full order, not just the code). The fetch is best-effort: on
/// failure the screen still renders the confirmation code, which is in hand.
@MainActor
final class BookingSuccessViewModel: ObservableObject {
    @Published private(set) var order: OrderItem?

    private let orderId: String
    private let fetch: @Sendable (String) async -> OrderItem?
    private var didLoad = false

    init(orderId: String, fetch: @escaping @Sendable (String) async -> OrderItem?) {
        self.orderId = orderId
        self.fetch = fetch
    }

    func load() async {
        guard !didLoad, !orderId.isBlank else { return }
        didLoad = true
        order = await fetch(orderId)
    }
}
