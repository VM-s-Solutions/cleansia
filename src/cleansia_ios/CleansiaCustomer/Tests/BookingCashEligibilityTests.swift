import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

/// Cash is offered only to a signed-in customer whose quote says one cleaner does the booking alone. The
/// wizard disables it with the reason, takes a stale cash choice away without switching it to card, and
/// never sends cash the fresh quote refuses — the server would refuse it anyway, after the customer
/// slid to confirm.
@MainActor
final class BookingCashEligibilityTests: XCTestCase {
    private var cancellables = Set<AnyCancellable>()

    private func makeVM(
        requiredEmployees: Int,
        create: FakeOrderCreateClient = FakeOrderCreateClient(),
        tokenStore: FakeTokenStore = .signedIn(),
        scheduler: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions> = .dispatch
    ) -> (BookingViewModel, FakeQuoteClient) {
        let quote = FakeQuoteClient(result: .success(Self.quote(requiredEmployees: requiredEmployees)))
        let vm = BookingViewModel(
            quoteClient: quote,
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            countryResolver: FakeCountryResolver(),
            tokenStore: tokenStore,
            isCardPaymentAvailable: false,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
        return (vm, quote)
    }

    private static func quote(requiredEmployees: Int) -> BookingQuote {
        BookingQuote(totalPrice: 1234, currencyId: "cur-czk", currencyCode: "CZK", requiredEmployees: requiredEmployees)
    }

    private func readyState(payment: PaymentMethod? = nil) -> (BookingState) -> BookingState {
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

    private func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }

    private func events(of vm: BookingViewModel) -> () -> [BookingEvent] {
        var received: [BookingEvent] = []
        vm.events.sink { received.append($0) }.store(in: &cancellables)
        return { received }
    }

    // MARK: - What the payment step offers

    func testCashIsOfferedOnceAOneCleanerQuoteDescribesTheSelection() async {
        let (vm, _) = makeVM(requiredEmployees: 1)
        vm.update(readyState())

        XCTAssertEqual(vm.cashEligibility, .pending)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod, "cash was taken before any quote said who does the job")

        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.cashEligibility, .available)
        vm.selectPayment(.cash)
        XCTAssertEqual(vm.state.paymentMethod, .cash)
    }

    func testTwoRequiredCleanersMeanCardOnly() async {
        let (vm, _) = makeVM(requiredEmployees: 2)
        vm.update(readyState())
        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.cashEligibility, .needsCard(requiredCleaners: 2))
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
        vm.selectPayment(.card)
        XCTAssertEqual(vm.state.paymentMethod, .card)
    }

    func testAGuestIsNeverOfferedCashEvenForOneCleaner() async {
        let (vm, _) = makeVM(requiredEmployees: 1, tokenStore: .guest)
        vm.update(readyState())
        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.cashEligibility, .needsAccount)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
    }

    /// A quote for an earlier selection says nothing about this one: it reads as pending, which keeps a
    /// cash choice rather than flapping it away on every edit.
    func testAChangedSelectionIsPendingAndKeepsCashUntilItIsQuoted() async {
        let (vm, _) = makeVM(requiredEmployees: 1)
        vm.update(readyState())
        await vm.refreshQuoteForTest()
        vm.selectPayment(.cash)

        vm.update { current in
            var next = current
            next.rooms = 6
            return next
        }

        XCTAssertEqual(vm.cashEligibility, .pending)
        XCTAssertEqual(vm.state.paymentMethod, .cash)
    }

    // MARK: - A stale cash choice is taken away, never switched

    func testAQuoteThatNeedsTwoCleanersTakesCashAwayWithoutChoosingCard() async {
        let (vm, quote) = makeVM(requiredEmployees: 1)
        let received = events(of: vm)
        vm.update(readyState())
        await vm.refreshQuoteForTest()
        vm.selectPayment(.cash)

        quote.result = .success(Self.quote(requiredEmployees: 2))
        vm.update { current in
            var next = current
            next.rooms = 6
            return next
        }
        await vm.refreshQuoteForTest()

        XCTAssertNil(vm.state.paymentMethod, "cash survived a quote that refuses it, or was switched to card")
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(received(), [.cashCleared])
    }

    /// The path the wizard runs on every edit: the debounced re-quote, not the submit's own quote.
    func testAnEditThatNeedsTwoCleanersTakesCashAwayOnTheReQuote() async {
        let scheduler = TestScheduler.dispatch
        let (vm, quote) = makeVM(requiredEmployees: 1, scheduler: scheduler)
        let received = events(of: vm)
        vm.update(readyState())
        scheduler.advance(by: .milliseconds(400))
        await drain()
        XCTAssertEqual(vm.cashEligibility, .available, "the watcher never landed the first quote")
        vm.selectPayment(.cash)
        XCTAssertEqual(vm.state.paymentMethod, .cash)

        quote.result = .success(Self.quote(requiredEmployees: 2))
        vm.update { current in
            var next = current
            next.rooms = 6
            return next
        }
        scheduler.advance(by: .milliseconds(400))
        await drain()

        XCTAssertEqual(quote.requests.last?.rooms, 6)
        XCTAssertNil(vm.state.paymentMethod, "cash survived a re-quote that refuses it, or was switched to card")
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(received(), [.cashCleared])
    }

    func testTheNextChoiceClearsTheNotice() async {
        let (vm, _) = makeVM(requiredEmployees: 2)
        vm.update(readyState(payment: .cash))
        await vm.refreshQuoteForTest()
        XCTAssertTrue(vm.cashCleared)

        vm.selectPayment(.card)

        XCTAssertFalse(vm.cashCleared)
        XCTAssertEqual(vm.state.paymentMethod, .card)
    }

    func testTheCrewNeverTouchesACardChoice() async {
        let (vm, _) = makeVM(requiredEmployees: 3)
        let received = events(of: vm)
        vm.update(readyState(payment: .card))

        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.state.paymentMethod, .card)
        XCTAssertFalse(vm.cashCleared)
        XCTAssertEqual(received(), [])
    }

    // MARK: - Submit sends cash only on a fresh quote that allows it

    func testSubmittingCashForATwoCleanerBookingSendsNothing() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = makeVM(requiredEmployees: 2, create: create)
        let received = events(of: vm)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .paymentMethodCleared)
        XCTAssertTrue(create.commands.isEmpty, "a cash order the rule refuses was sent")
        XCTAssertNil(vm.state.paymentMethod)
        XCTAssertEqual(received(), [.cashCleared], "the customer is told once")
    }

    func testSubmittingCashForAOneCleanerBookingSendsCash() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = makeVM(requiredEmployees: 1, create: create)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
    }

    func testCardIsSentWhateverTheCrew() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = makeVM(requiredEmployees: 4, create: create)
        vm.update(readyState(payment: .card))

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.paymentType, ._2)
    }

    /// No choice is not cash: the command used to default a missing choice to cash.
    func testNoPaymentChoiceIsNeverSentAsCash() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = makeVM(requiredEmployees: 1, create: create)
        vm.update(readyState(payment: nil))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(nil))
        XCTAssertTrue(create.commands.isEmpty)
    }

    /// The server re-decides on create. Its refusal is already a sentence the customer reads, so the
    /// choice is taken away without a second one.
    func testTheServersCashRefusalTakesTheChoiceAwayQuietly() async {
        let refusal = ApiError(code: CashEligibility.refusalCode, httpStatus: 400)
        let (vm, _) = makeVM(requiredEmployees: 1, create: FakeOrderCreateClient(result: .failure(refusal)))
        let received = events(of: vm)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(refusal))
        XCTAssertNil(vm.state.paymentMethod)
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(received(), [])
    }

    // MARK: - The reason under the options

    func testTheReasonNamesTheCrewAndIsSilentWhenCashIsAvailable() {
        XCTAssertNil(L10n.Booking.cashReason(.available))
        let needsCard = L10n.Booking.cashReason(.needsCard(requiredCleaners: 3))
        XCTAssertEqual(needsCard?.contains("3"), true, "the reason does not say how many cleaners")
        XCTAssertNotEqual(needsCard, "booking_cash_needs_card")
        XCTAssertNotEqual(L10n.Booking.cashReason(.pending), "booking_cash_pending")
        XCTAssertNotEqual(L10n.Booking.cashReason(.needsAccount), "booking_cash_needs_account")
    }
}
