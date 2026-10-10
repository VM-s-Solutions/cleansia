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
    private var plansFlight: Task<ApiResult<[MembershipPlan]>, Never>?
    private var plansFlightCountryId: String?
    private var plansFlightToken: UUID?
    private var plansGeneration = 0
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
    func refreshPlans(force: Bool = false) async -> ApiResult<[MembershipPlan]> {
        guard !Task.isCancelled else { return .failure(ApiError(code: ApiError.cancelledCode)) }
        let flight = startPlansFlight(force: force)
        let generation = plansGeneration
        let result = await flight.value
        guard !Task.isCancelled, generation == plansGeneration else {
            return .failure(ApiError(code: ApiError.cancelledCode))
        }
        return result
    }

    private func startPlansFlight(force: Bool) -> Task<ApiResult<[MembershipPlan]>, Never> {
        if force {
            plansGeneration += 1
            let previous = plansFlight
            plansFlight = nil
            plansFlightToken = nil
            previous?.cancel()
        }
        let countryId = marketCountryId
        if let plansFlight, plansFlightCountryId == countryId { return plansFlight }
        plansRequested = true
        let generation = plansGeneration
        let token = UUID()
        let client = client
        let flight = Task<ApiResult<[MembershipPlan]>, Never> { [weak self] in
            let result = await client.getPlans(countryId: countryId)
            guard let self else { return .failure(ApiError(code: ApiError.cancelledCode)) }
            defer {
                if plansFlightToken == token {
                    plansFlight = nil
                    plansFlightToken = nil
                    plansFlightCountryId = nil
                }
            }
            guard !Task.isCancelled, generation == plansGeneration,
                  plansFlightToken == token, marketCountryId == countryId
            else { return .failure(ApiError(code: ApiError.cancelledCode)) }
            switch result {
            case let .success(plans):
                self.plans = plans
                plansState = .loaded(plans)
                plansCountryId = countryId
            case let .failure(error):
                if plans.isEmpty { plansState = .error(error) }
            }
            return result
        }
        plansFlight = flight
        plansFlightCountryId = countryId
        plansFlightToken = token
        if plans.isEmpty { plansState = .loading }
        return flight
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
        if marketCountryId != countryId {
            plansGeneration += 1
            let previous = plansFlight
            plansFlight = nil
            plansFlightToken = nil
            plansFlightCountryId = nil
            previous?.cancel()
        }
        marketCountryId = countryId
        marketReload?.cancel()
        marketReload = nil
        guard plansRequested, plansCountryId != countryId else { return }
        marketReload = Task { [weak self] in
            guard !Task.isCancelled, let flight = self?.startPlansFlight(force: false) else { return }
            _ = await flight.value
        }
    }

    func cancel() async -> ApiResult<Date?> {
        await client.cancel()
    }

    func swapPlan(newPlanCode: String) async -> ApiResult<Void> {
        await client.swapPlan(newPlanCode: newPlanCode)
    }

    func clear() async {
        plansGeneration += 1
        let previousReload = marketReload
        let previousFlight = plansFlight
        marketReload = nil
        plansFlight = nil
        plansFlightToken = nil
        plansFlightCountryId = nil
        previousReload?.cancel()
        previousFlight?.cancel()
        current = nil
        plans = []
        plansState = .loading
        plansCountryId = nil
        plansRequested = false
        staleness.invalidate()
    }
}
