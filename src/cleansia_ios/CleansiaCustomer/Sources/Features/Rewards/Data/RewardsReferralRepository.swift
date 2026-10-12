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
    private var sessionGeneration = 0

    init(client: RewardsReferralClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<Void> {
        if loading { return .success(()) }
        let generation = sessionGeneration
        loading = true
        defer { if generation == sessionGeneration { loading = false } }
        let accountResult = await client.getMy()
        guard generation == sessionGeneration else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        switch accountResult {
        case let .success(account):
            self.account = account
        case let .failure(error):
            return .failure(error)
        }
        let referralsResult = await client.getMyReferrals(offset: 0, limit: 20)
        guard generation == sessionGeneration else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        if case let .success(page) = referralsResult {
            referrals = page.items
        }
        loaded = true
        return .success(())
    }

    func clear() async {
        sessionGeneration += 1
        loading = false
        account = nil
        referrals = []
        loaded = false
    }
}
