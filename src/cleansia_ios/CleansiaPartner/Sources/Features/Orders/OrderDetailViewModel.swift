import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// Per-action discriminator so individual lifecycle buttons can show their own
/// spinner (the `OrderAction` parity). Declared now; the lifecycle actions are
/// wired in a later slice.
enum OrderAction: Equatable {
    case take
    case notifyOnTheWay
    case start
    case complete
}

@MainActor
final class OrderDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<OrderDetail> = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var inFlightAction: OrderAction?

    private let orderId: String
    private let client: PartnerOrderClient
    private let snackbar: SnackbarController

    init(orderId: String, client: PartnerOrderClient, snackbar: SnackbarController) {
        self.orderId = orderId
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        await fetch()
    }

    private func fetch() async {
        switch await client.getById(orderId: orderId) {
        case let .success(item):
            state = .loaded(OrderDetail(item))
        case let .failure(error):
            snackbar.showApiError(error)
            // Stay .loaded through a background-refetch failure — only the
            // first load (nothing loaded yet) flips to .error
            // (OrderDetailViewModel.kt:74-79 parity).
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }
}
