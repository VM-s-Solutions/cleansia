import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The consent read judges the texts in force for the default market; ConfirmRecurringOrder judges the visit's
/// own. When they disagree the server refuses the confirm, and that refusal — not another read — shows the tick.
/// A visit first landed by a push is read for consents like one landed by an opening.
@MainActor
extension OrderDetailViewModelTests {
    private static let heldConsents: Set<SignupConsentType> = [.termsOfService, .privacyPolicy]
    private static let termsRefusal = ApiError(code: "consent.terms_not_accepted", httpStatus: 400)
    private static let cashConfirmed = RecurringConfirmation(
        clientSecret: nil,
        stripeCustomerId: nil,
        ephemeralKey: nil
    )

    private func confirmRefused(
        with error: ApiError,
        client: FakeOrderClient = FakeOrderClient()
    ) async -> OrderDetailViewModel {
        client.detailResults = [.success(recurringOccurrence(paymentType: 1))]
        client.confirmRecurringResult = .failure(error)
        let vm = makeVM(client: client, consent: FakeConsentStatusClient(granted: Self.heldConsents))
        await vm.load()
        XCTAssertFalse(vm.asksForTerms)
        XCTAssertTrue(vm.canConfirmRecurring)
        await vm.confirmRecurring()
        return vm
    }

    private func eventually(_ condition: () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() { return }
            try? await Task.sleep(nanoseconds: 1_000_000)
        }
    }

    func testATermsRefusalShowsTheTickAtOnceAndTheTickedRetryAssertsIt() async {
        let client = FakeOrderClient()
        let vm = await confirmRefused(with: Self.termsRefusal, client: client)

        XCTAssertTrue(vm.asksForTerms, "the refusal left the box hidden")
        XCTAssertFalse(vm.canConfirmRecurring, "the confirm reopened without the tick")
        XCTAssertEqual(vm.confirmRecurringState, .idle)

        vm.setTermsAccepted(true)
        client.confirmRecurringResult = .success(Self.cashConfirmed)
        await vm.confirmRecurring()

        XCTAssertEqual(client.confirmRecurringTerms, [nil, true])
    }

    func testARefusalForAnotherReasonLeavesTheConfirmAsItWas() async {
        let vm = await confirmRefused(with: ApiError(code: "order.not_found", httpStatus: 400))

        XCTAssertFalse(vm.asksForTerms)
        XCTAssertTrue(vm.canConfirmRecurring)
    }

    /// The read still says "held" for the default market, so letting it answer again would hide the box the
    /// server just asked for, and the next confirm would be refused the same way.
    func testNoLaterReadHidesTheTickTheServerAskedFor() async {
        let vm = await confirmRefused(with: Self.termsRefusal)

        await vm.load()

        XCTAssertTrue(vm.asksForTerms)
        XCTAssertFalse(vm.canConfirmRecurring)
    }

    func testAPushThatLandsAVisitAwaitingConfirmationAsksForTheTerms() async {
        let consent = FakeConsentStatusClient(granted: [])
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 503)), .success(recurringOccurrence(paymentType: 1))]
        let bus = OrderEventBus()
        let vm = makeVM(client: client, consent: consent, eventBus: bus)
        await vm.load()
        XCTAssertNil(vm.alreadyConsented)

        bus.emit(orderId: "o1")
        await eventually { vm.alreadyConsented != nil }

        XCTAssertEqual(vm.state.loadedValue?.needsConfirmation, true)
        XCTAssertTrue(vm.asksForTerms, "the push left the confirm closed with no box to tick")
        XCTAssertEqual(consent.callCount, 1)
    }

    func testAPushThatLandsAVisitAwaitingConfirmationOpensTheConfirmForAnAccountHoldingBoth() async {
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 503)), .success(recurringOccurrence(paymentType: 2))]
        let bus = OrderEventBus()
        let vm = makeVM(client: client, consent: FakeConsentStatusClient(granted: Self.heldConsents), eventBus: bus)
        await vm.load()
        XCTAssertFalse(vm.canConfirmRecurring)

        bus.emit(orderId: "o1")
        await eventually { vm.alreadyConsented != nil }

        XCTAssertFalse(vm.asksForTerms)
        XCTAssertTrue(vm.canConfirmRecurring, "the push left the confirm closed with no box to tick")
    }
}
