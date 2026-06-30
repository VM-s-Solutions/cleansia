import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's saved addresses (the
/// `AddressRepository.kt` parity, minus the Android guest/DataStore offline
/// path — the iOS port is server-scoped only). Ownership is enforced
/// server-side (`BeOwnedByCaller`); there is no client ownership check.
///
/// Mutations refetch the list rather than mirroring server invariants (default
/// demotion, the Delete empty-200 that carries no id) in two places. Registered
/// in the `SessionScopedCacheRegistry` so sign-out / forced-401 wipes it.
@MainActor
final class SavedAddressRepository: SessionScopedCache {
    @Published private(set) var addresses: [SavedAddress] = []
    @Published private(set) var loaded = false
    @Published private(set) var loading = false

    private let client: SavedAddressClient

    init(client: SavedAddressClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<Void> {
        if loading { return .success(()) }
        loading = true
        defer { loading = false }
        switch await client.getMine() {
        case let .success(list):
            addresses = list
            loaded = true
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    @discardableResult
    func add(_ draft: SavedAddressDraft) async -> ApiResult<Void> {
        switch await client.add(draft) {
        case .success:
            await reload()
        case let .failure(error):
            .failure(error)
        }
    }

    @discardableResult
    func update(id: String, draft: SavedAddressDraft) async -> ApiResult<Void> {
        switch await client.update(id: id, draft: draft) {
        case .success:
            await reload()
        case let .failure(error):
            .failure(error)
        }
    }

    @discardableResult
    func setDefault(id: String) async -> ApiResult<Void> {
        switch await client.setDefault(id: id) {
        case .success:
            await reload()
        case let .failure(error):
            .failure(error)
        }
    }

    @discardableResult
    func delete(id: String) async -> ApiResult<Void> {
        switch await client.delete(id: id) {
        case .success:
            await reload()
        case let .failure(error):
            .failure(error)
        }
    }

    /// Refetch after a mutation. A reload failure leaves the cache untouched and
    /// surfaces the error so the calling VM can snackbar it; the mutation itself
    /// already succeeded server-side.
    private func reload() async -> ApiResult<Void> {
        switch await client.getMine() {
        case let .success(list):
            addresses = list
            loaded = true
            return .success(())
        case let .failure(error):
            return .failure(error)
        }
    }

    func clear() async {
        addresses = []
        loaded = false
    }
}
