import CleansiaCore
import Combine
import Foundation

/// Backs `RewardsActivityScreen` — the full points ledger. Activity isn't cached
/// by `LoyaltyRepository` (an infrequent drilldown), so this VM owns the paged
/// list locally for the screen's lifetime. The Slice-A paged-list archetype:
/// sealed `UiState` + `RefreshPhase`, `refresh()` replaces page 0, `loadNextPage()`
/// appends additively (the `RewardsActivityViewModel.kt` parity).
@MainActor
final class RewardsActivityViewModel: ViewModel {
    @Published private(set) var state: UiState<[LoyaltyActivityItem]> = .loading
    @Published private(set) var refreshPhase: RefreshPhase = .idle
    @Published private(set) var loadingMore = false
    @Published private(set) var hasMore = false

    private let loyaltyRepository: LoyaltyRepository
    private let snackbar: SnackbarController
    private let pageSize: Int
    private var total = 0
    private var loading = false

    init(loyaltyRepository: LoyaltyRepository, snackbar: SnackbarController, pageSize: Int = 20) {
        self.loyaltyRepository = loyaltyRepository
        self.snackbar = snackbar
        self.pageSize = pageSize
    }

    func onAppear() async {
        guard state.loadedValue == nil else { return }
        await refresh()
    }

    func pullToRefresh() async {
        await runRefresh(.userRefreshing)
    }

    func retry() async {
        state = .loading
        await runRefresh(.backgroundRefreshing)
    }

    func refresh() async {
        await runRefresh(.backgroundRefreshing)
    }

    func loadNextPage() async {
        let current = state.loadedValue ?? []
        guard hasMore, !loadingMore, !loading else { return }
        loadingMore = true
        defer { loadingMore = false }
        switch await loyaltyRepository.loadActivity(offset: current.count, limit: pageSize) {
        case let .success(page):
            let combined = current + page.items
            total = page.total
            state = .loaded(combined)
            hasMore = combined.count < total
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    private func runRefresh(_ phase: RefreshPhase) async {
        if loading { return }
        loading = true
        refreshPhase = phase
        defer {
            loading = false
            refreshPhase = .idle
        }
        switch await loyaltyRepository.loadActivity(offset: 0, limit: pageSize) {
        case let .success(page):
            total = page.total
            state = .loaded(page.items)
            hasMore = page.items.count < total
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }
}
