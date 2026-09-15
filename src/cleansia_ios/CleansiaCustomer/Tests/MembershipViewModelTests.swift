import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class MembershipViewModelTests: XCTestCase {
    private func makeVM(
        client: FakeMembershipManagementClient = FakeMembershipManagementClient(),
        market: MarketStore? = nil,
        cardAvailable: Bool = true
    ) -> (MembershipViewModel, MembershipRepository, FakeMembershipManagementClient) {
        let repo = market.map { MembershipRepository(client: client, market: $0.statePublisher) }
            ?? MembershipRepository(client: client)
        let vm = MembershipViewModel(
            repository: repo,
            snackbar: SnackbarController(),
            isCardPaymentAvailable: cardAvailable
        )
        return (vm, repo, client)
    }

    func testStartsIdle() {
        let (vm, _, _) = makeVM()
        XCTAssertEqual(vm.submitState, .idle)
    }

    // MARK: The market — plans priced for it, the subscription created in its currency

    /// Every plan carries the code its price is stated in, so the label is the payload's, never the
    /// catalogue's default and never a literal.
    func testEachPlanIsLabelledWithItsOwnCurrency() async {
        let client = FakeMembershipManagementClient()
        client.plansResult = .success(MembershipFixtures.plansInEur)
        let (vm, _, _) = makeVM(client: client)

        await vm.load()

        let plan = try? XCTUnwrap(vm.plans.first)
        XCTAssertEqual(plan?.currencyCode, "EUR")
        XCTAssertEqual(MembershipFormat.price(plan?.price ?? 0, currencyCode: plan?.currencyCode), "8 €")
    }

    func testThePlansAreRequestedForTheChosenMarket() async {
        let market = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        let (vm, _, client) = makeVM(market: market)

        await vm.load()

        XCTAssertEqual(client.plansCountryIds, ["svk"])
    }

    func testBothSubscribePhasesCarryTheChosenMarket() async {
        let market = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        let (vm, _, client) = makeVM(market: market)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        _ = await vm.confirmSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(client.subscribeCalls.map(\.countryId), ["svk", "svk"])
    }

    /// The no-market state: nothing is sent, and the server prices in the platform default.
    func testWithoutAMarketThePlansAndTheSubscribeCarryNoCountry() async {
        let market = await MarketFixtures.unavailable()
        let (vm, _, client) = makeVM(market: market)

        await vm.load()
        _ = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(client.plansCountryIds, [nil])
        XCTAssertEqual(client.subscribeCalls.map(\.countryId), [nil])
    }

    func testSwitchingTheMarketReReadsThePlansForItWithoutARestart() async {
        let market = await MarketFixtures.resolved()
        let (vm, _, client) = makeVM(market: market)
        await vm.load()
        XCTAssertEqual(client.plansCountryIds, ["cze"])

        client.plansResult = .success(MembershipFixtures.plansInEur)
        market.select(isoCode: "SVK")
        await drain()

        XCTAssertEqual(client.plansCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.plans.first?.currencyCode, "EUR")
    }

    func testAMarketChangeBeforeThePlansWereEverReadDoesNotReadThem() async {
        let market = await MarketFixtures.resolved()
        let (_, _, client) = makeVM(market: market)

        market.select(isoCode: "SVK")
        await drain()

        XCTAssertEqual(client.plansCallCount, 0)
    }

    /// Plus not on sale in the market: an empty list is a loaded state of its own — no price, no
    /// button — never a zero and never an error.
    func testAnEmptyPlanListIsPlusNotAvailableInThisMarket() async {
        let client = FakeMembershipManagementClient()
        client.plansResult = .success([])
        let (vm, _, _) = makeVM(client: client)
        XCTAssertTrue(vm.plansState.isLoading)

        await vm.load()

        guard case let .loaded(plans) = vm.plansState else { return XCTFail("expected loaded, got \(vm.plansState)") }
        XCTAssertEqual(plans, [])
    }

    func testAFailedFirstPlanReadIsAnErrorStateAndALaterFailureKeepsThePlans() async {
        let client = FakeMembershipManagementClient()
        client.plansResult = .failure(ApiError(httpStatus: 500))
        let (vm, _, _) = makeVM(client: client)

        await vm.load()
        guard case .error = vm.plansState else { return XCTFail("expected error, got \(vm.plansState)") }

        client.plansResult = .success(MembershipFixtures.plans)
        await vm.reloadPlans()
        XCTAssertEqual(vm.plansState.loadedValue?.count, 2)

        client.plansResult = .failure(ApiError(httpStatus: 500))
        await vm.reloadPlans()
        XCTAssertEqual(vm.plansState.loadedValue?.count, 2, "a failed re-read keeps the plans on screen")
    }

    // MARK: The membership keeps its currency

    func testTheMembershipCarriesItsOwnPriceAndCurrency() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active)]
        let (vm, _, _) = makeVM(client: client)

        await vm.refresh()

        XCTAssertEqual(vm.current?.price, 199)
        XCTAssertEqual(vm.current?.currencyCode, "CZK")
    }

    /// A swap charges the plan's row in the subscription's currency whatever market is chosen, so the
    /// annual switch is offered only when the listed plan is priced in that same currency.
    func testTheAnnualSwitchIsOfferedOnlyWhenTheListedPlanIsInTheMembershipsCurrency() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active)]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()
        XCTAssertEqual(vm.annualSwitchPlan?.code, "plus_yearly")

        client.plansResult = .success(MembershipFixtures.plansInEur)
        await vm.reloadPlans()

        XCTAssertNil(vm.annualSwitchPlan, "a EUR price must not be shown for a CZK swap")
    }

    func testTheAnnualSwitchIsNotOfferedWithoutAnActiveMonthlyMembership() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.inactive)]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()

        XCTAssertNil(vm.annualSwitchPlan)
    }

    // MARK: Stripe's one currency per Customer

    func testTheCurrencyLockRefusalNamesTheMembershipsCurrencyWhenKnown() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active)]
        client.phase1Result = .failure(ApiError(code: MembershipViewModel.currencyLockedKey, httpStatus: 400))
        let snackbar = SnackbarController()
        let repo = MembershipRepository(client: client)
        let vm = MembershipViewModel(repository: repo, snackbar: snackbar, isCardPaymentAvailable: true)
        await vm.refresh()

        let outcome = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .failed)
        XCTAssertEqual(snackbar.current?.text, L10n.Membership.currencyLockedIn("CZK"))
    }

    func testTheCurrencyLockRefusalWithoutAKnownCurrencyFallsToTheCatalogSentence() async {
        let client = FakeMembershipManagementClient()
        client.phase1Result = .failure(ApiError(code: MembershipViewModel.currencyLockedKey, httpStatus: 400))
        let snackbar = SnackbarController()
        let vm = MembershipViewModel(
            repository: MembershipRepository(client: client),
            snackbar: snackbar,
            isCardPaymentAvailable: true
        )

        _ = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(
            snackbar.current?.text,
            ApiErrorLocalizer().message(for: ApiError(code: MembershipViewModel.currencyLockedKey, httpStatus: 400))
        )
        XCTAssertNotEqual(snackbar.current?.text, MembershipViewModel.currencyLockedKey, "the raw key must never show")
    }

    private func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }

    // MARK: Phase 1 — SetupIntent

    func testStartSubscribeSuccessReturnsNeedsPaymentMethodSetupIntent() async {
        let (vm, _, client) = makeVM()

        let outcome = await vm.startSubscribe(planCode: "plus_monthly")

        guard case let .needsPaymentMethod(presentation) = outcome else {
            return XCTFail("expected needsPaymentMethod, got \(outcome)")
        }
        XCTAssertEqual(presentation.intentKind, .setup)
        XCTAssertEqual(presentation.clientSecret, "seti_secret_abc")
        XCTAssertEqual(presentation.stripeCustomerId, "cus_1")
        XCTAssertEqual(presentation.ephemeralKey, "ek_1")
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(client.subscribeCalls.count, 1)
        XCTAssertEqual(client.subscribeCalls.first?.confirmed, false)
    }

    func testStartSubscribeWithExistingMembershipReturnsAlreadyActive() async {
        let client = FakeMembershipManagementClient()
        client.phase1Result = .success(SubscriptionSetup(
            membershipId: "mem-existing", setupIntentClientSecret: "x", stripeCustomerId: "c", ephemeralKey: "e"
        ))
        let (vm, _, _) = makeVM(client: client)

        let outcome = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .alreadyActive)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testStartSubscribeFailureReturnsFailed() async {
        let client = FakeMembershipManagementClient()
        client.phase1Result = .failure(ApiError(httpStatus: 500))
        let (vm, _, _) = makeVM(client: client)

        let outcome = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .failed)
        XCTAssertEqual(vm.submitState, .idle)
    }

    // MARK: Fail-closed (Gate-SEC R7)

    func testFailClosedStartSubscribeUnreachableUnderEmptyKey() async {
        let (vm, _, client) = makeVM(cardAvailable: false)

        let outcome = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .failed)
        XCTAssertTrue(
            client.subscribeCalls.isEmpty,
            "no SetupIntent must be requested when card payment is unavailable"
        )
    }

    func testCtaHiddenWhenCardUnavailable() {
        let (vm, _, _) = makeVM(cardAvailable: false)
        XCTAssertFalse(vm.canSubscribe)
    }

    func testCtaShownWhenCardAvailable() {
        let (vm, _, _) = makeVM(cardAvailable: true)
        XCTAssertTrue(vm.canSubscribe)
    }

    // MARK: Phase 2 — idempotency replay (Gate-SEC R8) + webhook-authority (R5/R9)

    func testConfirmSubscribeReplaysTheSameIdempotencyTokenAcrossBothPhases() async {
        let (vm, _, client) = makeVM()

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        let outcome = await vm.confirmSubscribe(planCode: "plus_monthly")

        guard case let .subscribed(membershipId) = outcome else {
            return XCTFail("expected subscribed, got \(outcome)")
        }
        XCTAssertEqual(membershipId, "mem-99")
        XCTAssertEqual(client.subscribeCalls.count, 2)
        XCTAssertEqual(client.subscribeCalls[0].confirmed, false)
        XCTAssertEqual(client.subscribeCalls[1].confirmed, true)
        XCTAssertEqual(
            client.subscribeCalls[0].token,
            client.subscribeCalls[1].token,
            "Phase-1 and Phase-2 must replay ONE idempotency token to collapse double-taps"
        )
        XCTAssertFalse(client.subscribeCalls[0].token.isEmpty)
    }

    func testFreshSubscribeAttemptMintsANewToken() async {
        let (vm, _, client) = makeVM()

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        _ = await vm.startSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(client.subscribeCalls.count, 2)
        XCTAssertNotEqual(
            client.subscribeCalls[0].token,
            client.subscribeCalls[1].token,
            "a new logical subscribe attempt must mint a fresh token"
        )
    }

    func testConfirmSubscribeRereadsMembershipAfterCompleted() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active)]
        let (vm, repo, _) = makeVM(client: client)
        XCTAssertNil(repo.current)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        _ = await vm.confirmSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(client.mineCallCount, 1, "Phase-2 success must re-read membershipGetMine — webhook is authority")
        XCTAssertEqual(repo.current?.hasMembership, true)
    }

    func testConfirmSubscribeFailureReturnsFailedAndNoMutation() async {
        let client = FakeMembershipManagementClient()
        client.phase2Result = .failure(ApiError(httpStatus: 500))
        let (vm, repo, _) = makeVM(client: client)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        let outcome = await vm.confirmSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .failed)
        XCTAssertNil(repo.current, "a failed confirm must never client-mutate membership state")
    }

    func testConfirmSubscribeEmptyMembershipIdIsFailure() async {
        let client = FakeMembershipManagementClient()
        client.phase2Result = .success(SubscriptionSetup(
            membershipId: "", setupIntentClientSecret: "", stripeCustomerId: "c", ephemeralKey: "e"
        ))
        let (vm, _, _) = makeVM(client: client)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        let outcome = await vm.confirmSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .failed)
    }

    // MARK: Cancel (period-end, own-membership)

    func testCancelSuccessReturnsEffectiveDate() async {
        let (vm, _, client) = makeVM()

        let date = await vm.cancel()

        XCTAssertNotNil(date)
        XCTAssertEqual(client.cancelCallCount, 1)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testCancelFailureReturnsNilAndStaysIdle() async {
        let client = FakeMembershipManagementClient()
        client.cancelResult = .failure(ApiError(httpStatus: 500))
        let (vm, _, _) = makeVM(client: client)

        let date = await vm.cancel()

        XCTAssertNil(date)
        XCTAssertEqual(vm.submitState, .idle)
    }

    // MARK: Swap (instant proration, NO sheet)

    func testSwapPlanSucceedsWithoutPaymentSheet() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active), .success(MembershipFixtures.active)]
        let (vm, _, _) = makeVM(client: client)

        let ok = await vm.swapPlan(newPlanCode: "plus_yearly")

        XCTAssertTrue(ok)
        XCTAssertEqual(client.swapCodes, ["plus_yearly"])
        XCTAssertEqual(client.mineCallCount, 1, "swap re-reads membership; no PaymentSheet is involved")
    }

    func testSwapPlanFailureReturnsFalse() async {
        let client = FakeMembershipManagementClient()
        client.swapResult = .failure(ApiError(httpStatus: 500))
        let (vm, _, _) = makeVM(client: client)

        let ok = await vm.swapPlan(newPlanCode: "plus_yearly")

        XCTAssertFalse(ok)
    }

    // MARK: Binding lifetime

    /// The repository is a session-lived singleton and is deliberately held past the screen:
    /// a binding that retains `self` keeps the view model alive for the life of the process.
    func testTheViewModelIsReleasedWhenTheScreenGoesAway() async {
        let repository = MembershipRepository(client: FakeMembershipManagementClient())
        weak var released: MembershipViewModel?

        func openAndLeaveTheScreen() async {
            let vm = MembershipViewModel(
                repository: repository,
                snackbar: SnackbarController(),
                isCardPaymentAvailable: true
            )
            released = vm
            await vm.load()
            XCTAssertNotNil(released, "the view model was released before the screen was left")
        }

        await openAndLeaveTheScreen()

        XCTAssertNil(released, "the view model outlived its screen")
    }

    /// The bindings replay the repository's current value on subscribe, which is what makes a
    /// separate seed assignment redundant. Both asserted values differ from the property
    /// defaults (`nil`, `[]`), so an unbound view model cannot pass this.
    func testAWarmRepositoryIsVisibleBeforeTheFirstLoad() async {
        let client = FakeMembershipManagementClient()
        client.mineResults = [.success(MembershipFixtures.active)]
        let repository = MembershipRepository(client: client)
        await repository.refresh()
        await repository.refreshPlans()

        let vm = MembershipViewModel(
            repository: repository,
            snackbar: SnackbarController(),
            isCardPaymentAvailable: true
        )

        XCTAssertEqual(vm.current?.planCode, "plus_monthly")
        XCTAssertEqual(vm.plans.map(\.code), ["plus_monthly", "plus_yearly"])
    }

    func testSecretsNeverAppearInSetupIntentPresentationDescription() async {
        let (vm, _, _) = makeVM()
        let outcome = await vm.startSubscribe(planCode: "plus_monthly")
        guard case let .needsPaymentMethod(presentation) = outcome else {
            return XCTFail("expected needsPaymentMethod")
        }
        let rendered = "\(presentation)" + presentation.debugDescription
        XCTAssertFalse(rendered.contains("seti_secret_abc"))
        XCTAssertFalse(rendered.contains("ek_1"))
        XCTAssertFalse(rendered.contains("cus_1"))
    }
}
