import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// With no trial the confirm charges the first invoice, so it can answer a 3-D Secure step or a card
/// refusal instead of a membership, and Plus is shown only once the server has granted one.
@MainActor
final class MembershipFirstInvoiceTests: XCTestCase {
    private let secret = "pi_123_secret_456"

    // swiftlint:disable large_tuple
    private func makeVM(
        _ client: FakeMembershipManagementClient
    ) -> (MembershipViewModel, MembershipRepository, SnackbarController) {
        let repository = MembershipRepository(client: client)
        let snackbar = SnackbarController()
        let vm = MembershipViewModel(repository: repository, snackbar: snackbar, isCardPaymentAvailable: true)
        return (vm, repository, snackbar)
    }

    // swiftlint:enable large_tuple

    private func authenticationStep(_ secret: String) -> ApiResult<SubscriptionSetup> {
        .success(SubscriptionSetup(
            membershipId: "",
            setupIntentClientSecret: "",
            stripeCustomerId: "cus_1",
            ephemeralKey: "",
            paymentIntentClientSecret: secret
        ))
    }

    func testAConfirmThatNeeds3DSecureAsksForItAndGrantsNothing() async {
        let client = FakeMembershipManagementClient()
        client.phase2Result = authenticationStep(secret)
        let (vm, repository, snackbar) = makeVM(client)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        let outcome = await vm.confirmSubscribe(planCode: "plus_monthly")

        XCTAssertEqual(outcome, .needsAuthentication(paymentIntentClientSecret: secret))
        XCTAssertEqual(client.mineCallCount, 0)
        XCTAssertNil(repository.current)
        XCTAssertNil(snackbar.current)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testTheFollowUpResendsTheSameConfirmAndShowsPlusOnlyWhenGranted() async {
        let client = FakeMembershipManagementClient()
        client.phase2Result = authenticationStep(secret)
        client.mineResults = [.success(MembershipFixtures.active)]
        let (vm, repository, _) = makeVM(client)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        _ = await vm.confirmSubscribe(planCode: "plus_monthly")
        client.phase2Result = .success(MembershipFixtures.subscribed)
        let outcome = await vm.confirmAfterAuthentication(planCode: "plus_monthly", authentication: .completed)

        XCTAssertEqual(outcome, .subscribed(membershipId: "mem-99"))
        XCTAssertEqual(client.subscribeCalls.map(\.confirmed), [false, true, true])
        XCTAssertEqual(Set(client.subscribeCalls.map(\.token)).count, 1, "the follow-up replays the confirm's token")
        XCTAssertEqual(repository.current?.hasMembership, true)
    }

    func testTheFollowUpIsSentWhateverTheChallengeReportedAndAStillUnpaidAnswerStops() async {
        for (authentication, message) in [
            (PaymentSheetOutcome.canceled, "error_payment_cancelled"),
            (.failed, "error_payment_failed"),
            (.completed, "error_payment_failed")
        ] {
            let client = FakeMembershipManagementClient()
            client.phase2Result = authenticationStep(secret)
            let (vm, repository, snackbar) = makeVM(client)

            _ = await vm.startSubscribe(planCode: "plus_monthly")
            _ = await vm.confirmSubscribe(planCode: "plus_monthly")
            let outcome = await vm.confirmAfterAuthentication(planCode: "plus_monthly", authentication: authentication)

            XCTAssertEqual(outcome, .failed, "\(authentication)")
            XCTAssertEqual(client.subscribeCalls.count, 3, "one follow-up, never a loop: \(authentication)")
            XCTAssertEqual(snackbar.current?.text, L10n.localized(message), "\(authentication)")
            XCTAssertEqual(client.mineCallCount, 0)
            XCTAssertNil(repository.current)
        }
    }

    /// The subscription webhook can grant the paid membership before the follow-up arrives.
    func testAlreadyActiveOnTheFollowUpIsTheGrantedMembership() async {
        let client = FakeMembershipManagementClient()
        client.phase2Result = authenticationStep(secret)
        client.mineResults = [.success(MembershipFixtures.active)]
        let (vm, repository, snackbar) = makeVM(client)

        _ = await vm.startSubscribe(planCode: "plus_monthly")
        _ = await vm.confirmSubscribe(planCode: "plus_monthly")
        client.phase2Result = .failure(ApiError(code: "membership.already_active", httpStatus: 400))
        let outcome = await vm.confirmAfterAuthentication(planCode: "plus_monthly", authentication: .completed)

        guard case .subscribed = outcome else { return XCTFail("expected subscribed, got \(outcome)") }
        XCTAssertEqual(repository.current?.hasMembership, true)
        XCTAssertNil(snackbar.current)
    }

    func testACardRefusalShowsTheCardsReasonAndGrantsNothing() async {
        for key in [
            "membership.card_declined",
            "membership.card_insufficient_funds",
            "membership.card_authentication_failed"
        ] {
            for followUp in [false, true] {
                let client = FakeMembershipManagementClient()
                let refusal = ApiError(code: key, httpStatus: 400)
                client.phase2Result = followUp ? authenticationStep(secret) : .failure(refusal)
                let (vm, repository, snackbar) = makeVM(client)

                _ = await vm.startSubscribe(planCode: "plus_monthly")
                var outcome = await vm.confirmSubscribe(planCode: "plus_monthly")
                if followUp {
                    client.phase2Result = .failure(refusal)
                    outcome = await vm.confirmAfterAuthentication(planCode: "plus_monthly", authentication: .completed)
                }

                XCTAssertEqual(outcome, .failed, key)
                XCTAssertEqual(snackbar.current?.text, ApiErrorLocalizer().message(for: refusal), key)
                XCTAssertNotEqual(snackbar.current?.text, key, "the raw key must never show")
                XCTAssertEqual(client.mineCallCount, 0, key)
                XCTAssertNil(repository.current, key)
            }
        }
    }

    // MARK: the confirm's answer on the wire

    func testA3DSecureAnswerMapsToItsClientSecretInsteadOfBeingRefusedAsIncomplete() throws {
        let setup = try CreateMembershipSubscriptionResponse(
            membershipId: "",
            setupIntentClientSecret: "",
            stripeCustomerId: "cus_1",
            ephemeralKey: "",
            paymentIntentClientSecret: secret
        ).toDomain()

        XCTAssertEqual(setup.membershipId, "")
        XCTAssertEqual(setup.paymentIntentClientSecret, secret)
    }

    func testAGrantedConfirmCarriesNoClientSecret() throws {
        let setup = try CreateMembershipSubscriptionResponse(
            membershipId: "mem-1",
            setupIntentClientSecret: "",
            stripeCustomerId: "cus_1",
            ephemeralKey: "",
            paymentIntentClientSecret: ""
        ).toDomain()

        XCTAssertEqual(setup.membershipId, "mem-1")
        XCTAssertEqual(setup.paymentIntentClientSecret, "")
    }

    func testAnAnswerWithNeitherAMembershipNorAnyClientSecretIsStillRefused() {
        XCTAssertThrowsError(try CreateMembershipSubscriptionResponse(
            membershipId: "",
            setupIntentClientSecret: "",
            stripeCustomerId: "cus_1",
            ephemeralKey: "",
            paymentIntentClientSecret: ""
        ).toDomain())
    }
}
