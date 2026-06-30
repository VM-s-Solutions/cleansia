import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeMembershipManagementClient: MembershipManagementClient, @unchecked Sendable {
    var mineResults: [ApiResult<MyMembership>] = [.success(MembershipFixtures.inactive)]
    private(set) var mineCallCount = 0

    var plansResult: ApiResult<[MembershipPlan]> = .success(MembershipFixtures.plans)
    private(set) var plansCallCount = 0

    var phase1Result: ApiResult<SubscriptionSetup> = .success(MembershipFixtures.setup)
    var phase2Result: ApiResult<SubscriptionSetup> = .success(MembershipFixtures.subscribed)
    private(set) var subscribeCalls: [(planCode: String, confirmed: Bool, token: String)] = []

    var cancelResult: ApiResult<Date?> = .success(Date(timeIntervalSince1970: 1_780_000_000))
    private(set) var cancelCallCount = 0

    var swapResult: ApiResult<Void> = .success(())
    private(set) var swapCodes: [String] = []

    func getMine() async -> ApiResult<MyMembership> {
        defer { mineCallCount += 1 }
        let index = min(mineCallCount, mineResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return mineResults[index]
    }

    func getPlans() async -> ApiResult<[MembershipPlan]> {
        plansCallCount += 1
        return plansResult
    }

    func subscribe(
        planCode: String,
        paymentMethodConfirmed: Bool,
        idempotencyToken: String
    ) async -> ApiResult<SubscriptionSetup> {
        subscribeCalls.append((planCode, paymentMethodConfirmed, idempotencyToken))
        return paymentMethodConfirmed ? phase2Result : phase1Result
    }

    func cancel() async -> ApiResult<Date?> {
        cancelCallCount += 1
        return cancelResult
    }

    func swapPlan(newPlanCode: String) async -> ApiResult<Void> {
        swapCodes.append(newPlanCode)
        return swapResult
    }
}

enum MembershipFixtures {
    static let inactive = MyMembership(
        hasMembership: false,
        planCode: nil,
        planName: nil,
        discountPercentage: nil,
        freeCancellationWindowHours: nil,
        allowsExpressUpgrade: nil,
        currentPeriodEnd: nil,
        cancelRequested: false,
        billingInterval: nil
    )

    static let active = MyMembership(
        hasMembership: true,
        planCode: "plus_monthly",
        planName: "Cleansia Plus",
        discountPercentage: 5,
        freeCancellationWindowHours: 4,
        allowsExpressUpgrade: true,
        currentPeriodEnd: Date(timeIntervalSince1970: 1_780_000_000),
        cancelRequested: false,
        billingInterval: 1
    )

    static let setup = SubscriptionSetup(
        membershipId: "",
        setupIntentClientSecret: "seti_secret_abc",
        stripeCustomerId: "cus_1",
        ephemeralKey: "ek_1"
    )

    static let subscribed = SubscriptionSetup(
        membershipId: "mem-99",
        setupIntentClientSecret: "",
        stripeCustomerId: "cus_1",
        ephemeralKey: "ek_1"
    )

    static let plans = [
        MembershipPlan(
            code: "plus_monthly",
            name: "Monthly",
            price: 199,
            monthlyEquivalentPrice: 199,
            billingInterval: 1,
            discountPercentage: 5,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            trialPeriodDays: 14,
            savingsPercentVsMonthly: 0
        ),
        MembershipPlan(
            code: "plus_yearly",
            name: "Annual",
            price: 2030,
            monthlyEquivalentPrice: 169,
            billingInterval: 2,
            discountPercentage: 5,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            trialPeriodDays: 14,
            savingsPercentVsMonthly: 15
        )
    ]
}
