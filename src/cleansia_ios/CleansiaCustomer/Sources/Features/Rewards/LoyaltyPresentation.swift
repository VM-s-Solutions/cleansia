import Foundation

enum TierStatus: Equatable {
    case current
    case unlocked
    case locked
}

enum DiscountSummary: Equatable {
    case noDiscount
    case basic(percent: Int)
    case minOrder(percent: Int, minOrder: Int)
}

enum LoyaltyTransactionKind: Equatable {
    case earnOrder(points: Int, order: String)
    case revokeOrder(points: Int, order: String)
    case referral(points: Int)
    case manual(points: Int)
}

enum ReferralStatsVariant: Equatable {
    case empty
    case waiting(accepted: Int)
    case qualified(accepted: Int, qualified: Int)
}

/// Pure mapping from loyalty/referral domain values to renderable presentation —
/// the `RewardsTab.kt` `composeDiscountSummary`/`transactionDescription`/stats
/// branches lifted out of the view for strict TDD.
enum LoyaltyPresentation {
    static func nextThreshold(_ account: LoyaltyAccount) -> Int? {
        guard let pointsToNext = account.pointsToNextTier, account.nextTier != nil else { return nil }
        return account.lifetimePoints + pointsToNext
    }

    static func progressFraction(_ account: LoyaltyAccount) -> Double? {
        guard let threshold = nextThreshold(account), threshold > 0 else { return nil }
        return min(max(Double(account.lifetimePoints) / Double(threshold), 0), 1)
    }

    static func discountSummary(_ tier: TierInfo) -> DiscountSummary {
        let percent = Int(tier.discountPercent * 100)
        guard percent > 0 else { return .noDiscount }
        let minOrder = Int(tier.minimumOrderAmountForDiscount ?? 0)
        return minOrder > 0 ? .minOrder(percent: percent, minOrder: minOrder) : .basic(percent: percent)
    }

    static func status(for tier: LoyaltyTier, current: LoyaltyTier) -> TierStatus {
        if tier.rawValue == current.rawValue { return .current }
        return tier.rawValue < current.rawValue ? .unlocked : .locked
    }

    /// Bronze with no backend perks falls back to the welcome badge so the perks
    /// section never renders empty (`CurrentPerksCard` parity).
    static func effectivePerks(_ perks: [TierPerk]) -> [TierPerk] {
        perks.isEmpty ? [TierPerk(icon: "badge", labelKey: "loyalty.perks.welcome_badge")] : perks
    }

    static func transactionKind(_ item: LoyaltyActivityItem) -> LoyaltyTransactionKind {
        let order = item.orderDisplayNumber.flatMap { $0.isEmpty ? nil : $0 } ?? "—"
        switch LoyaltyEarnSource(rawValue: item.source) {
        case .orderCompleted: return .earnOrder(points: item.points, order: order)
        case .orderCancelled: return .revokeOrder(points: item.points, order: order)
        case .referral: return .referral(points: item.points)
        case .manualGrant, .none: return .manual(points: item.points)
        }
    }

    static func referralStats(accepted: Int, qualified: Int) -> ReferralStatsVariant {
        if accepted <= 0 { return .empty }
        if qualified <= 0 { return .waiting(accepted: accepted) }
        return .qualified(accepted: accepted, qualified: qualified)
    }
}

enum RewardsShare {
    static func landingURL(code: String) -> String {
        "https://cleansia.cz/r/\(code)"
    }

    static func message(code: String) -> String {
        L10n.Rewards.referralShareText(code, landingURL(code: code))
    }
}
