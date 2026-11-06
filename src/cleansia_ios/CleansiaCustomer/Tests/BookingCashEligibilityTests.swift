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
        scheduler: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions> = .dispatch,
        cards: FakeSavedCardClient = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
    ) async -> (BookingViewModel, FakeQuoteClient) {
        let quote = FakeQuoteClient(result: .success(Self.quote(requiredEmployees: requiredEmployees)))
        let vm = BookingViewModel(
            quoteClient: quote,
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            countryResolver: FakeCountryResolver(),
            tokenStore: tokenStore,
            isCardPaymentAvailable: false,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler(),
            savedCardClient: cards
        )
        await vm.refreshCashCard()
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

    /// A fixed number of yields can end before the re-quote lands; this waits for the outcome itself.
    private func eventually(_ condition: () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() { return }
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
        let (vm, _) = await makeVM(requiredEmployees: 1)
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
        let (vm, _) = await makeVM(requiredEmployees: 2)
        vm.update(readyState())
        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.cashEligibility, .needsCard(requiredCleaners: 2))
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
        vm.selectPayment(.card)
        XCTAssertEqual(vm.state.paymentMethod, .card)
    }

    func testAGuestIsNeverOfferedCashEvenForOneCleaner() async {
        let (vm, _) = await makeVM(requiredEmployees: 1, tokenStore: .guest)
        vm.update(readyState())
        await vm.refreshQuoteForTest()

        XCTAssertEqual(vm.cashEligibility, .needsAccount)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
    }

    /// A quote for an earlier selection says nothing about this one: it reads as pending, which keeps a
    /// cash choice rather than flapping it away on every edit.
    func testAChangedSelectionIsPendingAndKeepsCashUntilItIsQuoted() async {
        let (vm, _) = await makeVM(requiredEmployees: 1)
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
        let (vm, quote) = await makeVM(requiredEmployees: 1)
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
        let (vm, quote) = await makeVM(requiredEmployees: 1, scheduler: scheduler)
        let received = events(of: vm)
        vm.update(readyState())
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.cashEligibility == .available }
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
        await eventually { vm.state.paymentMethod == nil }

        XCTAssertEqual(quote.requests.last?.rooms, 6)
        XCTAssertNil(vm.state.paymentMethod, "cash survived a re-quote that refuses it, or was switched to card")
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(received(), [.cashCleared])
    }

    /// A heavier level lengthens the job, so the crew the server quotes for it can outgrow cash.
    func testALevelThatNeedsTwoCleanersTakesCashAwayOnTheReQuote() async {
        let scheduler = TestScheduler.dispatch
        let (vm, quote) = await makeVM(requiredEmployees: 1, scheduler: scheduler)
        let received = events(of: vm)
        vm.update(readyState())
        vm.setDirtiness(.normal)
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.cashEligibility == .available }
        vm.selectPayment(.cash)
        XCTAssertEqual(vm.state.paymentMethod, .cash)

        quote.result = .success(Self.quote(requiredEmployees: 2))
        vm.setDirtiness(.heavy)
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.state.paymentMethod == nil }

        XCTAssertEqual(quote.requests.last?.dirtiness, .heavy)
        XCTAssertNil(vm.state.paymentMethod, "cash survived a level the server crews with two cleaners")
        XCTAssertEqual(received(), [.cashCleared])
    }

    func testTheNextChoiceClearsTheNotice() async {
        let (vm, _) = await makeVM(requiredEmployees: 2)
        vm.update(readyState(payment: .cash))
        await vm.refreshQuoteForTest()
        XCTAssertTrue(vm.cashCleared)

        vm.selectPayment(.card)

        XCTAssertFalse(vm.cashCleared)
        XCTAssertEqual(vm.state.paymentMethod, .card)
    }

    func testTheCrewNeverTouchesACardChoice() async {
        let (vm, _) = await makeVM(requiredEmployees: 3)
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
        let (vm, _) = await makeVM(requiredEmployees: 2, create: create)
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
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
    }

    /// A previously verified guarantee card does not require a second card-saving consent tick.
    func testCashWithASavedGuaranteeNeedsNoNewCardSavingTick() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create)
        vm.update(readyState(payment: .cash))
        vm.update { current in
            var next = current
            next.earlyPerformanceRequested = true
            return next
        }
        await vm.refreshQuoteForTest()

        XCTAssertTrue(BookingStepGate.canContinue(step: 4, state: vm.state, alreadyConsented: true))
        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(create.commands.count, 1)
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
    }

    func testCardIsSentWhateverTheCrew() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = await makeVM(requiredEmployees: 4, create: create)
        vm.update(readyState(payment: .card))

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.paymentType, ._2)
    }

    /// No choice is not cash: the command used to default a missing choice to cash.
    func testNoPaymentChoiceIsNeverSentAsCash() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create)
        vm.update(readyState(payment: nil))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(nil))
        XCTAssertTrue(create.commands.isEmpty)
    }

    /// The server re-decides on create. Its refusal is already a sentence the customer reads, so the
    /// choice is taken away without a second one.
    func testTheServersCashRefusalTakesTheChoiceAwayQuietly() async {
        let refusal = ApiError(code: CashEligibility.refusalCode, httpStatus: 400)
        let (vm, _) = await makeVM(requiredEmployees: 1, create: FakeOrderCreateClient(result: .failure(refusal)))
        let received = events(of: vm)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(refusal))
        XCTAssertNil(vm.state.paymentMethod)
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(received(), [])
    }

    func testNoSavedCardKeepsCashUnavailableAndSendsNoOrder() async {
        let create = FakeOrderCreateClient()
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create, cards: FakeSavedCardClient())
        vm.update(readyState())
        await vm.refreshQuoteForTest()
        XCTAssertEqual(vm.cashEligibility, .needsSavedCard)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
        vm.update(readyState(payment: .cash))
        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .paymentMethodCleared)
        XCTAssertTrue(create.commands.isEmpty)
    }

    func testExpiredOrOtherCurrencyCardsDoNotGuaranteeCash() async {
        for card in [
            PaymentsFixtures.card(id: "expired", currencyCode: "CZK", expYear: 2020),
            PaymentsFixtures.card(id: "eur", currencyCode: "EUR")
        ] {
            let cards = FakeSavedCardClient(reads: [.success([card])])
            let (vm, _) = await makeVM(requiredEmployees: 1, cards: cards)
            vm.update(readyState())
            await vm.refreshQuoteForTest()
            XCTAssertEqual(vm.cashEligibility, .needsSavedCard)
        }
    }

    func testRemovingTheSavedCardAfterTheOptionWasShownPreventsCreate() async {
        let create = FakeOrderCreateClient()
        let cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard]), .success([])])
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create, cards: cards)
        vm.update(readyState())
        await vm.refreshQuoteForTest()
        vm.selectPayment(.cash)
        XCTAssertEqual(vm.state.paymentMethod, .cash)
        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .paymentMethodCleared)
        XCTAssertEqual(cards.readCount, 2)
        XCTAssertTrue(create.commands.isEmpty)
    }

    func testAFailedCardRecheckSendsNothingAndCanBeRetried() async {
        let create = FakeOrderCreateClient()
        let error = ApiError(httpStatus: 503)
        let cards = FakeSavedCardClient(reads: [
            .success([PaymentsFixtures.czkCard]), .failure(error), .success([PaymentsFixtures.czkCard])
        ])
        let (vm, _) = await makeVM(requiredEmployees: 1, create: create, cards: cards)
        vm.update(readyState(payment: .cash))
        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(error))
        XCTAssertTrue(create.commands.isEmpty)
        XCTAssertEqual(vm.cashEligibility, .cardCheckFailed)
        await vm.refreshCashCard()
        XCTAssertEqual(vm.cashEligibility, .available)
    }

    func testAnOlderCardSuccessCannotOverwriteANewerFailedCheck() async {
        let cards = FakeSavedCardClient(reads: [
            .success([PaymentsFixtures.czkCard]), .failure(ApiError(httpStatus: 503))
        ])
        var held: CheckedContinuation<Void, Never>?
        cards.beforeRead = {
            if cards.readCount == 1 { await withCheckedContinuation { held = $0 } }
        }
        let vm = BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(Self.quote(requiredEmployees: 1))),
            tokenStore: FakeTokenStore.signedIn(),
            savedCardClient: cards
        )
        vm.update(readyState())
        await vm.refreshQuoteForTest()
        let older = Task { await vm.refreshCashCard() }
        await eventually { held != nil }
        await vm.refreshCashCard()
        held?.resume()
        _ = await older.value
        XCTAssertEqual(vm.cashEligibility, .cardCheckFailed)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
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
