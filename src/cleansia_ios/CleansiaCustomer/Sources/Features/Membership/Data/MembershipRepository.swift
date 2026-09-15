import CleansiaCore
import Combine
import Foundation

/// Singleton cache for the signed-in user's membership state (the
/// `MembershipRepository.kt` parity). Caches the current membership snapshot +
/// plan catalog. Registered in the `SessionScopedCacheRegistry` so sign-out /
/// forced-401 wipes it.
///
/// The plans and the subscribe commands follow the chosen market — the plans are priced per
/// market and the subscription is created in that market's currency — so the repository watches
/// the market and re-reads the plans when it answers for another one, the `CatalogRepository`
/// idiom. An existing membership keeps its own currency and never follows.
@MainActor
final class MembershipRepository: SessionScopedCache {
    @Published private(set) var current: MyMembership?
    @Published private(set) var plans: [MembershipPlan] = []
    @Published private(set) var plansState: UiState<[MembershipPlan]> = .loading
    @Published private(set) var loading = false

    /// Freshness watermark for `current`. A subscribe/cancel/swap refreshes
    /// through the membership VM, so this only gates the ambient Home resume.
    let staleness: Staleness

    private let client: MembershipManagementClient
    private(set) var marketCountryId: String?
    private var plansCountryId: String?
    private var plansRequested = false
    private var marketReload: Task<Void, Never>?
    private var cancellables = Set<AnyCancellable>()

    init(
        client: MembershipManagementClient,
        market: AnyPublisher<MarketState, Never> = Just(.unavailable).eraseToAnyPublisher(),
        staleness: Staleness = Staleness()
    ) {
        self.client = client
        self.staleness = staleness
        market
            .map(\.countryId)
            .removeDuplicates()
            .sink { [weak self] countryId in self?.followMarket(countryId) }
            .store(in: &cancellables)
    }

    @discardableResult
    func refresh() async -> ApiResult<MyMembership> {
        if loading { return current.map { .success($0) } ?? .failure(ApiError(code: "membership.loading")) }
        loading = true
        defer { loading = false }
        let result = await client.getMine()
        if case let .success(membership) = result {
            current = membership
            staleness.markFresh()
        }
        return result
    }

    /// A failed read keeps the plans already on screen; only a first read that fails is an error state.
    @discardableResult
    func refreshPlans() async -> ApiResult<[MembershipPlan]> {
        plansRequested = true
        if plans.isEmpty {
            plansState = .loading
        }
        let countryId = marketCountryId
        let result = await client.getPlans(countryId: countryId)
        switch result {
        case let .success(plans):
            self.plans = plans
            plansState = .loaded(plans)
            plansCountryId = countryId
        case let .failure(error):
            if plans.isEmpty {
                plansState = .error(error)
            }
        }
        return result
    }

    func subscribePhase1(planCode: String, idempotencyToken: String) async -> ApiResult<SubscriptionSetup> {
        await client.subscribe(
            planCode: planCode,
            paymentMethodConfirmed: false,
            countryId: marketCountryId,
            idempotencyToken: idempotencyToken
        )
    }

    func subscribePhase2(planCode: String, idempotencyToken: String) async -> ApiResult<SubscriptionSetup> {
        await client.subscribe(
            planCode: planCode,
            paymentMethodConfirmed: true,
            countryId: marketCountryId,
            idempotencyToken: idempotencyToken
        )
    }

    private func followMarket(_ countryId: String?) {
        marketCountryId = countryId
        marketReload?.cancel()
        guard plansRequested, plansCountryId != countryId else { return }
        marketReload = Task { [weak self] in
            guard let self, !Task.isCancelled else { return }
            await refreshPlans()
        }
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
        plansState = .loading
        plansCountryId = nil
        plansRequested = false
        staleness.invalidate()
    }
}
