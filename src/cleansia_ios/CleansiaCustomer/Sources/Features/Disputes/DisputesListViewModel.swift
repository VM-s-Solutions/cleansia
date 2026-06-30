import CleansiaCore
import Combine
import Foundation

@MainActor
final class DisputesListViewModel: ViewModel {
    @Published private(set) var state: UiState<[DisputeListEntry]> = .loading
    @Published private(set) var refreshPhase: RefreshPhase = .idle
    @Published private(set) var loadingMore = false
    @Published private(set) var hasMore = false

    private let repository: DisputeRepository
    private let snackbar: SnackbarController
    private var cancellables: Set<AnyCancellable> = []

    init(repository: DisputeRepository, snackbar: SnackbarController) {
        self.repository = repository
        self.snackbar = snackbar
        super.init()
        bind()
    }

    var entries: [DisputeListEntry] {
        state.loadedValue ?? []
    }

    func onAppear() async {
        guard !repository.loading else { return }
        if repository.loaded {
            await runRefresh(.backgroundRefreshing)
        } else {
            await runRefresh(.backgroundRefreshing)
        }
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
        repository.$disputes
            .combineLatest(repository.$loaded)
            .sink { [weak self] disputes, loaded in
                guard let self, loaded else { return }
                state = .loaded(disputes)
            }
            .store(in: &cancellables)

        repository.$disputes
            .combineLatest(repository.$totalRecords)
            .map { disputes, total in disputes.count < total }
            .assign(to: &$hasMore)

        if repository.loaded {
            state = .loaded(repository.disputes)
        }
    }
}
