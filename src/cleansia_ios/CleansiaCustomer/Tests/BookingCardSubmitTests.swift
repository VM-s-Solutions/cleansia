import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingCardSubmitTests: XCTestCase {
    private func makeVM(
        create: FakeOrderCreateClient = FakeOrderCreateClient(
            result: .success(CreatedOrder(id: "o-card", confirmationCode: "CLN-C"))
        ),
        paymentIntent: FakePaymentIntentClient = FakePaymentIntentClient(),
        tokenStore: FakeTokenStore = .signedIn(),
        cardAvailable: Bool = true
    ) -> BookingViewModel {
        BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1234, currencyId: "cur-czk", currencyCode: "CZK"
            ))),
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            paymentIntentClient: paymentIntent,
            countryResolver: FakeCountryResolver(),
            tokenStore: tokenStore,
            isCardPaymentAvailable: cardAvailable,
            quoteDebounce: .milliseconds(400),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private func cardReadyState(_ state: BookingState) -> BookingState {
        var s = state
        s.selectedServiceIds = ["s-1"]
        s.rooms = 2
        s.bathrooms = 1
        s.street = "Zenklova 6"
        s.city = "Praha"
        s.zipCode = "18000"
        s.countryIsoCode = "cz"
        s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
        s.paymentMethod = .card
        return s
    }

    func testCardSubmitCreatesOrderThenPaymentIntent() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-card", confirmationCode: "CLN-C")))
        let intent = FakePaymentIntentClient()
        let vm = makeVM(create: create, paymentIntent: intent)
        vm.update(cardReadyState)

        let outcome = await vm.submit()

        XCTAssertEqual(create.commands.first?.paymentType, ._2)
        XCTAssertEqual(create.callCount, 1)
        XCTAssertEqual(intent.callCount, 1)
        XCTAssertEqual(intent.orderIds, ["o-card"])
        guard case let .cardPending(orderId, confirmationCode, presentation) = outcome else {
            return XCTFail("expected cardPending, got \(outcome)")
        }
        XCTAssertEqual(orderId, "o-card")
        XCTAssertEqual(confirmationCode, "CLN-C")
        XCTAssertEqual(presentation.clientSecret, "pi_secret_123")
        XCTAssertEqual(presentation.merchantDisplayName, "Cleansia")
    }

    func testUntickedCardPaymentAsksStripeToKeepNothing() async {
        let intent = FakePaymentIntentClient()
        let vm = makeVM(paymentIntent: intent)
        vm.update(cardReadyState)

        _ = await vm.submit()

        XCTAssertEqual(intent.saveCards, [false])
    }

    func testTickedCardPaymentAsksStripeToKeepTheCard() async {
        let intent = FakePaymentIntentClient()
        let vm = makeVM(paymentIntent: intent)
        vm.update(cardReadyState)
        vm.setSaveCard(true)

        _ = await vm.submit()

        XCTAssertEqual(intent.saveCards, [true])
    }

    func testUntickedCardPaymentPresentsTheSheetWithoutTheCustomer() async {
        let vm = makeVM()
        vm.update(cardReadyState)

        let outcome = await vm.submit()

        guard case let .cardPending(_, _, presentation) = outcome else {
            return XCTFail("expected cardPending, got \(outcome)")
        }
        XCTAssertEqual(presentation.clientSecret, "pi_secret_123")
        XCTAssertTrue(presentation.stripeCustomerId.isEmpty)
        XCTAssertTrue(presentation.ephemeralKey.isEmpty)
    }

    func testTickedCardPaymentPresentsTheSheetWithTheCustomer() async {
        let vm = makeVM()
        vm.update(cardReadyState)
        vm.setSaveCard(true)

        let outcome = await vm.submit()

        guard case let .cardPending(_, _, presentation) = outcome else {
            return XCTFail("expected cardPending, got \(outcome)")
        }
        XCTAssertEqual(presentation.stripeCustomerId, "cus_789")
        XCTAssertEqual(presentation.ephemeralKey, "ek_secret_456")
    }

    func testCardSavingIsOfferedOnlyWhilePayingByCard() {
        let vm = makeVM()
        XCTAssertFalse(vm.offersCardSaving)

        vm.update(cardReadyState)
        XCTAssertTrue(vm.offersCardSaving)

        vm.update { state in
            var s = state
            s.paymentMethod = .cash
            return s
        }
        XCTAssertFalse(vm.offersCardSaving)
    }

    func testGuestIsNeverOfferedCardSaving() {
        let vm = makeVM(tokenStore: .guest)
        vm.update(cardReadyState)

        XCTAssertFalse(vm.offersCardSaving)
    }

    func testCardSavingIsNotOfferedWhenCardPaymentIsUnavailable() {
        let vm = makeVM(cardAvailable: false)
        vm.update(cardReadyState)

        XCTAssertFalse(vm.offersCardSaving)
    }

    func testSaveCardTickStartsOffAndIsNotCarriedIntoTheNextBooking() {
        let vm = makeVM()
        XCTAssertFalse(vm.state.saveCard)

        vm.setSaveCard(true)
        vm.reset()

        XCTAssertFalse(vm.state.saveCard)
    }

    /// The order is already created at this point, so the customer needs to know
    /// exactly why the card step died — the intent error is the only clue they get.
    func testPaymentIntentFailureLeavesCardPendingUnreached() async {
        let serverError = ApiError(code: "payment.intent_failed", httpStatus: 502)
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-card", confirmationCode: "CLN-C")))
        let intent = FakePaymentIntentClient(result: .failure(serverError))
        let vm = makeVM(create: create, paymentIntent: intent)
        vm.update(cardReadyState)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(serverError))
        XCTAssertEqual(create.callCount, 1)
        XCTAssertEqual(intent.callCount, 1)
    }

    /// A 200 with an empty client secret is a local sanity check, not a server
    /// rejection — there is no ApiError to show, so the generic message stands.
    func testEmptyClientSecretFails() async {
        let intent = FakePaymentIntentClient(result: .success(PaymentIntentDetails(
            clientSecret: "", ephemeralKey: "ek", stripeCustomerId: "cus"
        )))
        let vm = makeVM(paymentIntent: intent)
        vm.update(cardReadyState)

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(nil))
    }

    func testCardUnavailableNeverCallsPaymentIntent() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-cash", confirmationCode: "CLN-X")))
        let intent = FakePaymentIntentClient()
        let vm = makeVM(create: create, paymentIntent: intent, cardAvailable: false)
        vm.update(cardReadyState)

        let outcome = await vm.submit()

        XCTAssertEqual(intent.callCount, 0)
        XCTAssertEqual(outcome, .success(orderId: "o-cash", confirmationCode: "CLN-X"))
    }

    func testCashSubmitNeverCallsPaymentIntent() async {
        let intent = FakePaymentIntentClient()
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-cash", confirmationCode: "CLN-Z")))
        let vm = makeVM(create: create, paymentIntent: intent)
        vm.update { state in
            var s = self.cardReadyState(state)
            s.paymentMethod = .cash
            return s
        }

        let outcome = await vm.submit()

        XCTAssertEqual(intent.callCount, 0)
        XCTAssertEqual(outcome, .success(orderId: "o-cash", confirmationCode: "CLN-Z"))
    }

    func testIsCardPaymentAvailableReflectsInjectedFlag() {
        XCTAssertTrue(makeVM(cardAvailable: true).isCardPaymentAvailable)
        XCTAssertFalse(makeVM(cardAvailable: false).isCardPaymentAvailable)
    }

    func testFailClosedSubmitNeverReachesPaymentSheet() async {
        let intent = FakePaymentIntentClient()
        let presenter = FakePaymentSheetPresenter()
        let vm = makeVM(paymentIntent: intent, cardAvailable: false)
        vm.update(cardReadyState)

        let outcome = await vm.submit()
        if case .cardPending = outcome {
            XCTFail("unconfigured card must not yield cardPending — the sheet is never presented")
        }
        XCTAssertEqual(intent.callCount, 0)
        XCTAssertTrue(presenter.presentations.isEmpty)
    }

    func testEmptyKeyResolvesToUnavailableCardPayment() {
        let vm = makeVM(cardAvailable: StripeConfig.isConfigured(publishableKey: ""))
        XCTAssertFalse(vm.isCardPaymentAvailable)
    }

    func testDefaultCardAvailabilityIsDerivedFromStripeConfig() {
        let vm = BookingViewModel(
            quoteClient: FakeQuoteClient(),
            profileClient: FakeProfileClient(),
            orderCreateClient: FakeOrderCreateClient(),
            paymentIntentClient: FakePaymentIntentClient(),
            countryResolver: FakeCountryResolver(),
            tokenStore: FakeTokenStore.signedIn(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
        XCTAssertEqual(vm.isCardPaymentAvailable, StripeConfig.isCardPaymentAvailable)
    }

    /// A 3-D Secure approval in the bank app, or a Face ID prompt, comes back to the foreground under Stripe's
    /// sheet. A booking being sent or paid for is not re-checked then: clearing its time would send an order
    /// already placed back to the When step.
    func testABookingBeingSentOrPaidForIsNotReCheckedOnTheWayBack() throws {
        let features = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
        let shell = try String(
            contentsOf: features.appendingPathComponent("Shell/CustomerShellView+Booking.swift"),
            encoding: .utf8
        )
        let recheck = try XCTUnwrap(
            shell.range(of: "func recheckOpenBooking() {").map { String(shell[$0.upperBound...].prefix(200)) },
            "recheckOpenBooking() not found"
        )
        XCTAssertTrue(
            recheck
                .contains("guard !bookingVM.submitState.isSubmitting, !bookingVM.paymentSheetShowing else { return }"),
            "a return to the foreground re-checks a booking being sent or paid for"
        )

        let sheet = try String(
            contentsOf: features.appendingPathComponent("Booking/BookingSheetView.swift"),
            encoding: .utf8
        ).replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
        XCTAssertEqual(
            sheet.components(separatedBy: "paymentSheet.present(").count - 1,
            1,
            "Stripe's sheet is shown somewhere that does not hold the re-check"
        )
        XCTAssertEqual(
            sheet.components(separatedBy: "await showPaymentSheet(presentation)").count - 1,
            1,
            "the booking pays through showPaymentSheet in one place"
        )
        XCTAssertTrue(
            sheet.contains(
                "vm.paymentSheetShowing = true defer { vm.paymentSheetShowing = false } " +
                    "return await paymentSheet.present(presentation)"
            ),
            "Stripe's sheet no longer holds the re-check while it is up"
        )
    }
}
