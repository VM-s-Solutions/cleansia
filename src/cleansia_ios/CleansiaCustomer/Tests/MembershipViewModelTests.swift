import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class MembershipViewModelTests: XCTestCase {
    private func makeVM(
        client: FakeMembershipManagementClient = FakeMembershipManagementClient(),
        cardAvailable: Bool = true
    ) -> (MembershipViewModel, MembershipRepository, FakeMembershipManagementClient) {
        let repo = MembershipRepository(client: client)
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
