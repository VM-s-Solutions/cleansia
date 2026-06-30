import CleansiaCore
import Combine
import Foundation

struct RewardsContent: Equatable {
    let account: LoyaltyAccount
    let tiers: [TierInfo]
    let referral: ReferralAccount?
    let activityPreview: [LoyaltyActivityItem]
}

/// Backs `RewardsTab`. Reads the singleton `LoyaltyRepository` (account + tiers)
/// and `RewardsReferralRepository` (code + stats), plus a 5-item activity preview
/// (not cached by the repo). Sealed `UiState` — loading until the account lands,
/// `.error` on a first-load account failure, `.loaded` once cached.
@MainActor
final class RewardsViewModel: ViewModel {
    @Published private(set) var state: UiState<RewardsContent> = .loading

    private let loyaltyRepository: LoyaltyRepository
    private let referralRepository: RewardsReferralRepository
    private let snackbar: SnackbarController
    private let activityPreviewSize: Int

    init(
        loyaltyRepository: LoyaltyRepository,
        referralRepository: RewardsReferralRepository,
        snackbar: SnackbarController,
        activityPreviewSize: Int = 5
    ) {
        self.loyaltyRepository = loyaltyRepository
        self.referralRepository = referralRepository
        self.snackbar = snackbar
        self.activityPreviewSize = activityPreviewSize
        super.init()
        if let content = currentContent() {
            state = .loaded(content)
        }
    }

    func load() async {
        guard loyaltyRepository.account == nil else {
            await reconcile()
            return
        }
        await runLoad(initial: true)
    }

    func refresh() async {
        await runLoad(initial: false)
    }

    private func runLoad(initial: Bool) async {
        let loyaltyResult = await loyaltyRepository.refresh()
        await referralRepository.refresh()
        let preview = await loadActivityPreview()

        if case let .failure(error) = loyaltyResult {
            snackbar.showApiError(error)
            if loyaltyRepository.account == nil, initial {
                state = .error(error)
            }
        }
        publish(activityPreview: preview)
    }

    private func reconcile() async {
        let preview = await loadActivityPreview()
        publish(activityPreview: preview)
    }

    private func loadActivityPreview() async -> [LoyaltyActivityItem] {
        guard loyaltyRepository.account != nil else { return [] }
        switch await loyaltyRepository.loadActivity(offset: 0, limit: activityPreviewSize) {
        case let .success(page):
            return page.items
        case let .failure(error):
            snackbar.showApiError(error)
            return []
        }
    }

    private func publish(activityPreview: [LoyaltyActivityItem]) {
        guard let content = currentContent(activityPreview: activityPreview) else { return }
        state = .loaded(content)
    }

    private func currentContent(activityPreview: [LoyaltyActivityItem] = []) -> RewardsContent? {
        guard let account = loyaltyRepository.account else { return nil }
        return RewardsContent(
            account: account,
            tiers: loyaltyRepository.tiers,
            referral: referralRepository.account,
            activityPreview: activityPreview
        )
    }
}
