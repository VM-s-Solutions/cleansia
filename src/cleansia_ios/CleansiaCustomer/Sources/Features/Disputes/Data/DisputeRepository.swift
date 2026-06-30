import CleansiaCore
import Combine
import Foundation

/// Singleton cache + paginator for the signed-in user's disputes (the
/// `DisputeRepository.kt` parity, mirroring the Slice-A `OrderRepository`).
/// `refresh()` replaces page 0, `loadNextPage()` appends additively. Details are
/// NOT cached — each `getById` hits the network (message threads change often).
/// Registered in the `SessionScopedCacheRegistry` so sign-out / forced-401 wipes
/// it; own-dispute scoping is SERVER-enforced (Gate-SEC R10), the client adds no
/// ownership check.
@MainActor
final class DisputeRepository: SessionScopedCache {
    @Published private(set) var disputes: [DisputeListEntry] = []
    @Published private(set) var totalRecords = 0
    @Published private(set) var loaded = false
    @Published private(set) var loading = false
    @Published private(set) var loadingMore = false

    private let client: DisputeClient
    private let pageSize: Int

    init(client: DisputeClient, pageSize: Int = 20) {
        self.client = client
        self.pageSize = pageSize
    }

    var hasMore: Bool {
        disputes.count < totalRecords
    }

    @discardableResult
    func refresh() async -> ApiResult<Void> {
        if loading { return .success(()) }
        loading = true
        defer { loading = false }
        switch await client.getPaged(offset: 0, limit: pageSize) {
        case let .success(page):
            disputes = page.items
            totalRecords = page.total
            loaded = true
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    @discardableResult
    func loadNextPage() async -> ApiResult<Void> {
        if loadingMore { return .success(()) }
        guard hasMore else { return .success(()) }
        loadingMore = true
        defer { loadingMore = false }
        switch await client.getPaged(offset: disputes.count, limit: pageSize) {
        case let .success(page):
            disputes += page.items
            totalRecords = page.total
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    func getById(_ id: String) async -> ApiResult<DisputeDetail> {
        await client.getById(disputeId: id)
    }

    func create(orderId: String, reason: Int, description: String) async -> ApiResult<String> {
        await client.create(orderId: orderId, reason: reason, description: description)
    }

    func addMessage(disputeId: String, message: String) async -> ApiResult<Void> {
        await client.addMessage(disputeId: disputeId, message: message)
    }

    func uploadEvidence(disputeId: String, file: URL) async -> ApiResult<DisputeEvidence> {
        await client.uploadEvidence(disputeId: disputeId, file: file)
    }

    func clear() async {
        disputes = []
        totalRecords = 0
        loaded = false
    }
}
