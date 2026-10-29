import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// What the customer owes, paid through a pay link, and the cards they saved.
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
        cardPaymentAvailable: Bool = true,
        pendingCards: SavedCardCaptureStore? = nil,
        pauses: @escaping () -> Void = {}
    ) -> PaymentsViewModel {
        PaymentsViewModel(
            savedCardClient: cards,
            receivableClient: receivables,
            snackbar: snackbar,
            countryId: countryId,
            currencyCode: currencyCode,
            isCardPaymentAvailable: cardPaymentAvailable,
            pendingCards: pendingCards ?? SavedCardCaptureStore(),
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

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(cards.removedIds, ["card-czk"])
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertEqual(vm.state.loadedValue?.receivables, [PaymentsFixtures.receivable()])
        XCTAssertEqual(vm.removeState, .idle)
    }

    /// The system confirm has already closed, so the refusal is the snackbar's and the row is free again.
    func testARefusedRemovalKeepsTheCardAndSaysSo() async {
        cards.removeResult = .failure(ApiError(code: "saved_card.not_found", httpStatus: 400))
        let vm = makeVM()
        await vm.load()

        await vm.remove(PaymentsFixtures.czkCard)

        XCTAssertEqual(vm.state.loadedValue?.cards, [PaymentsFixtures.czkCard])
        XCTAssertEqual(vm.removeState, .idle)
        XCTAssertNotNil(snackbar.current)
    }

    // MARK: - Saving a card here

    func testTheCardIsOfferedOnlyWithoutAUsableOneInTheMarketsCurrency() async {
        let expired = PaymentsFixtures.card(id: "card-old", currencyCode: "CZK", expMonth: 1, expYear: 2020)
        let euro = PaymentsFixtures.card(id: "card-eur", currencyCode: "EUR")

        cards.reads = [.success([expired, euro])]
        let czech = makeVM(currencyCode: "CZK")
        await czech.load()
        XCTAssertTrue(czech.offersCardCapture, "an expired card and a card in another currency are not usable")

        let slovak = makeVM(currencyCode: "EUR")
        await slovak.load()
        XCTAssertFalse(slovak.offersCardCapture)

        let noMarket = makeVM()
        await noMarket.load()
        XCTAssertFalse(noMarket.offersCardCapture, "with no market known any usable card counts")
    }

    /// A build with no Stripe key has no sheet to save a card through, so it offers no capture and starts none: the
    /// server would mint a SetupIntent and a pending card that nothing in the build can finish.
    func testABuildWithNoStripeKeyOffersNoCardAndStartsNoCapture() async {
        cards.reads = [.success([])]
        let vm = makeVM(countryId: "country-cz", currencyCode: "CZK", cardPaymentAvailable: false)
        await vm.load()
        var sheets: [PaymentSheetPresentation] = []
        vm.cardSetups.sink { sheets.append($0.presentation) }.store(in: &cancellables)

        XCTAssertFalse(vm.offersCardCapture, "a card is offered with no way to save it")
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        XCTAssertEqual(cards.captureConsents, [], "a card capture started with no way to finish it")
        XCTAssertEqual(sheets, [])
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testSavingACardStartsTheCaptureOnlyWithTheConsentTicked() async {
        cards.reads = [.success([])]
        let vm = makeVM(countryId: "country-cz", currencyCode: "CZK")
        await vm.load()
        var sheets: [PaymentSheetPresentation] = []
        vm.cardSetups.sink { sheets.append($0.presentation) }.store(in: &cancellables)

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

        await finish(vm, .completed)

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

        await finish(vm, .completed)

        XCTAssertEqual(cards.readCount, 1 + PaymentsViewModel.cardReads)
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertNotNil(vm.cardCapture)
        XCTAssertEqual(vm.addCardState, .idle)

        vm.restartCardSetup()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        await finish(vm, .canceled)

        XCTAssertEqual(cards.readCount, 1 + PaymentsViewModel.cardReads, "a closed sheet waited for a card")
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAddCancelled)
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testPendingVerificationDoesNotMintAnotherSetupIntent() async {
        cards.reads = [.success([])]
        let vm = makeVM(countryId: "country-cz", currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        await finish(vm, .completed)

        await vm.addCard()

        XCTAssertEqual(cards.captureConsents, [true], "an unconfirmed capture must be explicitly restarted")
    }

    func testReturningToPaymentsFinishesAPreviouslyPendingCard() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        await finish(vm, .completed)
        let saved = PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")
        cards.reads = [.success([saved])]

        await vm.onResumed()

        XCTAssertEqual(vm.state.loadedValue?.cards, [saved])
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAdded)
        XCTAssertFalse(vm.cardConsentAccepted)
    }
}

extension PaymentsViewModelTests {
    func testPendingIdentitySurvivesRouteReentryWithoutAnotherCapture() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let first = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        await first.load()
        first.setCardConsentAccepted(true)
        await first.addCard()
        await finish(first, .completed)

        let returned = makeVM(countryId: "cz", currencyCode: "czk", pendingCards: store)
        await returned.load()
        XCTAssertNotNil(returned.cardCapture)
        await returned.addCard()
        XCTAssertEqual(cards.captureConsents, [true])
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        await returned.checkSavedCard()

        XCTAssertNil(returned.cardCapture)
        XCTAssertEqual(returned.state.loadedValue?.cards.first?.id, PaymentsFixtures.setup.savedCardId)
    }

    func testAnotherMarketDoesNotShowOrVerifyThePendingCapture() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let czech = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        await czech.load()
        czech.setCardConsentAccepted(true)
        await czech.addCard()
        await finish(czech, .completed)

        let slovak = makeVM(countryId: "sk", currencyCode: "EUR", pendingCards: store)
        await slovak.load()
        let reads = cards.readCount
        await slovak.onResumed()
        await czech.onResumed()

        XCTAssertNil(slovak.cardCapture)
        XCTAssertEqual(cards.readCount, reads, "neither a foreign scope nor its stale view may poll")
        let returned = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        XCTAssertNotNil(returned.cardCapture)
    }

    func testRestartRequiresFreshConsentAndAnOldCallbackCannotFinishTheNewCapture() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        guard let oldAttempt = vm.cardCapture?.attempt else { return XCTFail("missing initial capture") }
        vm.restartCardSetup()
        XCTAssertFalse(vm.cardConsentAccepted)
        await vm.addCard()
        XCTAssertEqual(cards.captureConsents, [true])

        let setup = SavedCardSetup(
            savedCardId: "new-capture", setupIntentClientSecret: "setup-test",
            stripeCustomerId: "customer-test", ephemeralKey: "ephemeral-test"
        )
        cards.captureResult = .success(setup)
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        let current = vm.cardCapture?.attempt

        await vm.cardSheetFinished(.completed, attempt: oldAttempt)

        XCTAssertEqual(vm.cardCapture?.attempt, current)
        XCTAssertEqual(cards.readCount, 1, "a stale sheet callback must not poll for the new capture")
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        await finish(vm, .completed)
        XCTAssertNotNil(vm.cardCapture, "the old card appearing does not confirm the restarted setup")
        XCTAssertTrue(vm.cardConsentAccepted)
        cards.reads = [.success([PaymentsFixtures.card(id: "new-capture", currencyCode: "CZK")])]
        await vm.checkSavedCard()
        XCTAssertNil(vm.cardCapture)
        XCTAssertFalse(vm.cardConsentAccepted)
    }

    func testSessionWipeCancelsPollingAndRejectsItsLateResult() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let vm = makeVM(currencyCode: "CZK", pendingCards: store)
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        let started = expectation(description: "verification started")
        var resume: CheckedContinuation<Void, Never>?
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        cards.beforeRead = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        let verification = Task { await self.finish(vm, .completed) }
        await fulfillment(of: [started], timeout: 2)

        await store.clear()
        resume?.resume()
        await verification.value

        XCTAssertNil(vm.cardCapture)
        XCTAssertNil(vm.state.loadedValue, "a late former-session result must not expose a card")
        XCTAssertFalse(vm.cardConsentAccepted)
        XCTAssertNotEqual(snackbar.current?.text, L10n.Payments.cardAdded)
    }

    func testSessionWipeRejectsLateSetupCreationAndItsPresentation() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let vm = makeVM(pendingCards: store)
        await vm.load()
        vm.setCardConsentAccepted(true)
        let started = expectation(description: "capture started")
        var resume: CheckedContinuation<Void, Never>?
        cards.beforeCapture = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        var sheets = 0
        vm.cardSetups.sink { _ in sheets += 1 }.store(in: &cancellables)
        let capture = Task { await vm.addCard() }
        await fulfillment(of: [started], timeout: 2)
        await store.clear()
        resume?.resume()
        await capture.value

        XCTAssertEqual(sheets, 0)
        XCTAssertNil(vm.cardCapture)
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testVerificationNetworkFailureRetainsPendingAndCanBeRetried() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        cards.reads = [.failure(ApiError(code: "test.offline", httpStatus: 503))]
        await finish(vm, .completed)

        XCTAssertNotNil(vm.cardCapture)
        XCTAssertEqual(vm.state.loadedValue?.cards, [])
        XCTAssertNotEqual(snackbar.current?.text, L10n.Payments.cardAdded)
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        await vm.checkSavedCard()
        XCTAssertNil(vm.cardCapture)
        XCTAssertEqual(vm.state.loadedValue?.cards.first?.id, PaymentsFixtures.setup.savedCardId)
    }

    func testCancelledAndFailedSheetsRetainCardsAndAllowAnotherConsentedAttempt() async {
        for outcome in [PaymentSheetOutcome.canceled, .failed] {
            cards.reads = [.success([])]
            let vm = makeVM(currencyCode: "CZK")
            await vm.load()
            vm.setCardConsentAccepted(true)
            await vm.addCard()
            let reads = cards.readCount
            await finish(vm, outcome)

            XCTAssertNil(vm.cardCapture)
            XCTAssertEqual(cards.readCount, reads)
            XCTAssertEqual(vm.state.loadedValue?.cards, [])
            XCTAssertTrue(vm.cardConsentAccepted)
            XCTAssertEqual(vm.addCardState, .idle)
        }
    }

    func testDuplicateCaptureTapAndSheetPresentationAreIgnored() async {
        cards.reads = [.success([])]
        let vm = makeVM()
        await vm.load()
        vm.setCardConsentAccepted(true)
        let started = expectation(description: "capture started")
        var resume: CheckedContinuation<Void, Never>?
        cards.beforeCapture = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        var presentations = 0
        vm.cardSetups.sink { request in
            if vm.claimCardSheet(request.attempt) { presentations += 1 }
        }.store(in: &cancellables)
        let capture = Task { await vm.addCard() }
        await fulfillment(of: [started], timeout: 2)
        await vm.addCard()
        XCTAssertEqual(cards.captureConsents, [true])
        resume?.resume()
        await capture.value
        vm.continueCardSetup()
        XCTAssertEqual(presentations, 1)
    }

    func testAnExactCardIdInAnotherCurrencyCannotConfirmThisCapture() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "EUR")])]
        await finish(vm, .completed)
        XCTAssertNotNil(vm.cardCapture)
        XCTAssertTrue(vm.cardConsentAccepted)
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "czk")])]
        await vm.checkSavedCard()
        XCTAssertNil(vm.cardCapture)
    }

    func testMarketSwitchCancelsVerificationButPreservesItsIdentityForReturn() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let czech = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        await czech.load()
        czech.setCardConsentAccepted(true)
        await czech.addCard()
        let attempt = czech.cardCapture?.attempt
        let started = expectation(description: "verification started")
        var resume: CheckedContinuation<Void, Never>?
        cards.beforeRead = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        let verification = Task { await self.finish(czech, .completed) }
        await fulfillment(of: [started], timeout: 2)
        let slovak = makeVM(countryId: "sk", currencyCode: "EUR", pendingCards: store)
        resume?.resume()
        await verification.value
        XCTAssertNil(slovak.cardCapture)
        XCTAssertNotEqual(snackbar.current?.text, L10n.Payments.cardAdded)
        let returned = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        XCTAssertEqual(returned.cardCapture?.attempt, attempt)
    }

    func testSetupFailureIsActionableAndDoesNotKeepAPendingCapture() async {
        cards.reads = [.success([])]
        cards.captureResult = .failure(ApiError(code: "test.capture_refused", httpStatus: 400))
        let vm = makeVM()
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        XCTAssertNil(vm.cardCapture)
        XCTAssertEqual(vm.addCardState, .idle)
        XCTAssertNotNil(snackbar.current)
        XCTAssertTrue(vm.cardConsentAccepted)
    }

    func testConcurrentVerificationRequestsShareOneBoundedReadCycle() async {
        cards.reads = [.success([])]
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()
        await finish(vm, .completed)
        let previousReads = cards.readCount
        let started = expectation(description: "verification started")
        var resume: CheckedContinuation<Void, Never>?
        cards.reads = [.success([PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")])]
        cards.beforeRead = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        let first = Task { await vm.checkSavedCard() }
        await fulfillment(of: [started], timeout: 2)
        let second = Task { await vm.checkSavedCard() }
        await Task.yield()
        resume?.resume()
        await first.value
        await second.value
        XCTAssertEqual(cards.readCount, previousReads + 1)
        XCTAssertNil(vm.cardCapture)
    }

    func testLeavingBeforeSetupReturnsKeepsAnUnpresentedSheetAvailableOnReentry() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let first = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        await first.load()
        first.setCardConsentAccepted(true)
        let started = expectation(description: "setup requested")
        var resume: CheckedContinuation<Void, Never>?
        cards.beforeCapture = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        let capture = Task { await first.addCard() }
        await fulfillment(of: [started], timeout: 2)
        resume?.resume()
        await capture.value

        let returned = makeVM(countryId: "cz", currencyCode: "CZK", pendingCards: store)
        await returned.load()
        var presentations = 0
        returned.cardSetups.sink { request in
            if returned.claimCardSheet(request.attempt) { presentations += 1 }
        }.store(in: &cancellables)
        returned.continueCardSetup()
        returned.continueCardSetup()

        XCTAssertEqual(presentations, 1)
        XCTAssertEqual(cards.captureConsents, [true], "reentry must use the original SetupIntent")
        await finish(returned, .canceled)
        XCTAssertNil(returned.cardCapture)
    }

    func testARequestDeliveredBeforeWipeCannotBeClaimedForPresentationAfterWipe() async {
        cards.reads = [.success([])]
        let store = SavedCardCaptureStore()
        let vm = makeVM(pendingCards: store)
        await vm.load()
        vm.setCardConsentAccepted(true)
        var request: SavedCardSheetRequest?
        vm.cardSetups.sink { request = $0 }.store(in: &cancellables)
        await vm.addCard()
        guard let request else { return XCTFail("missing delivered request") }
        await store.clear()

        XCTAssertFalse(vm.claimCardSheet(request.attempt))
        XCTAssertNil(vm.cardCapture)
    }
}
