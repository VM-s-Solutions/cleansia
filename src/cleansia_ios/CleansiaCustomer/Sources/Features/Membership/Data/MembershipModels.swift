import Foundation

struct MembershipPlan: Equatable, Identifiable {
    let code: String
    let name: String
    let price: Double
    let monthlyEquivalentPrice: Double
    let billingInterval: Int
    let discountPercentage: Double
    let freeCancellationWindowHours: Int
    let allowsExpressUpgrade: Bool
    let trialPeriodDays: Int
    let savingsPercentVsMonthly: Double

    var id: String {
        code
    }

    var isAnnual: Bool {
        billingInterval == 2
    }
}

struct MyMembership: Equatable {
    let hasMembership: Bool
    let planCode: String?
    let planName: String?
    let discountPercentage: Double?
    let freeCancellationWindowHours: Int?
    let allowsExpressUpgrade: Bool?
    let currentPeriodEnd: Date?
    let cancelRequested: Bool
    let billingInterval: Int?
}

struct SubscriptionSetup: Equatable {
    let membershipId: String
    let setupIntentClientSecret: String
    let stripeCustomerId: String
    let ephemeralKey: String
}
