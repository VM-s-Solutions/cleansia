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
    /// Filled by the location source later; nil for now (distance hides).
    @Published var currentLocation: Coordinate?
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

    /// The rows for the current tab after the client-side search filter.
    var visibleOrders: [OrderListItem] {
        (currentState.loadedValue ?? []).filter { $0.matchesSearch(searchQuery) }
    }

    /// The single in-progress order, if any — drives the sticky banner.
    var inProgressOrder: OrderListItem? {
        (currentState.loadedValue ?? []).first { $0.isInProgress }
    }

    func onAppear() async {
        await ensureFreshOrCached(tab.pane, background: true)
    }

    func selectTab(_ newTab: OrdersTab) async {
        guard newTab != tab else { return }
        tab = newTab
        await ensureFreshOrCached(newTab.pane, background: true)
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
    /// the Active row's is always true (assigned-to-me). `hasAfterPhotos` isn't
    /// on the list DTO, so an InProgress Active row resolves to `.complete` and
    /// the server's after-photos guard is the safety net (the inline parity).
    func inlineAction(for order: OrderListItem) -> OrderPrimaryAction {
        let isMine = tab == .active
        return OrderPrimaryAction.action(for: order.status, isMine: isMine, hasAfterPhotos: true)
    }

    /// Run a row's inline lifecycle action. O2: acts ONLY on `order.id` — the id
    /// the list response carried for that row; never a synthesized/echoed id.
    func runInlineAction(_ action: OrderPrimaryAction, on order: OrderListItem) async {
        guard inFlightActionOrderId == nil, let orderId = order.id else { return }
        guard let mutation = action.orderAction?.mutation else { return }

        inFlightActionOrderId = orderId
        let result = await command(for: action, orderId: orderId)
        inFlightActionOrderId = nil

        switch result {
        case .success:
            staleness.invalidatePanes(for: mutation)
            staleness.invalidateOrder(orderId)
            await refreshAffectedPanes(mutation)
        case let .failure(error):
            // O4: clean reject (e.g. already-taken) — surface + refresh the
            // current pane so the stale "takeable" row corrects.
            snackbar.showApiError(error)
            staleness.invalidatePanes(for: mutation)
            await fetch(tab.pane, phase: .backgroundRefreshing)
        }
    }

    private func command(for action: OrderPrimaryAction, orderId: String) async -> ApiResult<Void> {
        switch action {
        case .take: await client.takeOrder(orderId: orderId)
        case .notifyOnTheWay: await client.notifyOnTheWay(orderId: orderId)
        case .start: await client.startOrder(orderId: orderId)
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

    /// Skip the network when the pane's cache is warm (no-flash resume); else a
    /// silent background fetch (the `ensureFreshOrCachedAsync` parity). User
    /// pulls go through `userRefresh`, never here.
    private func ensureFreshOrCached(_ pane: OrdersPane, background: Bool) async {
        guard staleness.isPaneStale(pane) else { return }
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
