import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's membership state (the
/// `MembershipRepository.kt` parity). Caches the current membership snapshot +
/// plan catalog. Registered in the `SessionScopedCacheRegistry` so sign-out /
/// forced-401 wipes it.
@MainActor
final class MembershipRepository: SessionScopedCache {
    @Published private(set) var current: MyMembership?
    @Published private(set) var plans: [MembershipPlan] = []
    @Published private(set) var loading = false

    private let client: MembershipManagementClient

    init(client: MembershipManagementClient) {
        self.client = client
    }

    @discardableResult
    func refresh() async -> ApiResult<MyMembership> {
        if loading { return current.map { .success($0) } ?? .failure(ApiError(code: "membership.loading")) }
        loading = true
        defer { loading = false }
        let result = await client.getMine()
        if case let .success(membership) = result {
            current = membership
        }
        return result
    }

    @discardableResult
    func refreshPlans() async -> ApiResult<[MembershipPlan]> {
        let result = await client.getPlans()
        if case let .success(plans) = result {
            self.plans = plans
        }
        return result
    }

    func subscribePhase1(planCode: String, idempotencyToken: String) async -> ApiResult<SubscriptionSetup> {
        await client.subscribe(planCode: planCode, paymentMethodConfirmed: false, idempotencyToken: idempotencyToken)
    }

    func subscribePhase2(planCode: String, idempotencyToken: String) async -> ApiResult<SubscriptionSetup> {
        await client.subscribe(planCode: planCode, paymentMethodConfirmed: true, idempotencyToken: idempotencyToken)
    }

    func cancel() async -> ApiResult<Date?> {
        await client.cancel()
    }

    func swapPlan(newPlanCode: String) async -> ApiResult<Void> {
        await client.swapPlan(newPlanCode: newPlanCode)
    }

    func clear() async {
        current = nil
        plans = []
    }
}
