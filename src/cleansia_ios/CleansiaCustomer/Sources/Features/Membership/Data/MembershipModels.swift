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
    let expressUpgradesPerMonth: Int?
    let expressUpgradesRemaining: Int?
    let trialEndsAtUtc: Date?

    init(
        hasMembership: Bool,
        planCode: String?,
        planName: String?,
        discountPercentage: Double?,
        freeCancellationWindowHours: Int?,
        allowsExpressUpgrade: Bool?,
        currentPeriodEnd: Date?,
        cancelRequested: Bool,
        billingInterval: Int?,
        expressUpgradesPerMonth: Int? = nil,
        expressUpgradesRemaining: Int? = nil,
        trialEndsAtUtc: Date? = nil
    ) {
        self.hasMembership = hasMembership
        self.planCode = planCode
        self.planName = planName
        self.discountPercentage = discountPercentage
        self.freeCancellationWindowHours = freeCancellationWindowHours
        self.allowsExpressUpgrade = allowsExpressUpgrade
        self.currentPeriodEnd = currentPeriodEnd
        self.cancelRequested = cancelRequested
        self.billingInterval = billingInterval
        self.expressUpgradesPerMonth = expressUpgradesPerMonth
        self.expressUpgradesRemaining = expressUpgradesRemaining
        self.trialEndsAtUtc = trialEndsAtUtc
    }
}

struct SubscriptionSetup: Equatable {
    let membershipId: String
    let setupIntentClientSecret: String
    let stripeCustomerId: String
    let ephemeralKey: String
}
