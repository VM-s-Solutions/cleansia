import CleansiaCore
import Combine
import Foundation
import XCTest
@testable import CleansiaCustomer

final class FakeMembershipManagementClient: MembershipManagementClient, @unchecked Sendable {
    var mineResults: [ApiResult<MyMembership>] = [.success(MembershipFixtures.inactive)]
    private(set) var mineCallCount = 0
    var holdMine = false
    var mineStarted: ((Int) -> Void)?
    private var heldMine: [Int: CheckedContinuation<ApiResult<MyMembership>, Never>] = [:]

    var plansResult: ApiResult<[MembershipPlan]> = .success(MembershipFixtures.plans)
    private(set) var plansCallCount = 0
    private(set) var plansCountryIds: [String?] = []
    var holdPlans = false
    var plansStarted: ((Int, String?) -> Void)?
    var plansCompleted: ((Int) -> Void)?
    private var heldPlans: [Int: CheckedContinuation<ApiResult<[MembershipPlan]>, Never>] = [:]

    var phase1Result: ApiResult<SubscriptionSetup> = .success(MembershipFixtures.setup)
    var phase2Result: ApiResult<SubscriptionSetup> = .success(MembershipFixtures.subscribed)
    // swiftlint:disable:next large_tuple
    private(set) var subscribeCalls: [(planCode: String, confirmed: Bool, countryId: String?, token: String)] = []

    var cancelResult: ApiResult<Date?> = .success(Date(timeIntervalSince1970: 1_780_000_000))
    private(set) var cancelCallCount = 0

    var swapResult: ApiResult<Void> = .success(())
    private(set) var swapCodes: [String] = []

    @MainActor
    func getMine() async -> ApiResult<MyMembership> {
        let index = min(mineCallCount, mineResults.count - 1)
        mineCallCount += 1
        let call = mineCallCount
        guard holdMine else {
            mineStarted?(call)
            guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
            return mineResults[index]
        }
        return await withCheckedContinuation { continuation in
            heldMine[call] = continuation
            mineStarted?(call)
        }
    }

    @MainActor
    func releaseMine(call: Int, result: ApiResult<MyMembership>) {
        heldMine.removeValue(forKey: call)?.resume(returning: result)
    }

    @MainActor
    func getPlans(countryId: String?) async -> ApiResult<[MembershipPlan]> {
        plansCallCount += 1
        let call = plansCallCount
        plansCountryIds.append(countryId)
        defer { plansCompleted?(call) }
        guard holdPlans else {
            plansStarted?(call, countryId)
            return plansResult
        }
        return await withCheckedContinuation { continuation in
            heldPlans[call] = continuation
            plansStarted?(call, countryId)
        }
    }

    @MainActor
    func releasePlans(call: Int, result: ApiResult<[MembershipPlan]>) {
        heldPlans.removeValue(forKey: call)?.resume(returning: result)
    }

    @MainActor
    func releaseAllPlans() {
        holdPlans = false
        let pending = heldPlans.values
        heldPlans = [:]
        pending.forEach { $0.resume(returning: plansResult) }
    }

    func subscribe(
        planCode: String,
        paymentMethodConfirmed: Bool,
        countryId: String?,
        idempotencyToken: String
    ) async -> ApiResult<SubscriptionSetup> {
        subscribeCalls.append((planCode, paymentMethodConfirmed, countryId, idempotencyToken))
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
        billingInterval: 1,
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining: 1,
        price: 199,
        monthlyEquivalentPrice: 199,
        currencyCode: "CZK"
    )

    static let pastDue = MyMembership(
        hasMembership: true,
        planCode: "plus_monthly",
        planName: "Cleansia Plus",
        discountPercentage: 5,
        freeCancellationWindowHours: 4,
        allowsExpressUpgrade: true,
        currentPeriodEnd: Date(timeIntervalSince1970: 1_780_000_000),
        cancelRequested: false,
        billingInterval: 1,
        expressUpgradesPerMonth: 2,
        expressUpgradesRemaining: 1,
        price: 199,
        monthlyEquivalentPrice: 199,
        currencyCode: "CZK",
        benefitsPaused: true
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
            expressUpgradesPerMonth: 2,
            trialPeriodDays: 14,
            savingsPercentVsMonthly: 0,
            currencyCode: "CZK"
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
            expressUpgradesPerMonth: 2,
            trialPeriodDays: 14,
            savingsPercentVsMonthly: 15,
            currencyCode: "CZK"
        )
    ]

    /// The same two plans priced for a EUR market.
    static let plansInEur = plans.map { plan in
        MembershipPlan(
            code: plan.code,
            name: plan.name,
            price: plan.price / 25,
            monthlyEquivalentPrice: plan.monthlyEquivalentPrice / 25,
            billingInterval: plan.billingInterval,
            discountPercentage: plan.discountPercentage,
            freeCancellationWindowHours: plan.freeCancellationWindowHours,
            allowsExpressUpgrade: plan.allowsExpressUpgrade,
            expressUpgradesPerMonth: plan.expressUpgradesPerMonth,
            trialPeriodDays: plan.trialPeriodDays,
            savingsPercentVsMonthly: plan.savingsPercentVsMonthly,
            currencyCode: "EUR"
        )
    }
}

@MainActor
extension MembershipViewModelTests {
    func testConcurrentChosenCountryPlanFailureIsSharedAndNextReadRetries() async {
        let entered = expectation(description: "chosen country API entered")
        let secondEntered = expectation(description: "second failure waiter entered")
        let completed = expectation(description: "chosen country API completed")
        let client = FakeMembershipManagementClient()
        let error = ApiError(httpStatus: 500)
        client.plansResult = .failure(error)
        client.holdPlans = true
        client.plansStarted = { call, _ in if call == 1 { entered.fulfill() } }
        client.plansCompleted = { call in if call == 1 { completed.fulfill() } }
        let market = await MarketFixtures.resolved()
        let repository = MembershipRepository(client: client, market: market.statePublisher)
        let first = Task { await repository.refreshPlans() }
        await fulfillment(of: [entered], timeout: 2)
        let second = Task {
            secondEntered.fulfill()
            return await repository.refreshPlans()
        }
        await fulfillment(of: [secondEntered], timeout: 2)
        XCTAssertEqual(client.plansCallCount, 1)
        client.releaseAllPlans()
        let firstResult = await first.value
        let secondResult = await second.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(firstResult, .failure(error))
        XCTAssertEqual(secondResult, .failure(error))
        client.plansResult = .success(MembershipFixtures.plans)
        let retry = await repository.refreshPlans()
        XCTAssertEqual(try? retry.get(), MembershipFixtures.plans)
        XCTAssertEqual(client.plansCountryIds, ["cze", "cze"])
        first.cancel()
        second.cancel()
    }

    func testCancellingFirstPlanWaiterDoesNotCancelSharedPublication() async {
        let entered = expectation(description: "owned plan API entered")
        let secondEntered = expectation(description: "surviving plan waiter entered")
        let completed = expectation(description: "owned plan API completed")
        let client = FakeMembershipManagementClient()
        client.holdPlans = true
        client.plansStarted = { call, _ in if call == 1 { entered.fulfill() } }
        client.plansCompleted = { call in if call == 1 { completed.fulfill() } }
        let repository = MembershipRepository(client: client)
        var publications = 0
        let observation = repository.$plans.dropFirst().sink { _ in publications += 1 }
        let first = Task { await repository.refreshPlans() }
        await fulfillment(of: [entered], timeout: 2)
        let second = Task {
            secondEntered.fulfill()
            return await repository.refreshPlans()
        }
        await fulfillment(of: [secondEntered], timeout: 2)
        first.cancel()
        client.releaseAllPlans()
        let firstResult = await first.value
        let secondResult = await second.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(firstResult, .failure(ApiError(code: ApiError.cancelledCode)))
        XCTAssertEqual(try? secondResult.get(), MembershipFixtures.plans)
        XCTAssertEqual(repository.plans, MembershipFixtures.plans)
        XCTAssertEqual(client.plansCallCount, 1)
        XCTAssertEqual(publications, 1)
        observation.cancel()
        second.cancel()
    }

    func testReloadPlansForcesFreshFlightAndOldCompletionCannotOverwriteIt() async {
        let oldEntered = expectation(description: "old plan API entered")
        let forcedEntered = expectation(description: "forced plan API entered")
        let completed = expectation(description: "both plan APIs completed")
        completed.expectedFulfillmentCount = 2
        let client = FakeMembershipManagementClient()
        client.holdPlans = true
        client.plansStarted = { call, _ in
            if call == 1 { oldEntered.fulfill() }
            if call == 2 { forcedEntered.fulfill() }
        }
        client.plansCompleted = { _ in completed.fulfill() }
        let repository = MembershipRepository(client: client)
        let vm = MembershipViewModel(
            repository: repository, snackbar: SnackbarController(), isCardPaymentAvailable: true
        )
        let old = Task { await repository.refreshPlans() }
        await fulfillment(of: [oldEntered], timeout: 2)
        let forced = Task { await vm.reloadPlans() }
        await fulfillment(of: [forcedEntered], timeout: 2)
        XCTAssertEqual(client.plansCallCount, 2)
        if client.plansCallCount != 2 { client.releaseAllPlans() }
        client.releasePlans(call: 2, result: .success(MembershipFixtures.plansInEur))
        await forced.value
        XCTAssertEqual(vm.plans, MembershipFixtures.plansInEur)
        client.releasePlans(call: 1, result: .success(MembershipFixtures.plans))
        client.releaseAllPlans()
        let oldResult = await old.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(oldResult, .failure(ApiError(code: ApiError.cancelledCode)))
        XCTAssertEqual(vm.plans, MembershipFixtures.plansInEur)
        XCTAssertEqual(client.plansCallCount, 2)
        old.cancel()
        forced.cancel()
    }

    func testReversedMarketCompletionKeepsNewCountryPlans() async {
        let oldEntered = expectation(description: "CZ plans entered")
        let newEntered = expectation(description: "SK plans entered")
        let newPublished = expectation(description: "SK plans published")
        let completed = expectation(description: "both markets completed")
        completed.expectedFulfillmentCount = 2
        let client = FakeMembershipManagementClient()
        client.holdPlans = true
        client.plansStarted = { _, country in
            if country == "cze" { oldEntered.fulfill() }
            if country == "svk" { newEntered.fulfill() }
        }
        client.plansCompleted = { _ in completed.fulfill() }
        let market = await MarketFixtures.resolved()
        let repository = MembershipRepository(client: client, market: market.statePublisher)
        let observation = repository.$plans.dropFirst().sink { plans in
            if plans.first?.currencyCode == "EUR" { newPublished.fulfill() }
        }
        let old = Task { await repository.refreshPlans() }
        await fulfillment(of: [oldEntered], timeout: 2)
        market.select(isoCode: "SVK")
        await fulfillment(of: [newEntered], timeout: 2)
        client.releasePlans(call: 2, result: .success(MembershipFixtures.plansInEur))
        await fulfillment(of: [newPublished], timeout: 2)
        client.releasePlans(call: 1, result: .success(MembershipFixtures.plans))
        client.releaseAllPlans()
        let oldResult = await old.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(oldResult, .failure(ApiError(code: ApiError.cancelledCode)))
        XCTAssertEqual(repository.plans, MembershipFixtures.plansInEur)
        XCTAssertEqual(client.plansCountryIds, ["cze", "svk"])
        observation.cancel()
        await repository.clear()
        old.cancel()
    }

    func testClearedNilCountryFlightCannotPublishIntoNewSession() async {
        let oldEntered = expectation(description: "old session plans entered")
        let newEntered = expectation(description: "new session plans entered")
        let completed = expectation(description: "both session APIs completed")
        completed.expectedFulfillmentCount = 2
        let client = FakeMembershipManagementClient()
        client.holdPlans = true
        client.plansStarted = { call, _ in
            if call == 1 { oldEntered.fulfill() }
            if call == 2 { newEntered.fulfill() }
        }
        client.plansCompleted = { _ in completed.fulfill() }
        let repository = MembershipRepository(client: client)
        let old = Task { await repository.refreshPlans() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()
        XCTAssertTrue(repository.plans.isEmpty)
        XCTAssertTrue(repository.plansState.isLoading)
        let new = Task { await repository.refreshPlans() }
        await fulfillment(of: [newEntered], timeout: 2)
        XCTAssertEqual(client.plansCallCount, 2)
        if client.plansCallCount != 2 { client.releaseAllPlans() }
        client.releasePlans(call: 2, result: .success(MembershipFixtures.plansInEur))
        let newResult = await new.value
        client.releasePlans(call: 1, result: .success(MembershipFixtures.plans))
        client.releaseAllPlans()
        let oldResult = await old.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(try? newResult.get(), MembershipFixtures.plansInEur)
        XCTAssertEqual(oldResult, .failure(ApiError(code: ApiError.cancelledCode)))
        XCTAssertEqual(repository.plans, MembershipFixtures.plansInEur)
        XCTAssertEqual(client.plansCallCount, 2)
        old.cancel()
        new.cancel()
    }

    /// Returning to a market whose plans had loaded empty, after a flight for another market was cancelled on
    /// the way, left the plans `.loading`: the market had not changed from the last success, so nothing re-read
    /// them and nothing restored the state the cancelled flight had replaced.
    func testReturningToAnEmptyMarketAfterACancelledFlightSettlesThePlans() async {
        let slovakEntered = expectation(description: "SK plans entered")
        let czechAgainEntered = expectation(description: "CZ plans re-read")
        let settled = expectation(description: "CZ plans settled empty")
        let client = FakeMembershipManagementClient()
        client.plansResult = .success([])
        let market = await MarketFixtures.resolved()
        let repository = MembershipRepository(client: client, market: market.statePublisher)
        _ = await repository.refreshPlans()
        XCTAssertEqual(repository.plansState.loadedValue, [])
        client.holdPlans = true
        client.plansStarted = { call, country in
            if call == 2, country == "svk" { slovakEntered.fulfill() }
            if call == 3, country == "cze" { czechAgainEntered.fulfill() }
        }
        market.select(isoCode: "SVK")
        await fulfillment(of: [slovakEntered], timeout: 2)
        XCTAssertTrue(repository.plansState.isLoading)
        let observation = repository.$plansState.dropFirst().sink { state in
            if state.loadedValue == [] { settled.fulfill() }
        }
        market.select(isoCode: "CZE")
        await fulfillment(of: [czechAgainEntered], timeout: 2)
        client.releasePlans(call: 3, result: .success([]))
        await fulfillment(of: [settled], timeout: 2)
        client.releasePlans(call: 2, result: .success(MembershipFixtures.plansInEur))
        client.releaseAllPlans()
        XCTAssertEqual(repository.plansState.loadedValue, [])
        XCTAssertEqual(repository.plans, [])
        XCTAssertEqual(client.plansCountryIds, ["cze", "svk", "cze"])
        observation.cancel()
        await repository.clear()
    }

    /// A membership read still in flight at sign-out answered into the next session: it put the previous
    /// user's membership back after `clear()` and marked it fresh (S11).
    func testRefreshHeldAcrossClearCannotRestoreThePreviousUsersMembership() async {
        let entered = expectation(description: "membership read entered")
        let client = FakeMembershipManagementClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = MembershipRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)
        await repository.clear()
        client.releaseMine(call: 1, result: .success(MembershipFixtures.active))
        let result = await refresh.value

        XCTAssertEqual(result, .failure(ApiError(code: ApiError.cancelledCode)))
        XCTAssertNil(repository.current)
        XCTAssertTrue(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
    }

    /// The previous session's read left `loading` set across `clear()`, so the next user's own refresh
    /// returned without reading and the staleness gate then held its readers off.
    func testANewSessionRefreshIsNotSwallowedByAnOldInFlightOne() async {
        let oldEntered = expectation(description: "old session read entered")
        let newEntered = expectation(description: "new session read entered")
        let client = FakeMembershipManagementClient()
        client.holdMine = true
        client.mineStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = MembershipRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()
        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseMine(call: 2, result: .success(MembershipFixtures.inactive))
        let newResult = await new.value
        client.releaseMine(call: 1, result: .success(MembershipFixtures.active))
        _ = await old.value

        XCTAssertEqual(newResult, .success(MembershipFixtures.inactive))
        XCTAssertEqual(repository.current, MembershipFixtures.inactive)
        XCTAssertFalse(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
        XCTAssertEqual(client.mineCallCount, 2)
    }

    /// The previous session's read answering first must not end the new session's `loading`: a refresh made
    /// meanwhile would read a second time inside the same session.
    func testAnOldSessionReadAnsweringFirstLeavesTheNewSessionsReadLoading() async {
        let oldEntered = expectation(description: "old session read entered")
        let newEntered = expectation(description: "new session read entered")
        let client = FakeMembershipManagementClient()
        client.holdMine = true
        client.mineStarted = { call in
            if call == 1 { oldEntered.fulfill() } else if call == 2 { newEntered.fulfill() }
        }
        let repository = MembershipRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()
        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseMine(call: 1, result: .success(MembershipFixtures.active))
        _ = await old.value

        XCTAssertTrue(repository.loading)
        client.holdMine = false
        _ = await repository.refresh()
        XCTAssertEqual(client.mineCallCount, 2)

        client.releaseMine(call: 2, result: .success(MembershipFixtures.inactive))
        _ = await new.value
        XCTAssertFalse(repository.loading)
        XCTAssertEqual(repository.current, MembershipFixtures.inactive)
    }

    /// The session's counter is its own: a forced plans reload moves the plans' counter, and a membership read
    /// in flight across it still answers.
    func testAForcedPlansReloadDoesNotDiscardAMembershipRefresh() async {
        let entered = expectation(description: "membership read entered")
        let client = FakeMembershipManagementClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = MembershipRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)
        _ = await repository.refreshPlans(force: true)
        client.releaseMine(call: 1, result: .success(MembershipFixtures.active))
        let result = await refresh.value

        XCTAssertEqual(result, .success(MembershipFixtures.active))
        XCTAssertEqual(repository.current, MembershipFixtures.active)
    }

    func testSequentialPlanReadsStayFreshIncludingEmptySuccess() async {
        let client = FakeMembershipManagementClient()
        let repository = MembershipRepository(client: client)
        _ = await repository.refreshPlans()
        client.plansResult = .success([])
        let second = await repository.refreshPlans()
        let third = await repository.refreshPlans()
        XCTAssertEqual(try? second.get(), [])
        XCTAssertEqual(try? third.get(), [])
        XCTAssertEqual(repository.plansState.loadedValue, [])
        XCTAssertEqual(client.plansCallCount, 3)
    }
}
