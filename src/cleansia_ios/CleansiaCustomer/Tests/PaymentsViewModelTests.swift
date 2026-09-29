import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// What the customer owes, paid through a pay link, and the card that guarantees their cash bookings.
@MainActor
final class PaymentsViewModelTests: XCTestCase {
    private var cards: FakeSavedCardClient!
    private var receivables: FakeReceivableClient!
    private var snackbar: SnackbarController!
    private var cancellables = Set<AnyCancellable>()

    override func setUp() {
        super.setUp()
        cards = FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        receivables = FakeReceivableClient(receivablesResult: .success([PaymentsFixtures.receivable()]))
        snackbar = SnackbarController()
    }

    private func makeVM(
        countryId: String? = nil,
        currencyCode: String? = nil,
        pauses: @escaping () -> Void = {}
    ) -> PaymentsViewModel {
        PaymentsViewModel(
            savedCardClient: cards,
            receivableClient: receivables,
            snackbar: snackbar,
            countryId: countryId,
            currencyCode: currencyCode,
            pauseBetweenCardReads: pauses
        )
    }

    func testLoadShowsWhatIsOwedAndTheCard() async {
        let vm = makeVM()

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue, PaymentsSnapshot(
            receivables: [PaymentsFixtures.receivable()],
            cards: [PaymentsFixtures.czkCard]
        ))
    }

    /// Half a page would read as "nothing owed" or "no card" while the server says otherwise.
    func testEitherReadFailingIsAnErrorNotHalfAPage() async {
        receivables.receivablesResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed receivables read rendered a page") }
        XCTAssertNotNil(snackbar.current)

        receivables.receivablesResult = .success([])
        cards.reads = [.failure(ApiError(httpStatus: 500))]
        await vm.load()

        guard case .error = vm.state else { return XCTFail("a failed cards read rendered a page") }
    }

    func testPayingOpensTheCheckoutPageAndReReadsOnReturn() async {
        let vm = makeVM()
        await vm.load()
        var opened: [URL] = []
        vm.payLinks.sink { opened.append($0) }.store(in: &cancellables)

        await vm.pay(PaymentsFixtures.receivable())

        XCTAssertEqual(receivables.payLinkIds, ["rcv-1"])
        XCTAssertEqual(opened, [PaymentsFixtures.checkoutUrl])

        receivables.receivablesResult = .success([])
        await vm.onResumed()
        XCTAssertEqual(vm.state.loadedValue?.receivables, [], "the paid amount is still shown on return")

        receivables.receivablesResult = .success([PaymentsFixtures.receivable(id: "rcv-2")])
        await vm.onResumed()
        XCTAssertEqual(vm.state.loadedValue?.receivables, [], "a return with no pay link opened re-read the page")
    }

    func testAPayLinkTheServerRefusesOpensNothing() async {
        receivables.payLinkResult = .failure(ApiError(code: "receivable.not_open", httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        var opened: [URL] = []
        vm.payLinks.sink { opened.append($0) }.store(in: &cancellables)

        await vm.pay(PaymentsFixtures.receivable())

        XCTAssertEqual(opened, [])
        XCTAssertNotNil(snackbar.current)
        XCTAssertFalse(vm.payState.isSubmitting)
    }

    func testRemovingTheCardTakesItOffThePage() async {
        let vm = makeVM()
        await vm.load()
        var removed: [String] = []
        vm.removed.sink { removed.append($0) }.store(in: &cancellables)

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(cards.removedIds, ["card-czk"])
        XCTAssertEqual(removed, ["card-czk"])
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertEqual(vm.state.loadedValue?.receivables, [PaymentsFixtures.receivable()])
        XCTAssertEqual(vm.removeState, .idle)
    }

    func testARefusedRemovalKeepsTheCardAndSaysSo() async {
        cards.removeResult = .failure(ApiError(code: "saved_card.not_found", httpStatus: 400))
        let vm = makeVM()
        await vm.load()

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(vm.state.loadedValue?.cards, [PaymentsFixtures.czkCard])
        XCTAssertEqual(vm.removeState, .error(L10n.Payments.cardRemoveRetryHint))
        XCTAssertNotNil(snackbar.current)
    }

    // MARK: - Saving a card here

    /// A recurring cash booking never captures a card, so without one saved it is refused and this
    /// screen is the only place to save it.
    func testTheCardIsOfferedOnlyWithoutAUsableOneInTheMarketsCurrency() async {
        let expired = PaymentsFixtures.card(id: "card-old", currencyCode: "CZK", expMonth: 1, expYear: 2020)
        let euro = PaymentsFixtures.card(id: "card-eur", currencyCode: "EUR")

        cards.reads = [.success([expired, euro])]
        let czech = makeVM(currencyCode: "CZK")
        await czech.load()
        XCTAssertTrue(czech.offersCardCapture, "an expired card and a card in another currency guarantee nothing")

        let slovak = makeVM(currencyCode: "EUR")
        await slovak.load()
        XCTAssertFalse(slovak.offersCardCapture)

        let noMarket = makeVM()
        await noMarket.load()
        XCTAssertFalse(noMarket.offersCardCapture, "with no market known any usable card counts")
    }

    func testSavingACardStartsTheCaptureOnlyWithTheConsentTicked() async {
        cards.reads = [.success([])]
        let vm = makeVM(countryId: "country-cz", currencyCode: "CZK")
        await vm.load()
        var sheets: [PaymentSheetPresentation] = []
        vm.cardSetups.sink { sheets.append($0) }.store(in: &cancellables)

        await vm.addCard()

        XCTAssertEqual(cards.captureConsents, [], "a card capture started without the consent")
        XCTAssertEqual(sheets, [])
        XCTAssertFalse(vm.addCardState.isSubmitting)

        vm.setCardConsentAccepted(true)
        await vm.addCard()

        XCTAssertEqual(cards.captureConsents, [true])
        XCTAssertEqual(cards.captureCountryIds, ["country-cz"])
        XCTAssertEqual(sheets, [PaymentSheetPresentation(
            clientSecret: PaymentsFixtures.setup.setupIntentClientSecret,
            ephemeralKey: PaymentsFixtures.setup.ephemeralKey,
            stripeCustomerId: PaymentsFixtures.setup.stripeCustomerId,
            merchantDisplayName: "Cleansia",
            intentKind: .setup
        )])
        XCTAssertTrue(vm.addCardState.isSubmitting)
    }

    func testASavedCardIsShownOnceTheServerListsIt() async {
        let saved = PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")
        cards.reads = [.success([]), .success([]), .success([]), .success([saved])]
        var pauses = 0
        let vm = makeVM(currencyCode: "CZK", pauses: { pauses += 1 })
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        await vm.cardSheetFinished(.completed)

        XCTAssertEqual(vm.state.loadedValue?.cards, [saved])
        XCTAssertEqual(vm.state.loadedValue?.receivables, [PaymentsFixtures.receivable()])
        XCTAssertFalse(vm.offersCardCapture)
        XCTAssertEqual(pauses, 2, "the reads were not spaced")
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAdded)
        XCTAssertFalse(vm.cardConsentAccepted, "the next capture would go out under this one's tick")
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testACardTheServerNeverListsOrAClosedSheetChangesNothing() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        await vm.cardSheetFinished(.completed)

        XCTAssertEqual(cards.readCount, 1 + BookingViewModel.cardCaptureReads)
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAddPending)
        XCTAssertEqual(vm.addCardState, .idle)

        await vm.addCard()
        await vm.cardSheetFinished(.canceled)

        XCTAssertEqual(cards.readCount, 1 + BookingViewModel.cardCaptureReads, "a closed sheet waited for a card")
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAddCancelled)
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testEveryKindTheServerSendsHasItsOwnLabelAndANewOneReadsAsAmountDue() {
        XCTAssertEqual(Receivable.Kind(wireValue: 1), .cancellationFee)
        XCTAssertEqual(Receivable.Kind(wireValue: 2), .lockout)
        XCTAssertEqual(Receivable.Kind(wireValue: 3), .unpaidCash)
        XCTAssertEqual(Receivable.Kind(wireValue: 4), .topUp)
        XCTAssertEqual(Receivable.Kind(wireValue: 9), .other)

        let labels = [Receivable.Kind.cancellationFee, .lockout, .unpaidCash, .topUp, .other].map(L10n.Payments.kind)
        XCTAssertEqual(Set(labels).count, labels.count, "two kinds read the same")
        XCTAssertFalse(labels.contains { $0.hasPrefix("receivable_kind_") }, "a kind shows its raw key")
    }
}
