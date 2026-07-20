import CleansiaCustomerApi
import Foundation

/// The `BookingSuccessUiState` (Android) parity: the screen always renders the
/// celebratory header + code pill; this state only drives the enrichment
/// (summary card + timeline detail). `.error` degrades silently — the success
/// moment is never blocked on a failed fetch.
enum BookingSuccessUiState {
    case loading
    case loaded(OrderItem)
    case error
}

@MainActor
final class BookingSuccessViewModel: ObservableObject {
    @Published private(set) var state: BookingSuccessUiState = .loading

    private let orderId: String
    private let fetch: @Sendable (String) async -> OrderItem?
    private let warmOrders: @Sendable () async -> Void
    private var didLoad = false

    init(
        orderId: String,
        fetch: @escaping @Sendable (String) async -> OrderItem?,
        warmOrders: @escaping @Sendable () async -> Void = {}
    ) {
        self.orderId = orderId
        self.fetch = fetch
        self.warmOrders = warmOrders
    }

    var order: OrderItem? {
        if case let .loaded(order) = state { return order }
        return nil
    }

    /// The freshly-loaded code wins over the nav-arg one (the backend may trim
    /// whitespace); the fallback keeps the pill rendering in loading/error.
    func effectiveCode(fallback: String) -> String {
        guard let code = order?.confirmationCode, !code.isBlank else { return fallback }
        return code
    }

    func load() async {
        guard !didLoad else { return }
        didLoad = true
        async let warm: Void = warmOrders()
        if orderId.isBlank {
            state = .error
        } else if let order = await fetch(orderId) {
            state = .loaded(order)
        } else {
            state = .error
        }
        await warm
    }
}
