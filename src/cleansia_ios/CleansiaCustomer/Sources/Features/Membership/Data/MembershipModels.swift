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
    let expressUpgradesPerMonth: Int
    let trialPeriodDays: Int
    let savingsPercentVsMonthly: Double
    /// The currency `price` is stated in — the market's, since a plan is priced per market.
    let currencyCode: String

    var id: String {
        code
    }

    var isAnnual: Bool {
        billingInterval == 2
    }

    var discountPerkPercent: Int? {
        let percent = Int(discountPercentage)
        return percent > 0 ? percent : nil
    }

    /// Smaller than the standard window is the perk; see `CancellationPolicyBuilder`.
    var cancellationPerkHours: Int? {
        let hours = freeCancellationWindowHours
        return hours > 0 && hours < CancellationPolicyBuilder.standardFreeHours ? hours : nil
    }

    var expressPerkPerMonth: Int? {
        allowsExpressUpgrade && expressUpgradesPerMonth > 0 ? expressUpgradesPerMonth : nil
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
    /// The subscription's own price and currency, fixed for its life — a market chosen later does
    /// not relabel them. Nil without a membership, or when the plan's row in that currency is gone.
    let price: Double?
    let monthlyEquivalentPrice: Double?
    let currencyCode: String?
    /// Past due or paused: the enrolment is live, so no second subscription, but no benefit runs and a
    /// cancel ends it at once.
    let benefitsPaused: Bool

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
        trialEndsAtUtc: Date? = nil,
        price: Double? = nil,
        monthlyEquivalentPrice: Double? = nil,
        currencyCode: String? = nil,
        benefitsPaused: Bool = false
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
        self.price = price
        self.monthlyEquivalentPrice = monthlyEquivalentPrice
        self.currencyCode = currencyCode
        self.benefitsPaused = benefitsPaused
    }
}

struct SubscriptionSetup: Equatable {
    let membershipId: String
    let setupIntentClientSecret: String
    let stripeCustomerId: String
    let ephemeralKey: String
}
