import Foundation

struct ReferralAccount: Equatable {
    let code: String
    let timesUsed: Int
    let qualifiedCount: Int
    let acceptedCount: Int
    let pointsPerReferral: Int
}

struct ReferralListItem: Equatable, Identifiable {
    let id: String?
    let referredUserName: String?
    let status: Int
    let acceptedOn: Date?
    let firstQualifyingOrderOn: Date?
    let pointsAwardedToReferrer: Int?
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
