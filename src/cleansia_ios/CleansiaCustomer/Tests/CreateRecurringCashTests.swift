import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// A schedule is always an account's, so its crew alone decides whether it may be paid in cash — the
/// crew the server quotes for the form as it is now. A new schedule starts on card, a stale cash choice
/// is taken away rather than switched, and cash is sent only on a fresh quote that allows it.
@MainActor
final class CreateRecurringCashTests: XCTestCase {
    private let scheduler = TestScheduler.dispatch

    private func makeVM(
        requiredEmployees: Int,
        editing: RecurringTemplate? = nil,
        recurringClient: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        snackbar: SnackbarController = SnackbarController(),
        quoteClient: QuoteClient? = nil
    ) -> (CreateRecurringViewModel, FakeQuoteClient) {
        let quote = FakeQuoteClient(result: .success(Self.quote(requiredEmployees: requiredEmployees)))
        let vm = CreateRecurringViewModel(
            sourceOrderId: nil,
            editing: editing,
            repository: RecurringBookingRepository(client: recurringClient),
            catalogClient: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: FakeOrderClient(),
            quoteClient: quoteClient ?? quote,
            snackbar: snackbar,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
        return (vm, quote)
    }

    private static func quote(requiredEmployees: Int) -> BookingQuote {
        BookingQuote(totalPrice: 900, currencyCode: "CZK", requiredEmployees: requiredEmployees)
    }

    private static let cashSchedule = RecurringFixtures.template(paymentType: RecurringPaymentType.cash)

    private func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
    }

    private func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }

    /// Past the quote debounce, and the quote it asked for landed.
    private func settle() async {
        scheduler.advance(by: .milliseconds(400))
        await drain()
    }

    // MARK: - What the form offers

    func testANewScheduleStartsOnCard() {
        let (vm, _) = makeVM(requiredEmployees: 1)

        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.card)
    }

    func testCashIsOfferedOnceAOneCleanerQuoteDescribesTheSchedule() async {
        let (vm, _) = makeVM(requiredEmployees: 1)
        await vm.load()
        fillValid(vm)

        XCTAssertEqual(vm.cashEligibility, .pending)
        vm.setPaymentType(RecurringPaymentType.cash)
        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.card)

        await settle()

        XCTAssertEqual(vm.cashEligibility, .available)
        vm.setPaymentType(RecurringPaymentType.cash)
        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.cash)
    }

    func testTwoRequiredCleanersMeanCardOnly() async {
        let (vm, _) = makeVM(requiredEmployees: 2)
        await vm.load()
        fillValid(vm)
        await settle()

        XCTAssertEqual(vm.cashEligibility, .needsCard(requiredCleaners: 2))
        vm.setPaymentType(RecurringPaymentType.cash)
        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.card)
    }

    /// A burst of stepper taps is one question: every quote draws on the same per-account allowance the
    /// save itself needs.
    func testRapidEditsAskForOneQuote() async {
        let (vm, quote) = makeVM(requiredEmployees: 1)
        await vm.load()
        fillValid(vm)
        await settle()
        let quoted = quote.callCount

        vm.setRooms(3)
        vm.setBathrooms(2)
        vm.setRooms(5)
        await drain()
        XCTAssertEqual(quote.callCount, quoted, "the form quoted before the edits settled")

        await settle()
        XCTAssertEqual(quote.callCount, quoted + 1)
        XCTAssertEqual(quote.requests.last?.rooms, 5)
        XCTAssertEqual(quote.requests.last?.bathrooms, 2)
    }

    /// A crew quoted for an earlier selection says nothing about this one.
    func testAChangedSelectionIsPendingUntilItIsQuoted() async {
        let (vm, _) = makeVM(requiredEmployees: 1)
        await vm.load()
        fillValid(vm)
        await settle()
        XCTAssertEqual(vm.cashEligibility, .available)

        vm.setRooms(6)

        XCTAssertEqual(vm.cashEligibility, .pending, "a crew quoted for another selection decided the payment step")
        vm.setPaymentType(RecurringPaymentType.cash)
        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.card)
    }

    /// The day, the time and the way to pay move no money, so they ask for no quote.
    func testOnlyThePricedSelectionIsQuoted() async {
        let (vm, quote) = makeVM(requiredEmployees: 1)
        await vm.load()
        fillValid(vm)
        await settle()
        let quoted = quote.callCount

        vm.setDayOfWeek(1)
        vm.setTimeOfDay("08:30")
        vm.setPaymentType(RecurringPaymentType.cash)
        await settle()
        XCTAssertEqual(quote.callCount, quoted)

        vm.setRooms(5)
        await settle()
        XCTAssertEqual(quote.callCount, quoted + 1)
        XCTAssertEqual(quote.requests.last?.rooms, 5)
        XCTAssertEqual(quote.requests.last?.extraSlugs, [])
        XCTAssertNil(quote.requests.last?.cleaningDate)
    }

    // MARK: - A stale cash choice is taken away, never switched

    func testEditingACashScheduleThatNowNeedsTwoCleanersTakesCashAway() async {
        let snackbar = SnackbarController()
        let client = FakeRecurringBookingClient()
        let (vm, _) = makeVM(
            requiredEmployees: 2,
            editing: Self.cashSchedule,
            recurringClient: client,
            snackbar: snackbar
        )

        await vm.load()
        await settle()

        XCTAssertNil(vm.formState.paymentType, "cash survived a quote that refuses it, or was switched to card")
        XCTAssertTrue(vm.cashCleared)
        XCTAssertEqual(snackbar.current?.text, L10n.Recurring.cashCleared)
        XCTAssertFalse(vm.isValid)
        let saved = await vm.submit()
        XCTAssertFalse(saved)
        XCTAssertTrue(client.updateInputs.isEmpty)
    }

    func testChoosingCardAfterwardsSavesTheScheduleOnCard() async {
        let client = FakeRecurringBookingClient()
        let (vm, _) = makeVM(requiredEmployees: 2, editing: Self.cashSchedule, recurringClient: client)
        await vm.load()
        await settle()

        vm.setPaymentType(RecurringPaymentType.card)
        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertFalse(vm.cashCleared)
        XCTAssertEqual(client.updateInputs.first?.paymentType, RecurringPaymentType.card)
    }

    // MARK: - Submit sends cash only on a fresh quote that allows it

    func testCashIsSentOnAFreshOneCleanerQuote() async {
        let client = FakeRecurringBookingClient()
        let (vm, quote) = makeVM(requiredEmployees: 1, editing: Self.cashSchedule, recurringClient: client)
        await vm.load()
        await settle()
        let quoted = quote.callCount

        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertEqual(quote.callCount, quoted + 1, "cash went out on a quote the form already had")
        XCTAssertEqual(client.updateInputs.first?.paymentType, RecurringPaymentType.cash)
    }

    /// The debounce can start a requote while the save's own quote is in flight. That requote may take
    /// over the form's crew, but the save is decided by the answer it waited for.
    func testASaveIsDecidedByItsOwnQuoteWhenAnEditRequotesMeanwhile() async {
        let client = FakeRecurringBookingClient()
        let gate = GatedQuoteClient(result: .success(Self.quote(requiredEmployees: 1)))
        let (vm, _) = makeVM(
            requiredEmployees: 1,
            editing: Self.cashSchedule,
            recurringClient: client,
            quoteClient: gate
        )
        await vm.load()
        scheduler.advance(by: .milliseconds(400))
        await drain()
        gate.releaseAll()
        await drain()
        XCTAssertEqual(vm.cashEligibility, .available)

        vm.setRooms(3)
        let saving = Task { await vm.submit() }
        await drain()
        scheduler.advance(by: .milliseconds(400))
        await drain()
        gate.releaseAll()
        let saved = await saving.value

        XCTAssertTrue(saved, "a requote the save did not wait for overruled its own one-cleaner quote")
        XCTAssertEqual(client.updateInputs.first?.paymentType, RecurringPaymentType.cash)
    }

    func testCashIsNotSentWhenTheFreshQuoteNeedsTwoCleaners() async {
        let client = FakeRecurringBookingClient()
        let (vm, quote) = makeVM(requiredEmployees: 1, editing: Self.cashSchedule, recurringClient: client)
        await vm.load()
        await settle()
        XCTAssertEqual(vm.cashEligibility, .available)

        quote.result = .success(Self.quote(requiredEmployees: 2))
        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(client.updateInputs.isEmpty, "a cash schedule the rule refuses was sent")
        XCTAssertNil(vm.formState.paymentType)
        XCTAssertEqual(vm.submitState, .idle)
    }

    /// A crew nobody could confirm is not a refusal: the choice is kept, nothing is sent, and the
    /// customer is told to try again or choose card.
    func testAnUnconfirmedCrewHoldsCashAndSaysSo() async {
        let snackbar = SnackbarController()
        let client = FakeRecurringBookingClient()
        let (vm, quote) = makeVM(
            requiredEmployees: 1,
            editing: Self.cashSchedule,
            recurringClient: client,
            snackbar: snackbar
        )
        await vm.load()
        await settle()

        quote.result = .failure(ApiError(code: "network.unreachable"))
        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(client.updateInputs.isEmpty)
        XCTAssertEqual(vm.formState.paymentType, RecurringPaymentType.cash)
        XCTAssertEqual(snackbar.current?.text, L10n.Recurring.cashUnchecked)
    }

    func testCardIsSentWithoutAskingForAQuote() async {
        let client = FakeRecurringBookingClient()
        let (vm, quote) = makeVM(requiredEmployees: 3, recurringClient: client)
        await vm.load()
        fillValid(vm)
        await settle()
        let quoted = quote.callCount

        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertEqual(quote.callCount, quoted)
        XCTAssertEqual(client.createInputs.first?.paymentType, RecurringPaymentType.card)
    }

    /// The server's refusal is already a sentence the customer reads, so the choice is taken away
    /// without a second one.
    func testTheServersCashRefusalTakesTheChoiceAway() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(ApiError(code: CashEligibility.refusalCode, httpStatus: 400))
        let (vm, _) = makeVM(requiredEmployees: 1, editing: Self.cashSchedule, recurringClient: client)
        await vm.load()
        await settle()

        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertNil(vm.formState.paymentType)
        XCTAssertTrue(vm.cashCleared)
    }

    func testTheReasonNamesTheCrewAndNeverTheAccount() {
        XCTAssertNil(L10n.Recurring.cashReason(.available))
        XCTAssertNil(L10n.Recurring.cashReason(.needsAccount))
        XCTAssertEqual(L10n.Recurring.cashReason(.needsCard(requiredCleaners: 2))?.contains("2"), true)
        XCTAssertNotEqual(L10n.Recurring.cashReason(.pending), "recurring_cash_pending")
    }
}

final class RecurringStatusBadgeTests: XCTestCase {
    func testALiveScheduleCarriesNoBadge() {
        XCTAssertNil(RecurringStatusBadge.of(RecurringFixtures.template()))
    }

    func testAScheduleThatNeedsAPaymentChangeSaysSo() {
        XCTAssertEqual(
            RecurringStatusBadge.of(RecurringFixtures.template(requiresPaymentMethodChange: true)),
            .needsPaymentChange
        )
    }

    func testPausedWinsOverEverythingElse() {
        XCTAssertEqual(
            RecurringStatusBadge.of(RecurringFixtures.template(isActive: false, requiresPaymentMethodChange: true)),
            .paused
        )
    }
}
