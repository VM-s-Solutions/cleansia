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

/// Money the platform owes the customer — not points. Points move the tier and are never spent; credit
/// comes off a card booking by itself (`GET /api/Credit/GetMy`). → /product/business-rules
struct CustomerCredit: Equatable {
    struct Balance: Equatable {
        let amount: Double
        let currencyCode: String
        /// When this currency's balance lapses if nothing moves it; nil when there is nothing to lapse.
        let expiresOn: Date?
    }

    /// The largest balance, or a zero in the platform default when the customer holds none.
    let primary: Balance
    /// Every currency held, largest first. Empty for a customer who has never been credited.
    let balances: [Balance]
    /// The share of one order credit may settle, as a fraction — the server's number, never a literal.
    let maxShareOfOrder: Double

    /// The balances there is something in, largest first — what the Rewards card lists. Empty for a
    /// customer holding nothing, whose card collapses to one line.
    var heldBalances: [Balance] {
        (balances.isEmpty ? [primary] : balances).filter { $0.amount > 0 }
    }
}
