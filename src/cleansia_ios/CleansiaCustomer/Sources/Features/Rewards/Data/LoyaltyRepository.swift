import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's loyalty state (the
/// `LoyaltyRepository.kt` parity). Caches the account snapshot + the tier ladder;
/// activity is paged on demand and not cached. Registered in the
/// `SessionScopedCacheRegistry` so sign-out / forced-401 wipes it.
@MainActor
final class LoyaltyRepository: SessionScopedCache {
    @Published private(set) var account: LoyaltyAccount?
    @Published private(set) var tiers: [TierInfo] = []
    @Published private(set) var loaded = false
    @Published private(set) var loading = false

    private let client: LoyaltyClient

    init(client: LoyaltyClient) {
        self.client = client
    }

    /// Fetch account + tier ladder in one pass. Tiers are static config — a
    /// tiers failure leaves an empty ladder rather than failing the refresh.
    @discardableResult
    func refresh() async -> ApiResult<Void> {
        if loading { return .success(()) }
        loading = true
        defer { loading = false }
        switch await client.getMy() {
        case let .success(account):
            self.account = account
        case let .failure(error):
            return .failure(error)
        }
        if case let .success(tiers) = await client.getTiers() {
            self.tiers = tiers
        }
        loaded = true
        return .success(())
    }

    func loadActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage> {
        await client.getActivity(offset: offset, limit: limit)
    }

    func clear() async {
        account = nil
        tiers = []
        loaded = false
    }
}
