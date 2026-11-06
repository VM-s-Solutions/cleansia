import CleansiaCustomerApi
import Foundation

/// The `BookingSuccessUiState` (Android) parity: the ticket renders from the submitted draft at once; this
/// state only brings the order's own figures. `.error` keeps the ticket and offers Try again — the success
/// moment is never blocked on a failed fetch.
enum BookingSuccessUiState {
    case loading
    case loaded(CustomerOrderDetail)
    case error
}

@MainActor
final class BookingSuccessViewModel: ObservableObject {
    @Published private(set) var state: BookingSuccessUiState = .loading

    private let orderId: String
    private let fetch: @Sendable (String) async -> CustomerOrderDetail?
    private let warmOrders: @Sendable () async -> Void
    private var didLoad = false

    init(
        orderId: String,
        fetch: @escaping @Sendable (String) async -> CustomerOrderDetail?,
        warmOrders: @escaping @Sendable () async -> Void = {}
    ) {
        self.orderId = orderId
        self.fetch = fetch
        self.warmOrders = warmOrders
    }

    var order: CustomerOrderDetail? {
        if case let .loaded(order) = state { return order }
        return nil
    }

    func load() async {
        guard !didLoad else { return }
        didLoad = true
        async let warm: Void = warmOrders()
        await read()
        await warm
    }

    func retry() async {
        if case .loading = state { return }
        state = .loading
        await read()
    }

    private func read() async {
        if orderId.isBlank {
            state = .error
        } else if let order = await fetch(orderId) {
            state = .loaded(order)
        } else {
            state = .error
        }
    }
}
