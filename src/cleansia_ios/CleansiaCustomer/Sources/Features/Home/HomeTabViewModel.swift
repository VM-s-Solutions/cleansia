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
    @Published private(set) var recentOrders: [CustomerOrderSummary] = []
    @Published private(set) var ordersLoaded = false
    @Published private(set) var ordersLoading = false
    @Published private(set) var recurringTemplates: [RecurringTemplate] = []
    @Published private(set) var recurringLoaded = false
    @Published private(set) var loyaltyAccount: LoyaltyAccount?
    @Published private(set) var loyaltyLoaded = false
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
        recurringRepository.$loaded.assign(to: &$recurringLoaded)
        loyaltyRepository.$account.assign(to: &$loyaltyAccount)
        loyaltyRepository.$loaded.assign(to: &$loyaltyLoaded)
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

    /// No membership term: a lapsed membership does not stop a schedule, and hiding a
    /// running schedule from the customer paying for it hides the way to stop it.
    var showRecurringSection: Bool {
        !activeRecurring.isEmpty
    }

    var mostRecentCompleted: CustomerOrderSummary? {
        HomeSections.mostRecentCompleted(recentOrders)
    }

    var recentForDisplay: [CustomerOrderSummary] {
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

    /// Home entry and every return to the foreground.
    ///
    /// Nothing else on this screen ever re-fetches a cache that already landed:
    /// the shell prefetch runs once per shell entry, and the `.task` warmers
    /// below all guard on "still empty". Loyalty had no warmer here at all, so
    /// points earned after the first fetch only surfaced on the next cold start
    /// (or a Rewards pull-to-refresh) — the reported "only after the next
    /// launch of the app".
    ///
    /// The naive fix is worse than the bug. SwiftUI restarts a `.task` every
    /// time it re-presents the tab root, and `scenePhase` flips to `.active` on
    /// every foreground, so this hook is hot: ungated it would bill three
    /// network calls per tap on the Home tab. Each source is therefore gated on
    /// its own freshness watermark, and a repeated return inside the window
    /// costs nothing.
    ///
    /// Concurrent, so one slow source cannot hold up the others. Silent on
    /// failure — the screen keeps rendering the cached snapshot, so a snackbar
    /// for an ambient background refresh would be noise.
    func refreshStaleSources() async {
        async let loyalty: Void = refreshLoyaltyIfStale()
        async let orders: Void = refreshOrdersIfStale()
        async let membership: Void = refreshMembershipIfStale()
        _ = await (loyalty, orders, membership)
    }

    /// The pull, which deliberately does NOT consult staleness.
    ///
    /// `refreshStaleSources` is ambient — it runs on appear and skips anything inside its freshness
    /// window, which is right for something the customer did not ask for. A pull IS the ask: a customer
    /// who drags the screen down and gets nothing because a watermark says the data is young enough has
    /// been told the gesture does not work. So every source refreshes unconditionally.
    ///
    /// Silent on failure, like its ambient sibling — the cached snapshot stays on screen, and the pull
    /// spinner ending is itself the feedback.
    func pullToRefresh() async {
        async let loyalty: Void = forceRefreshLoyalty()
        async let orders: Void = forceRefreshOrders()
        async let membership: Void = forceRefreshMembership()
        _ = await (loyalty, orders, membership)
    }

    // `refresh()` returns an ApiResult, so these wrap it to Void for the concurrent `async let` —
    // the same shape the staleness-gated siblings below already use. The result is discarded on
    // purpose: a pull that fails leaves the cached snapshot on screen, and the spinner ending is the
    // feedback.
    private func forceRefreshLoyalty() async { _ = await loyaltyRepository.refresh() }

    private func forceRefreshOrders() async { _ = await orderRepository.refresh() }

    private func forceRefreshMembership() async { _ = await membershipRepository.refresh() }

    private func refreshLoyaltyIfStale() async {
        guard loyaltyRepository.staleness.isStale else { return }
        await loyaltyRepository.refresh()
    }

    private func refreshOrdersIfStale() async {
        guard orderRepository.staleness.isStale else { return }
        await orderRepository.refresh()
    }

    private func refreshMembershipIfStale() async {
        guard membershipRepository.staleness.isStale else { return }
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

    /// The `LaunchedEffect(Unit) { recurringRepo.refresh() }` parity
    /// (`HomeTab.kt:160-162`) — errors stay silent, as on Android. Skips when the shell
    /// prefetch already landed the templates — this pass only backfills a failed/raced
    /// prefetch. Not gated on membership: a lapsed member's schedules keep generating,
    /// and an unfetched list is a section that cannot appear.
    func refreshRecurring() async {
        guard !recurringRepository.loaded else { return }
        await recurringRepository.refresh()
    }

    var sectionVisibility: HomeSections.SectionVisibility {
        HomeSections.SectionVisibility(
            orderAgain: mostRecentCompleted != nil,
            recurring: showRecurringSection,
            packages: !popularPackages.isEmpty,
            recent: showRecent,
            milestone: milestoneAccount != nil
        )
    }

    /// The 1.5s fallback ceiling (`HomeTab.kt:207-210`) — a slow/failing source
    /// stops blocking and the page renders whatever arrived.
    func runFirstPaintCeiling() async {
        try? await Task.sleep(nanoseconds: 1_500_000_000)
        guard !Task.isCancelled, !firstPaintReady else { return }
        firstPaintReady = true
    }

    /// Flip once when every Home source has landed (recurring only gates Plus
    /// members); never revert for this tab session (`HomeTab.kt:196-203`).
    private func startFirstPaintWatcher() {
        Publishers.CombineLatest(
            Publishers.CombineLatest3($ordersLoaded, $membership, $packages),
            Publishers.CombineLatest($loyaltyLoaded, $recurringLoaded)
        )
        .map { core, extras in
            let (ordersLoaded, membership, packages) = core
            let (loyaltyLoaded, recurringLoaded) = extras
            return HomeSections.firstPaintReady(HomeSections.FirstPaintSources(
                ordersLoaded: ordersLoaded,
                membershipReady: membership != nil,
                packagesReady: !packages.isEmpty,
                loyaltyLoaded: loyaltyLoaded,
                isPlus: membership?.hasMembership == true,
                recurringLoaded: recurringLoaded
            ))
        }
        .filter { $0 }
        .prefix(1)
        .sink { [weak self] ready in self?.firstPaintReady = ready }
        .store(in: &cancellables)
    }
}
