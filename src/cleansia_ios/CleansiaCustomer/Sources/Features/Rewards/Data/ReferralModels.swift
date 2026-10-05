import Foundation

struct ReferralAccount: Equatable {
    let code: String
    let timesUsed: Int
    let qualifiedCount: Int
    let acceptedCount: Int
}

struct ReferralListItem: Equatable, Identifiable {
    let id: String?
    let referredUserName: String?
    let status: Int
    let acceptedOn: Date?
    let firstQualifyingOrderOn: Date?
    /// What the referral credited the inviter, in the currency of the friend's order; nil until it qualifies,
    /// and when it paid nothing.
    let creditAwarded: MarketMoney?
}

struct ReferralListPage: Equatable {
    let items: [ReferralListItem]
    let total: Int
}

enum RewardsReferralStatus: Int {
    case accepted = 1
    case qualified = 2
    case expired = 3
}
