import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol MembershipManagementClient: Sendable {
    func getMine() async -> ApiResult<MyMembership>
    func getPlans() async -> ApiResult<[MembershipPlan]>
    func subscribe(planCode: String, paymentMethodConfirmed: Bool, idempotencyToken: String) async
        -> ApiResult<SubscriptionSetup>
    func cancel() async -> ApiResult<Date?>
    func swapPlan(newPlanCode: String) async -> ApiResult<Void>
}

struct LiveMembershipManagementClient: MembershipManagementClient {
    func getMine() async -> ApiResult<MyMembership> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipGetMine()
        }
        return result.map { $0.toDomain() }
    }

    func getPlans() async -> ApiResult<[MembershipPlan]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipGetPlans()
        }
        return result.map { $0.compactMap { $0.toDomain() } }
    }

    func subscribe(
        planCode: String,
        paymentMethodConfirmed: Bool,
        idempotencyToken: String
    ) async -> ApiResult<SubscriptionSetup> {
        let command = CreateMembershipSubscriptionCommand(
            planCode: planCode,
            paymentMethodConfirmed: paymentMethodConfirmed,
            idempotencyToken: idempotencyToken
        )
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipSubscribe(createMembershipSubscriptionCommand: command)
        }
        return result.map { $0.toDomain() }
    }

    func cancel() async -> ApiResult<Date?> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipCancel()
        }
        return result.map(\.effectiveEndDate)
    }

    func swapPlan(newPlanCode: String) async -> ApiResult<Void> {
        let command = SwapMembershipPlanCommand(newPlanCode: newPlanCode)
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipSwapPlan(swapMembershipPlanCommand: command)
        }
        return result.map { _ in () }
    }
}

private extension GetMyMembershipResponse {
    func toDomain() -> MyMembership {
        MyMembership(
            hasMembership: hasMembership ?? false,
            planCode: planCode,
            planName: planName,
            discountPercentage: discountPercentage,
            freeCancellationWindowHours: freeCancellationWindowHours,
            allowsExpressUpgrade: allowsExpressUpgrade,
            currentPeriodEnd: currentPeriodEnd,
            cancelRequested: cancelRequested ?? false,
            billingInterval: billingInterval
        )
    }
}

private extension GetMembershipPlansResponse {
    func toDomain() -> MembershipPlan? {
        guard let code, let name else { return nil }
        return MembershipPlan(
            code: code,
            name: name,
            price: price ?? 0,
            monthlyEquivalentPrice: monthlyEquivalentPrice ?? 0,
            billingInterval: billingInterval ?? 1,
            discountPercentage: discountPercentage ?? 0,
            freeCancellationWindowHours: freeCancellationWindowHours ?? 0,
            allowsExpressUpgrade: allowsExpressUpgrade ?? false,
            trialPeriodDays: trialPeriodDays ?? 0,
            savingsPercentVsMonthly: savingsPercentVsMonthly ?? 0
        )
    }
}

private extension CreateMembershipSubscriptionResponse {
    func toDomain() -> SubscriptionSetup {
        SubscriptionSetup(
            membershipId: membershipId ?? "",
            setupIntentClientSecret: setupIntentClientSecret ?? "",
            stripeCustomerId: stripeCustomerId ?? "",
            ephemeralKey: ephemeralKey ?? ""
        )
    }
}
