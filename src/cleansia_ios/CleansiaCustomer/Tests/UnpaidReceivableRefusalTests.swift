import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// A customer who owes any company money makes no new booking, by cash or by card, until it is paid (owner ruling
/// 2026-10-06). Every booking entry the server refuses for it offers the way to Payments instead of a snackbar.
@MainActor
final class UnpaidReceivableRefusalTests: XCTestCase {
    private let scheduler = TestScheduler.dispatch

    /// The debt can share its wire code with another whole-booking refusal and arrive joined after it.
    private static let joinedDebt: ApiError = {
        let json = #"{"errors":{"PaymentType":"order.cash_open_bookings_limit_reached","#
            + #""AsyncPredicateValidator":"country.not_serviced; order.unpaid_receivable"}}"#
        return ApiError.fromProblemDetails(httpStatus: 400, body: Data(json.utf8))
    }()

    private static let debt = ApiError(code: "order.unpaid_receivable", httpStatus: 400)
    private static let cashCap = ApiError(code: "order.cash_open_bookings_limit_reached", httpStatus: 400)

    // MARK: - The booking sheet

    private func bookingVM(create: FakeOrderCreateClient, intent: FakePaymentIntentClient) -> BookingViewModel {
        BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1234, currencyId: "cur-czk", currencyCode: "CZK", requiredEmployees: 1
            ))),
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            paymentIntentClient: intent,
            countryResolver: FakeCountryResolver(),
            tokenStore: FakeTokenStore.signedIn(),
            isCardPaymentAvailable: true,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
    }

    private func ready(_ payment: PaymentMethod) -> (BookingState) -> BookingState {
        { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.rooms = 2
            s.street = "Zenklova 6"
            s.city = "Praha"
            s.zipCode = "18000"
            s.countryIsoCode = "cz"
            s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            s.paymentMethod = payment
            return s
        }
    }

    func testABookingRefusedForAnUnpaidAmountAsksToPayWhateverTheTender() async {
        for payment in [PaymentMethod.cash, .card] {
            let intent = FakePaymentIntentClient()
            let vm = bookingVM(create: FakeOrderCreateClient(result: .failure(Self.debt)), intent: intent)
            vm.update(ready(payment))

            let outcome = await vm.submit()

            XCTAssertEqual(outcome, .owesMoney, "\(payment)")
            XCTAssertEqual(intent.callCount, 0, "\(payment): a refused booking opens no card payment")
        }
    }

    func testTheDebtIsFoundAmongTheOtherRefusals() async {
        let vm = bookingVM(create: FakeOrderCreateClient(result: .failure(Self.joinedDebt)), intent: .init())
        vm.update(ready(.cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .owesMoney)
    }

    func testAnyOtherBookingRefusalIsStillShownAsItReads() async {
        let vm = bookingVM(create: FakeOrderCreateClient(result: .failure(Self.cashCap)), intent: .init())
        vm.update(ready(.cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(Self.cashCap))
    }

    // MARK: - The schedule form

    private func scheduleVM(
        client: FakeRecurringBookingClient,
        snackbar: SnackbarController,
        editing: RecurringTemplate? = nil
    ) -> CreateRecurringViewModel {
        CreateRecurringViewModel(
            sourceOrderId: nil,
            editing: editing,
            repository: RecurringBookingRepository(client: client),
            catalogClient: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: FakeOrderClient(),
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(totalPrice: 900, currencyCode: "CZK"))),
            cleanersClient: FakeServingCleanersClient(),
            consentClient: FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy]),
            snackbar: snackbar,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
    }

    private func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setDirtiness(.normal)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        vm.setEarlyPerformanceRequested(true)
    }

    func testANewScheduleRefusedForAnUnpaidAmountOffersTheWayToPayIt() async {
        let client = FakeRecurringBookingClient()
        client.createResult = .failure(Self.joinedDebt)
        let snackbar = SnackbarController()
        let vm = scheduleVM(client: client, snackbar: snackbar)
        await vm.load()
        fillValid(vm)

        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(vm.owesMoney)
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertNil(snackbar.current, "the refusal is answered with the way to pay, not a snackbar")
    }

    func testAnEditRefusedForAnUnpaidAmountOffersTheWayAndADismissalClosesIt() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(Self.debt)
        let vm = scheduleVM(
            client: client,
            snackbar: SnackbarController(),
            editing: RecurringFixtures.template(paymentType: RecurringPaymentType.card)
        )
        await vm.load()

        _ = await vm.submit()
        XCTAssertTrue(vm.owesMoney)

        vm.dismissOwesMoney()
        XCTAssertFalse(vm.owesMoney)
    }

    func testAnyOtherScheduleRefusalStillSnackbarsAndAsksForNoPayment() async {
        let client = FakeRecurringBookingClient()
        client.createResult = .failure(Self.cashCap)
        let snackbar = SnackbarController()
        let vm = scheduleVM(client: client, snackbar: snackbar)
        await vm.load()
        fillValid(vm)

        _ = await vm.submit()

        XCTAssertFalse(vm.owesMoney)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }
}
