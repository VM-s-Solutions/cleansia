import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's referral state (the
/// `ReferralRepository.kt` parity). `GetMy` lazily issues the code server-side,
/// so refresh doubles as "issue my code". The referrals page is best-effort —
/// a failure leaves the cached list as-is. Registered in the
/// `SessionScopedCacheRegistry`.
@MainActor
final class RewardsReferralRepository: SessionScopedCache {
    @Published private(set) var account: ReferralAccount?
    @Published private(set) var referrals: [ReferralListItem] = []
    @Published private(set) var loaded = false
    @Published private(set) var loading = false

    private let client: RewardsReferralClient

    init(client: RewardsReferralClient) {
        self.client = client
    }

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
        if case let .success(page) = await client.getMyReferrals(offset: 0, limit: 20) {
            referrals = page.items
        }
        loaded = true
        return .success(())
    }

    func clear() async {
        account = nil
        referrals = []
        loaded = false
    }
}
