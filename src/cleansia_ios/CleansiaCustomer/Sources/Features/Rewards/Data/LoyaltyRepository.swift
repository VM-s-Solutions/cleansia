import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's loyalty state (the
/// `LoyaltyRepository.kt` parity). Caches the account snapshot, the tier ladder and the credit
/// balance; activity is paged on demand and not cached. Registered in the
/// `SessionScopedCacheRegistry` so sign-out / forced-401 wipes it.
@MainActor
final class LoyaltyRepository: ObservableObject, SessionScopedCache {
    @Published private(set) var account: LoyaltyAccount?
    @Published private(set) var tiers: [TierInfo] = []
    /// Nil until read, and after a failed read — the screens hide the credit rather than state a
    /// figure they do not have. A zero balance is a real answer and is kept.
    @Published private(set) var credit: CustomerCredit?
    @Published private(set) var loaded = false
    @Published private(set) var loading = false

    /// Freshness watermark for `account` / `tiers`, distinct from `loaded` — that
    /// is a one-way first-paint latch, so points earned after the first fetch
    /// would otherwise sit stale for the whole session. Screen-entry hooks ask
    /// `isStale` before triggering a background `refresh()`.
    let staleness: Staleness

    private let client: LoyaltyClient

    init(client: LoyaltyClient, staleness: Staleness = Staleness()) {
        self.client = client
        self.staleness = staleness
    }

    /// Fetch account + tier ladder + credit in one pass. Tiers are static config — a
    /// tiers failure leaves an empty ladder rather than failing the refresh. Credit is read the same
    /// non-blocking way: a failed read clears it, so no screen goes on stating an old balance.
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
        async let tiersRead = client.getTiers()
        async let creditRead = client.getCredit()
        if case let .success(tiers) = await tiersRead {
            self.tiers = tiers
        }
        credit = try? await creditRead.get()
        loaded = true
        staleness.markFresh()
        return .success(())
    }

    func loadActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage> {
        await client.getActivity(offset: offset, limit: limit)
    }

    func clear() async {
        account = nil
        tiers = []
        credit = nil
        loaded = false
        staleness.invalidate()
    }
}
