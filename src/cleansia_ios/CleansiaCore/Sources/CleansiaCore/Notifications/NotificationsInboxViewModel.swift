import Combine
import Foundation

/// Backs `NotificationsInboxSheet`: newest-first pages of 20 with additive
/// load-more. Opening marks the fetched rows seen via the watermarked mark-all —
/// rows created after the fetch stay unread; the rows fetched unread keep their
/// indicator for the viewing session. Tapping an unread row fires the idempotent
/// single mark-read with an optimistic row/badge flip, and a row the app
/// resolves to a destination emits it.
@MainActor
public final class NotificationsInboxViewModel<Destination>: ViewModel {
    @Published public private(set) var state: UiState<[UserNotification]> = .loading
    @Published public private(set) var loadingMore = false
    @Published public private(set) var hasMore = false

    /// One-shot on a row tap that resolves a destination: the view dismisses
    /// and navigates. A target-less row only marks read and the sheet stays
    /// open. The VM never navigates itself.
    public let tapped = PassthroughSubject<Destination, Never>()

    private let client: NotificationFeedClient
    private let badge: NotificationBadgeModel
    private let snackbar: SnackbarController
    private let destination: (UserNotification) -> Destination?
    private let pageSize: Int
    private var total = 0
    private var loading = false

    public init(
        client: NotificationFeedClient,
        badge: NotificationBadgeModel,
        snackbar: SnackbarController,
        destination: @escaping (UserNotification) -> Destination?,
        pageSize: Int = 20
    ) {
        self.client = client
        self.badge = badge
        self.snackbar = snackbar
        self.destination = destination
        self.pageSize = pageSize
    }

    public func onOpen() async {
        guard state.loadedValue == nil else { return }
        await loadFirstPage()
    }

    public func retry() async {
        state = .loading
        await loadFirstPage()
    }

    public func loadNextPage() async {
        let current = state.loadedValue ?? []
        guard hasMore, !loadingMore, !loading else { return }
        loadingMore = true
        defer { loadingMore = false }
        switch await client.page(offset: current.count, limit: pageSize) {
        case let .success(page):
            let combined = current + page.items
            total = page.total
            state = .loaded(combined)
            hasMore = combined.count < total
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }

    public func tap(id: String) async {
        guard var items = state.loadedValue,
              let index = items.firstIndex(where: { $0.id == id })
        else { return }
        let item = items[index]
        let wasUnread = item.readOn == nil
        if wasUnread {
            items[index] = item.markedRead(on: Date())
            state = .loaded(items)
            badge.noteMarkedRead()
        }
        if let destination = destination(item) {
            tapped.send(destination)
        }
        // Idempotent server write after the optimistic flip; a failure is
        // silent — the watermarked mark-all or the next open reconciles.
        if wasUnread {
            _ = await client.markRead(id: id)
        }
    }

    private func loadFirstPage() async {
        if loading { return }
        loading = true
        defer { loading = false }
        switch await client.page(offset: 0, limit: pageSize) {
        case let .success(page):
            total = page.total
            state = .loaded(page.items)
            hasMore = page.items.count < total
            await markFetchedSeen(page.items)
        case let .failure(error):
            snackbar.showApiError(error)
            if state.loadedValue == nil {
                state = .error(error)
            }
        }
    }

    /// The watermark is the newest FETCHED row's `createdOn`, so a row created
    /// between the fetch and this call stays unread.
    private func markFetchedSeen(_ items: [UserNotification]) async {
        guard let newest = items.map(\.createdOn).max() else { return }
        if case .success = await client.markAllRead(upToCreatedOn: newest) {
            await badge.refresh()
        }
    }
}
