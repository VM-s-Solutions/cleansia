import CleansiaCore
import Combine
import Foundation

@MainActor
final class NotificationPreferencesViewModel: ViewModel {
    @Published private(set) var state: UiState<NotificationPreferences> = .loading

    private let client: NotificationPreferencesClient
    private let pendingWrites = PassthroughSubject<NotificationPreferences, Never>()
    private var cancellables: Set<AnyCancellable> = []
    private var lastConfirmed: NotificationPreferences?

    init(
        client: NotificationPreferencesClient,
        scheduler: AnySchedulerOf<DispatchQueue> = .main
    ) {
        self.client = client
        super.init()
        pendingWrites
            .debounce(for: .milliseconds(300), scheduler: scheduler)
            .sink { [weak self] payload in
                Task { await self?.commit(payload) }
            }
            .store(in: &cancellables)
    }

    func load() async {
        state = .loading
        switch await client.getMine() {
        case let .success(preferences):
            lastConfirmed = preferences
            state = .loaded(preferences)
        case let .failure(error):
            state = .error(error)
        }
    }

    func setCategory(_ category: NotificationCategory, enabled: Bool) {
        guard case let .loaded(current) = state else { return }
        let updated = current.with(category, enabled: enabled)
        state = .loaded(updated)
        pendingWrites.send(updated)
    }

    private func commit(_ payload: NotificationPreferences) async {
        let snapshot = lastConfirmed
        switch await client.update(payload) {
        case let .success(confirmed):
            lastConfirmed = confirmed
            state = .loaded(confirmed)
        case .failure:
            if let snapshot {
                lastConfirmed = snapshot
                state = .loaded(snapshot)
            }
        }
    }
}
