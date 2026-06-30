import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

enum OrdersFilter: CaseIterable {
    case all
    case upcoming
    case completed
    case cancelled

    var label: String {
        switch self {
        case .all: L10n.Orders.filterAll
        case .upcoming: L10n.Orders.filterUpcoming
        case .completed: L10n.Orders.filterCompleted
        case .cancelled: L10n.Orders.filterCancelled
        }
    }

    func matches(_ status: OrderStatus?) -> Bool {
        switch self {
        case .all: true
        case .upcoming: OrderStatusGroup.isUpcoming(status)
        case .completed: OrderStatusGroup.isCompleted(status)
        case .cancelled: OrderStatusGroup.isCancelled(status)
        }
    }
}

@MainActor
final class OrdersListViewModel: ViewModel {
    @Published private(set) var state: UiState<[OrderListItem]> = .loading
    @Published private(set) var refreshPhase: RefreshPhase = .idle
    @Published private(set) var loadingMore = false
    @Published private(set) var hasMore = false
    @Published var activeFilter: OrdersFilter = .all

    private let repository: OrderRepository
    private let snackbar: SnackbarController
    private var cancellables: Set<AnyCancellable> = []

    init(repository: OrderRepository, snackbar: SnackbarController) {
        self.repository = repository
        self.snackbar = snackbar
        super.init()
        bind()
    }

    var filteredOrders: [OrderListItem] {
        guard let orders = state.loadedValue else { return [] }
        return orders.filter { activeFilter.matches($0.status) }
    }

    func filterCount(_ filter: OrdersFilter) -> Int {
        (state.loadedValue ?? []).filter { filter.matches($0.status) }.count
    }

    /// Background refresh on appear (gated on `loading`) — the MainShell prefetch
    /// only runs once, so a booking created since then would leave the cache
    /// stale (`OrdersTab.kt:141-147`).
    func onAppear() async {
        guard !repository.loading else { return }
        await runRefresh(.backgroundRefreshing)
    }

    func pullToRefresh() async {
        await runRefresh(.userRefreshing)
    }

    func retry() async {
        state = .loading
        await runRefresh(.backgroundRefreshing)
    }

    func loadNextPage() async {
        guard hasMore, !loadingMore else { return }
        loadingMore = true
        defer { loadingMore = false }
        _ = await repository.loadNextPage()
    }

    private func runRefresh(_ phase: RefreshPhase) async {
        refreshPhase = phase
        defer { refreshPhase = .idle }
        if case let .failure(error) = await repository.refresh() {
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    private func bind() {
        repository.$orders
            .combineLatest(repository.$loaded)
            .sink { [weak self] orders, loaded in
                guard let self else { return }
                if loaded {
                    state = .loaded(orders)
                }
            }
            .store(in: &cancellables)

        repository.$orders
            .combineLatest(repository.$totalRecords)
            .map { orders, total in orders.count < total }
            .assign(to: &$hasMore)

        if repository.loaded {
            state = .loaded(repository.orders)
        }
    }
}
