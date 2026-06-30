import Foundation

struct LoyaltyAccount: Equatable {
    let currentTier: Int
    let lifetimePoints: Int
    let completedBookingsCount: Int
    let tierAchievedOn: Date?
    let pointsToNextTier: Int?
    let nextTier: Int?
    let currentDiscountPercent: Double
    let currentDiscountMinOrderAmount: Double?
    let currentPerks: [TierPerk]
}

struct TierInfo: Equatable {
    let tier: Int
    let lifetimePointsThreshold: Int
    let discountPercent: Double
    let minimumOrderAmountForDiscount: Double?
    let perks: [TierPerk]
}

struct TierPerk: Equatable {
    let icon: String?
    let labelKey: String?
}

struct LoyaltyActivityItem: Equatable, Identifiable {
    let id = UUID()
    let type: Int
    let points: Int
    let source: Int
    let orderId: String?
    let orderDisplayNumber: String?
    let occurredOn: Date?
}

struct LoyaltyActivityPage: Equatable {
    let items: [LoyaltyActivityItem]
    let total: Int
}

enum LoyaltyTier: Int, CaseIterable {
    case bronzeCleaner = 1
    case silverMopper = 2
    case goldPolisher = 3
    case platinumSparkler = 4

    init?(value: Int?) {
        guard let value, let tier = LoyaltyTier(rawValue: value) else { return nil }
        self = tier
    }
}

enum LoyaltyEarnSource: Int {
    case orderCompleted = 1
    case orderCancelled = 2
    case referral = 3
    case manualGrant = 4
}
