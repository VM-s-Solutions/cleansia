import CleansiaCore
import CleansiaCustomerApi
import Combine
import Foundation

/// Singleton cache + paginator for the signed-in user's orders (the
/// `OrderRepository.kt` parity). The Home + Orders surfaces observe `orders`;
/// `refresh()` replaces page 0, `loadNextPage()` appends additively. Registered
/// in the `SessionScopedCacheRegistry` so sign-out / forced-401 wipes it.
@MainActor
final class OrderRepository: SessionScopedCache {
    @Published private(set) var orders: [CustomerOrderSummary] = []
    @Published private(set) var totalRecords = 0
    @Published private(set) var loaded = false
    @Published private(set) var loading = false
    @Published private(set) var loadingMore = false

    /// Freshness watermark for page 0. Only the Home resume hook consults it;
    /// the Orders tab and pull-to-refresh always go to the network.
    let staleness: Staleness

    private let client: OrderClient
    private let pageSize: Int

    init(client: OrderClient, pageSize: Int = 20, staleness: Staleness = Staleness()) {
        self.client = client
        self.pageSize = pageSize
        self.staleness = staleness
    }

    var hasMore: Bool {
        orders.count < totalRecords
    }

    /// Fetch page 0 and replace the cache. Pull-to-refresh and initial loads.
    @discardableResult
    func refresh() async -> ApiResult<Void> {
        if loading { return .success(()) }
        loading = true
        defer { loading = false }
        switch await client.getMyOrders(offset: 0, limit: pageSize) {
        case let .success(page):
            orders = page.items
            totalRecords = page.total
            loaded = true
            staleness.markFresh()
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    /// Append the next page if more remain. Silent on failure — the caller
    /// ignores the error and scrolling again retries.
    @discardableResult
    func loadNextPage() async -> ApiResult<Void> {
        if loadingMore { return .success(()) }
        guard hasMore else { return .success(()) }
        loadingMore = true
        defer { loadingMore = false }
        switch await client.getMyOrders(offset: orders.count, limit: pageSize) {
        case let .success(page):
            orders += page.items
            totalRecords = page.total
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    func clear() async {
        orders = []
        totalRecords = 0
        loaded = false
        staleness.invalidate()
    }
}
