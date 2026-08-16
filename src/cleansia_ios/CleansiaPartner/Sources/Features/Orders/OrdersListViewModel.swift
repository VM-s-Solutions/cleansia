import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class OrdersListViewModel: ViewModel {
    @Published private(set) var tab: OrdersTab = .available
    @Published private(set) var paneState: [OrdersPane: UiState<[OrderListItem]>] = [
        .available: .loading,
        .active: .loading,
        .history: .loading
    ]
    @Published private(set) var refreshPhase: RefreshPhase = .idle
    @Published var searchQuery: String = ""
    @Published private(set) var availableSort: AvailableSort = .earningsHighToLow
    @Published private(set) var completedPeriod: CompletedPeriod = .thisMonth
    /// The cleaner's last known fix, used only to annotate Available rows with a
    /// distance (`OrderListItem.distanceKm(from:)`). Stays nil unless location is
    /// authorized, and every consumer treats nil as "hide the distance".
    @Published var currentLocation: Coordinate?
    /// The last settled authorization answer. Re-read from the provider on every
    /// Available entry so a grant made in the Settings app is picked up without
    /// a relaunch.
    @Published private(set) var locationStatus: LocationAuthorizationStatus = .notDetermined
    /// The order whose inline action is currently in flight — drives the per-row
    /// spinner so the cleaner can't double-fire. Nil when no action is running.
    @Published private(set) var inFlightActionOrderId: String?

    let navigateToDetail = PassthroughSubject<String, Never>()

    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let snackbar: SnackbarController
    private var ownEmployeeId: String?
    private var inFlight: Set<OrdersPane> = []

    init(client: PartnerOrderClient, staleness: OrdersStaleness, snackbar: SnackbarController) {
        self.client = client
        self.staleness = staleness
        self.snackbar = snackbar
    }

    var currentState: UiState<[OrderListItem]> {
        paneState[tab.pane] ?? .loading
    }

    /// The rows for the current tab, search-filtered on Available only.
    ///
    /// `OrdersSearchField` renders inside `AvailablePane` and nowhere else, so
    /// applying the query to Active/History hid rows behind a control the
    /// cleaner couldn't see — and `ActivePane`'s empty branch would then claim
    /// `noActiveOrders` on a day they had jobs. The query itself deliberately
    /// survives a tab switch (Android's `selectTab` leaves it alone too); it is
    /// the *filter* that is scoped, not the field.
    var visibleOrders: [OrderListItem] {
        let rows = currentState.loadedValue ?? []
        guard tab == .available else { return rows }
        return rows.filter { $0.matchesSearch(searchQuery) }
    }

    /// The single in-progress order, if any — drives the sticky banner.
    var inProgressOrder: OrderListItem? {
        (currentState.loadedValue ?? []).first { $0.isInProgress }
    }

    /// The inline priming row, shown only while iOS can still be asked. A
    /// denied/restricted status hides it permanently — iOS never re-prompts, and
    /// a row whose button does nothing is worse than no row at all.
    var showsLocationPrompt: Bool {
        tab == .available && locationStatus == .notDetermined
    }

    func onAppear() async {
        await ensureFreshOrCached(tab.pane, background: true)
    }

    func selectTab(_ newTab: OrdersTab) async {
        guard newTab != tab else { return }
        tab = newTab
        await ensureFreshOrCached(newTab.pane, background: true)
    }

    /// Silent best-effort refresh, run on every Available entry
    /// (`OrdersListScreen.kt:114-126` parity for the *already granted* branch).
    ///
    /// Deliberate divergence from Android on the not-granted branch: Android
    /// re-launches the system dialog every time the cleaner enters the tab
    /// because Android may re-ask. iOS gets exactly one prompt per install and a
    /// "Don't Allow" is permanent short of a trip to Settings — and Orders is the
    /// tab that appears immediately after sign-in, so an Android-parity
    /// auto-prompt would fire a cold, context-free dialog seconds after login and
    /// kill the feature for everyone who taps through it. This NEVER prompts; the
    /// only path that may is `requestLocationPermission`, behind an explicit tap.
    ///
    /// Re-reading the status here (not just once at init) is what makes a grant
    /// made in the Settings app take effect, and re-resolving the fix on each
    /// entry retries a cold null first fix — the reason Android re-resolves too.
    func refreshLocationIfAuthorized(_ location: LocationProvider) async {
        locationStatus = location.authorizationStatus
        guard locationStatus == .authorized else { return }
        if let fix = await location.currentLocation() { currentLocation = fix }
    }

    /// The explicit tap on the priming row — the ONLY path that may prompt.
    func requestLocationPermission(_ location: LocationProvider) async {
        locationStatus = await location.requestWhenInUseAuthorization()
        guard locationStatus == .authorized else { return }
        if let fix = await location.currentLocation() { currentLocation = fix }
    }

    func userRefresh() async {
        await fetch(tab.pane, phase: .userRefreshing)
    }

    func setSearchQuery(_ query: String) {
        searchQuery = query
    }

    func setAvailableSort(_ sort: AvailableSort) async {
        guard sort != availableSort else { return }
        availableSort = sort
        if tab == .available {
            await fetch(.available, phase: .backgroundRefreshing)
        }
    }

    func setCompletedPeriod(_ period: CompletedPeriod) async {
        guard period != completedPeriod else { return }
        completedPeriod = period
        if tab == .history {
            await fetch(.history, phase: .backgroundRefreshing)
        }
    }

    func openDetail(_ orderId: String) {
        navigateToDetail.send(orderId)
    }

    /// The inline primary action for a list row, via the shared machine. The
    /// Available card's `isMine` is always false (it lists unassigned offers);
    /// the Active row's is always true (assigned-to-me). Neither `hasAfterPhotos`
    /// nor the payment fields are on the list DTO, so an InProgress Active row
    /// resolves to `.complete` and the server's after-photos + payment guards are
    /// the safety net (the inline parity). Cash collection lives on the detail.
    func inlineAction(for order: OrderListItem) -> OrderPrimaryAction {
        let isMine = tab == .active
        return OrderPrimaryAction.action(
            for: order.status,
            isMine: isMine,
            hasAfterPhotos: true,
            isCashPayment: false,
            isPaymentSettled: true
        )
    }

    /// Run a row's inline lifecycle action. O2: acts ONLY on `order.id` — the id
    /// the list response carried for that row; never a synthesized/echoed id.
    func runInlineAction(_ action: OrderPrimaryAction, on order: OrderListItem) async {
        guard inFlightActionOrderId == nil, let orderId = order.id else { return }
        guard let orderAction = action.orderAction else { return }

        inFlightActionOrderId = orderId
        let result = await command(for: action, orderId: orderId)

        switch result {
        case .success:
            if let confirmation = orderAction.successFeedback {
                snackbar.showSuccess(confirmation)
            }
            staleness.invalidatePanes(for: orderAction.mutation)
            staleness.invalidateOrder(orderId)
            // Held through the refresh: the row's slider stays locked-busy
            // until it re-renders with the NEW action, so a second swipe in
            // the gap can't re-fire the already-applied transition.
            await refreshAffectedPanes(orderAction.mutation)
            inFlightActionOrderId = nil
        case let .failure(error):
            // O4: clean reject (e.g. already-taken) — spring the slider back,
            // surface, and refresh the current pane so the stale row corrects.
            inFlightActionOrderId = nil
            snackbar.showApiError(error)
            staleness.invalidatePanes(for: orderAction.mutation)
            await fetch(tab.pane, phase: .backgroundRefreshing)
        }
    }

    private func command(for action: OrderPrimaryAction, orderId: String) async -> ApiResult<Void> {
        switch action {
        case .take: await client.takeOrder(orderId: orderId)
        case .notifyOnTheWay: await client.notifyOnTheWay(orderId: orderId)
        case .start: await client.startOrder(orderId: orderId)
        case .collectCash: await client.markCashCollected(orderId: orderId)
        case .complete: await client.completeOrder(orderId: orderId, actualMinutes: nil, notes: nil)
        case .completeBlocked, .none: .failure(ApiError(code: "orders.no_action"))
        }
    }

    private func refreshAffectedPanes(_ mutation: OrdersMutation) async {
        // Refresh the affected pane the cleaner is currently looking at first
        // (silent — the row spinner already gave feedback); the others refill
        // lazily on their next `ensureFreshOrCached`.
        if mutation.affectedPanes.contains(tab.pane) {
            await fetch(tab.pane, phase: .backgroundRefreshing)
        }
    }

    /// Skip the network when this view model is already showing the pane's rows AND its watermark is
    /// warm (no-flash resume); else a silent background fetch. User pulls go through `userRefresh`,
    /// never here.
    ///
    /// **A warm watermark alone is not enough to skip.** `OrdersStaleness` is built once in
    /// `PartnerAppContainer` and lives as long as the app, while `paneState` is rebuilt with every
    /// `OrdersListView` and starts every pane at `.loading`. So re-entering the tab inside the freshness
    /// window guarded out of the fetch and left all three panes spinning, with pull-to-refresh the only
    /// way out. `OrdersListViewModel.kt` carries the same rule for the same reason.
    private func ensureFreshOrCached(_ pane: OrdersPane, background: Bool) async {
        let holdsRows = paneState[pane]?.loadedValue != nil
        guard !holdsRows || staleness.isPaneStale(pane) else { return }
        await fetch(pane, phase: background ? .backgroundRefreshing : .userRefreshing)
    }

    private func fetch(_ pane: OrdersPane, phase: RefreshPhase) async {
        guard !inFlight.contains(pane) else { return }
        inFlight.insert(pane)
        defer { inFlight.remove(pane) }

        // A user pull shows the chunky indicator; a background fetch is silent.
        // The pane's own UiState stays whatever it was (loaded rows persist
        // through a background re-fetch — no spinner flash).
        if phase == .userRefreshing {
            refreshPhase = .userRefreshing
        } else if refreshPhase == .idle {
            refreshPhase = .backgroundRefreshing
        }

        let resolvedEmployeeId = await resolveOwnEmployeeIdIfNeeded(for: pane)
        let query = OrdersQueryBuilder.query(
            tab: tabFor(pane),
            ownEmployeeId: resolvedEmployeeId,
            sort: availableSort,
            period: completedPeriod
        )

        switch await client.getPaged(query) {
        case let .success(orders):
            paneState[pane] = .loaded(orders)
            staleness.markPaneFresh(pane)
        case let .failure(error):
            snackbar.showApiError(error)
            if paneState[pane]?.loadedValue == nil {
                paneState[pane] = .error(error)
            }
        }
        refreshPhase = .idle
    }

    /// O3: resolve the caller's OWN employeeId (JWT-truth surrogate) for the
    /// "mine" panes only — never a foreign id. Available passes nil.
    private func resolveOwnEmployeeIdIfNeeded(for pane: OrdersPane) async -> String? {
        guard pane == .active || pane == .history else { return nil }
        if let ownEmployeeId { return ownEmployeeId }
        if case let .success(id) = await client.currentEmployeeId() {
            ownEmployeeId = id
            return id
        }
        return nil
    }

    private func tabFor(_ pane: OrdersPane) -> OrdersTab {
        switch pane {
        case .available: .available
        case .active: .active
        case .history: .history
        }
    }
}
