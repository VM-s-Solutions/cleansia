import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

/// Injection-seam VM for `HomeTab` (the `HomeTabViewModel.kt` parity). The home
/// screen observes the customer singleton repositories; this VM holds no state
/// of its own. Only the orders singleton exists in this slice — loyalty,
/// membership, catalog, address and recurring land in later slices, so the home
/// surface observes orders now and the rest are filled additively.
@MainActor
final class HomeTabViewModel: ViewModel {
    let orderRepository: OrderRepository

    private let snackbar: SnackbarController

    init(orderRepository: OrderRepository, snackbar: SnackbarController) {
        self.orderRepository = orderRepository
        self.snackbar = snackbar
    }

    var recentOrders: [OrderListItem] {
        orderRepository.orders
    }

    var ordersLoaded: Bool {
        orderRepository.loaded
    }

    /// Warm the catalog for the popular-packages strip (`refreshCatalog`). The
    /// `CatalogRepository` singleton lands in a later slice; the seam is here
    /// so Home's first-composition warm-up is wired now.
    func refreshCatalog() async {
        // No-op until the CatalogRepository singleton ships (later slice).
    }
}
