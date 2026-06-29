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
            tokenStore: FakeTokenStore.signedIn(),
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
        XCTAssertEqual(presentation.ephemeralKey, "ek_secret_456")
        XCTAssertEqual(presentation.stripeCustomerId, "cus_789")
        XCTAssertEqual(presentation.merchantDisplayName, "Cleansia")
    }

    func testPaymentIntentFailureLeavesCardPendingUnreached() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-card", confirmationCode: "CLN-C")))
        let intent = FakePaymentIntentClient(result: .failure(ApiError(code: "network.unreachable")))
        let vm = makeVM(create: create, paymentIntent: intent)
        vm.update(cardReadyState)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed)
        XCTAssertEqual(create.callCount, 1)
        XCTAssertEqual(intent.callCount, 1)
    }

    func testEmptyClientSecretFails() async {
        let intent = FakePaymentIntentClient(result: .success(PaymentIntentDetails(
            clientSecret: "", ephemeralKey: "ek", stripeCustomerId: "cus"
        )))
        let vm = makeVM(paymentIntent: intent)
        vm.update(cardReadyState)

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed)
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

    func testDefaultCardAvailabilityIsFailClosedUnderEmptyKey() {
        let vm = BookingViewModel(
            quoteClient: FakeQuoteClient(),
            profileClient: FakeProfileClient(),
            orderCreateClient: FakeOrderCreateClient(),
            paymentIntentClient: FakePaymentIntentClient(),
            countryResolver: FakeCountryResolver(),
            tokenStore: FakeTokenStore.signedIn(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
        XCTAssertFalse(vm.isCardPaymentAvailable)
    }
}
