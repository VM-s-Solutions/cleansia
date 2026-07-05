import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

/// Injection-seam VM for `HomeTab` (the `HomeTabViewModel.kt` parity). The home
/// screen observes the customer singleton repositories; this VM mirrors their
/// streams into `@Published` state and hosts the first-paint skeleton gate
/// (`HomeTab.kt:196-215`). The catalog source is the shell's session-lived
/// `BookingViewModel` — the iOS stand-in for Android's shared
/// `CatalogRepository` singleton, so Home and the booking sheet read one cache.
@MainActor
final class HomeTabViewModel: ViewModel {
    @Published private(set) var recentOrders: [OrderListItem] = []
    @Published private(set) var ordersLoaded = false
    @Published private(set) var ordersLoading = false
    @Published private(set) var recurringTemplates: [RecurringTemplate] = []
    @Published private(set) var loyaltyAccount: LoyaltyAccount?
    @Published private(set) var membership: MyMembership?
    @Published private(set) var addresses: [SavedAddress] = []
    @Published private(set) var selectedAddressId: String?
    @Published private(set) var packages: [CatalogPackage] = []
    @Published private(set) var firstPaintReady = false

    private let orderRepository: OrderRepository
    private let recurringRepository: RecurringBookingRepository
    private let loyaltyRepository: LoyaltyRepository
    private let membershipRepository: MembershipRepository
    private let savedAddressRepository: SavedAddressRepository
    private let catalogSource: BookingViewModel
    private let snackbar: SnackbarController
    private var cancellables: Set<AnyCancellable> = []

    init(
        orderRepository: OrderRepository,
        recurringRepository: RecurringBookingRepository,
        loyaltyRepository: LoyaltyRepository,
        membershipRepository: MembershipRepository,
        savedAddressRepository: SavedAddressRepository,
        catalogSource: BookingViewModel,
        snackbar: SnackbarController
    ) {
        self.orderRepository = orderRepository
        self.recurringRepository = recurringRepository
        self.loyaltyRepository = loyaltyRepository
        self.membershipRepository = membershipRepository
        self.savedAddressRepository = savedAddressRepository
        self.catalogSource = catalogSource
        self.snackbar = snackbar
        super.init()
        orderRepository.$orders.assign(to: &$recentOrders)
        orderRepository.$loaded.assign(to: &$ordersLoaded)
        orderRepository.$loading.assign(to: &$ordersLoading)
        recurringRepository.$templates.assign(to: &$recurringTemplates)
        loyaltyRepository.$account.assign(to: &$loyaltyAccount)
        membershipRepository.$current.assign(to: &$membership)
        savedAddressRepository.$addresses.assign(to: &$addresses)
        savedAddressRepository.$selectedId.assign(to: &$selectedAddressId)
        catalogSource.$catalogState
            .map { $0.loadedValue?.packages ?? [] }
            .assign(to: &$packages)
        startFirstPaintWatcher()
    }

    var isPlus: Bool {
        membership?.hasMembership == true
    }

    var hasAnyOrders: Bool {
        !recentOrders.isEmpty
    }

    var displayedAddress: SavedAddress? {
        HomeSections.displayedAddress(addresses, selectedId: selectedAddressId)
    }

    var popularPackages: [CatalogPackage] {
        HomeSections.popularPackages(packages)
    }

    var activeRecurring: [RecurringTemplate] {
        HomeSections.activeRecurring(recurringTemplates)
    }

    var showRecurringSection: Bool {
        isPlus && !activeRecurring.isEmpty
    }

    var mostRecentCompleted: OrderListItem? {
        HomeSections.mostRecentCompleted(recentOrders)
    }

    var recentForDisplay: [OrderListItem] {
        HomeSections.recentForDisplay(recentOrders)
    }

    var showRecent: Bool {
        HomeSections.showRecent(recent: recentForDisplay, ordersLoaded: ordersLoaded, ordersLoading: ordersLoading)
    }

    var milestoneAccount: LoyaltyAccount? {
        HomeSections.showMilestone(loyaltyAccount) ? loyaltyAccount : nil
    }

    var showSetupRecurringSlide: Bool {
        Self.showSetupRecurringSlide(
            isPlus: isPlus,
            hasRecurringSource: true,
            templatesEmpty: recurringTemplates.isEmpty
        )
    }

    /// The `showSetupRecurringSlide` predicate (`HomeTab.kt:167`). The
    /// wired-source clause is vestigial now that the recurring repository is a
    /// required init param, kept so the showSetupRecurringSlide guard tests
    /// stay meaningful.
    static func showSetupRecurringSlide(isPlus: Bool, hasRecurringSource: Bool, templatesEmpty: Bool) -> Bool {
        isPlus && hasRecurringSource && templatesEmpty
    }

    /// `LaunchedEffect(Unit) { if (membership == null) membershipRepo.refresh() }`
    /// (`HomeTab.kt:134-136`) — silent, as on Android.
    func refreshMembershipIfNeeded() async {
        guard membership == nil else { return }
        await membershipRepository.refresh()
    }

    /// `LaunchedEffect(Unit) { if (packages.isEmpty) refreshCatalog() }`
    /// (`HomeTab.kt:144-146` + `HomeTabViewModel.kt:39-45`) — surfaces the
    /// snackbar on failure via the codebase-wide `showApiError` convention.
    func refreshCatalogIfNeeded() async {
        guard packages.isEmpty else { return }
        await catalogSource.loadCatalog()
        if case let .error(error) = catalogSource.catalogState {
            snackbar.showApiError(error)
        }
    }

    /// The `LaunchedEffect(isPlus) { if (isPlus) recurringRepo.refresh() }`
    /// parity (`HomeTab.kt:160-162`) — errors stay silent, as on Android.
    func refreshRecurringIfPlus(_ isPlus: Bool) async {
        guard isPlus else { return }
        await recurringRepository.refresh()
    }

    /// The 1.5s hard ceiling (`HomeTab.kt:207-210`) — a slow/failing source
    /// stops blocking and the page renders whatever arrived.
    func runFirstPaintCeiling() async {
        try? await Task.sleep(nanoseconds: 1_500_000_000)
        guard !Task.isCancelled else { return }
        firstPaintReady = true
    }

    /// Flip once when orders + membership + packages have all landed; never
    /// revert for this tab session (`HomeTab.kt:196-203`).
    private func startFirstPaintWatcher() {
        Publishers.CombineLatest3($ordersLoaded, $membership, $packages)
            .map { ordersLoaded, membership, packages in
                HomeSections.firstPaintReady(
                    ordersLoaded: ordersLoaded,
                    membershipReady: membership != nil,
                    packagesReady: !packages.isEmpty
                )
            }
            .filter { $0 }
            .prefix(1)
            .sink { [weak self] ready in self?.firstPaintReady = ready }
            .store(in: &cancellables)
    }
}
