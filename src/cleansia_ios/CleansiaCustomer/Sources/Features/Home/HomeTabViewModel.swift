import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

/// Injection-seam VM for `HomeTab` (the `HomeTabViewModel.kt` parity). The home
/// screen observes the customer singleton repositories; this VM mirrors their
/// streams into `@Published` state. Loyalty, membership, catalog and address
/// land in later slices, so those seams stay additive.
@MainActor
final class HomeTabViewModel: ViewModel {
    @Published private(set) var recentOrders: [OrderListItem] = []
    @Published private(set) var recurringTemplates: [RecurringTemplate] = []

    private let orderRepository: OrderRepository
    private let recurringRepository: RecurringBookingRepository?
    private let snackbar: SnackbarController

    init(
        orderRepository: OrderRepository,
        recurringRepository: RecurringBookingRepository? = nil,
        snackbar: SnackbarController
    ) {
        self.orderRepository = orderRepository
        self.recurringRepository = recurringRepository
        self.snackbar = snackbar
        super.init()
        orderRepository.$orders.assign(to: &$recentOrders)
        recurringRepository?.$templates.assign(to: &$recurringTemplates)
    }

    var hasAnyOrders: Bool {
        !recentOrders.isEmpty
    }

    var ordersLoaded: Bool {
        orderRepository.loaded
    }

    func showSetupRecurringSlide(isPlus: Bool) -> Bool {
        Self.showSetupRecurringSlide(
            isPlus: isPlus,
            hasRecurringSource: recurringRepository != nil,
            templatesEmpty: recurringTemplates.isEmpty
        )
    }

    /// The `showSetupRecurringSlide` predicate (`HomeTab.kt:167`), plus the
    /// wired-source guard: until the shell passes a `RecurringBookingRepository`
    /// the templates stream is a permanent `[]`, which must read as "unknown",
    /// not "none".
    static func showSetupRecurringSlide(isPlus: Bool, hasRecurringSource: Bool, templatesEmpty: Bool) -> Bool {
        isPlus && hasRecurringSource && templatesEmpty
    }

    /// The `LaunchedEffect(isPlus) { if (isPlus) recurringRepo.refresh() }`
    /// parity (`HomeTab.kt:160-162`) — errors stay silent, as on Android.
    func refreshRecurringIfPlus(_ isPlus: Bool) async {
        guard isPlus, let recurringRepository else { return }
        await recurringRepository.refresh()
    }

    /// Warm the catalog for the popular-packages strip (`refreshCatalog`). The
    /// `CatalogRepository` singleton lands in a later slice; the seam is here
    /// so Home's first-composition warm-up is wired now.
    func refreshCatalog() async {
        // No-op until the CatalogRepository singleton ships (later slice).
    }
}
