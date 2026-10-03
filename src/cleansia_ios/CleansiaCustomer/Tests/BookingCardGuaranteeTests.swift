import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// A signed-in customer's first cash booking saves a card first: PaymentSheet in setup mode, under the
/// consent the review step asks for, and the order goes out only once the server holds the card. A
/// usable card in the booking's currency lets cash go straight out; card bookings never ask.
@MainActor
final class BookingCardGuaranteeTests: XCTestCase {
    private func makeVM(
        cards: FakeSavedCardClient,
        create: FakeOrderCreateClient = FakeOrderCreateClient(),
        pauses: @escaping () -> Void = {}
    ) -> BookingViewModel {
        BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1234,
                currencyId: "cur-czk",
                currencyCode: "CZK",
                requiredEmployees: 1
            ))),
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            countryResolver: FakeCountryResolver(),
            savedCardClient: cards,
            pauseBetweenCardReads: pauses,
            tokenStore: FakeTokenStore.signedIn(),
            isCardPaymentAvailable: false,
            quoteDebounce: .milliseconds(400),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private func readyState(payment: PaymentMethod? = .cash, consent: Bool = true) -> (BookingState) -> BookingState {
        { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.street = "Zenklova 6"
            s.city = "Praha"
            s.zipCode = "18000"
            s.countryIsoCode = "cz"
            s.countryId = "country-cz"
            s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            s.paymentMethod = payment
            s.cardGuaranteeAccepted = consent
            s.earlyPerformanceRequested = true
            return s
        }
    }

    // MARK: - What the review step asks

    func testCashWithNoCardInTheBookingsCurrencyAsksForTheConsent() async {
        let vm = makeVM(cards: FakeSavedCardClient(reads: [.success([
            PaymentsFixtures.card(id: "card-eur", currencyCode: "EUR"),
            PaymentsFixtures.card(id: "card-expired", currencyCode: "CZK", expMonth: 1, expYear: 2020)
        ])]))
        vm.update(readyState(consent: false))
        await vm.refreshQuoteForTest()
        await vm.refreshSavedCards()

        XCTAssertTrue(vm.needsCardGuarantee)
        XCTAssertFalse(BookingStepGate.canContinue(
            step: 4, state: vm.state, alreadyConsented: true, needsCardGuarantee: vm.needsCardGuarantee
        ))
        vm.setCardGuaranteeAccepted(true)
        XCTAssertTrue(BookingStepGate.canContinue(
            step: 4, state: vm.state, alreadyConsented: true, needsCardGuarantee: vm.needsCardGuarantee
        ))
    }

    func testAUsableCardCardBookingsAndAnUnreadListAskNothing() async {
        let holding = makeVM(cards: .holdingCzkCard())
        holding.update(readyState(consent: false))
        await holding.refreshQuoteForTest()
        await holding.refreshSavedCards()
        XCTAssertFalse(holding.needsCardGuarantee)

        let card = makeVM(cards: FakeSavedCardClient())
        card.update(readyState(payment: .card, consent: false))
        await card.refreshQuoteForTest()
        await card.refreshSavedCards()
        XCTAssertFalse(card.needsCardGuarantee)

        let unread = makeVM(cards: FakeSavedCardClient())
        unread.update(readyState(consent: false))
        await unread.refreshQuoteForTest()
        XCTAssertFalse(unread.needsCardGuarantee, "a list never read is no reason to ask")
    }

    func testChoosingCashReadsTheCards() async {
        let cards = FakeSavedCardClient()
        let vm = makeVM(cards: cards)
        vm.update(readyState(payment: nil))
        await vm.refreshQuoteForTest()

        vm.selectPayment(.cash)
        // The fake reads off the main actor, so wait for the read itself rather than a fixed number of yields.
        for _ in 0 ..< 500 where vm.savedCards == nil {
            await Task.yield()
        }

        XCTAssertEqual(cards.readCount, 1)
        XCTAssertEqual(vm.savedCards, [])
    }

    // MARK: - Submit

    func testAFirstCashBookingCapturesTheCardBeforeAnyOrderIsSent() async {
        let cards = FakeSavedCardClient()
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .cardGuaranteeNeeded(PaymentSheetPresentation(
            clientSecret: "seti_secret_123",
            ephemeralKey: "ek_secret_456",
            stripeCustomerId: "cus_789",
            merchantDisplayName: "Cleansia",
            intentKind: .setup
        )))
        XCTAssertEqual(cards.captureConsents, [true])
        XCTAssertEqual(cards.captureCountryIds, ["country-cz"])
        XCTAssertTrue(create.commands.isEmpty, "a cash order went out before its card was saved")
    }

    func testWithoutTheConsentNoCardIsAskedForAndNothingIsSent() async {
        let cards = FakeSavedCardClient()
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState(consent: false))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .cardGuaranteeConsentRequired)
        XCTAssertTrue(cards.captureConsents.isEmpty)
        XCTAssertTrue(create.commands.isEmpty)
    }

    /// The review step's list may be stale; the submit asks the server again.
    func testACardTheServerNowHoldsLetsCashGoStraightOut() async {
        let cards = FakeSavedCardClient(reads: [.success([]), .success([PaymentsFixtures.czkCard])])
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState(consent: false))
        await vm.refreshSavedCards()

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
        XCTAssertTrue(cards.captureConsents.isEmpty)
    }

    func testAFailedCardReadSendsNothing() async {
        let refusal = ApiError(code: "network.unreachable")
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: FakeSavedCardClient(reads: [.failure(refusal)]), create: create)
        vm.update(readyState())

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(refusal))
        XCTAssertTrue(create.commands.isEmpty)
    }

    func testACardBookingNeverReadsTheCards() async {
        let cards = FakeSavedCardClient()
        let vm = makeVM(cards: cards)
        vm.update(readyState(payment: .card, consent: false))

        _ = await vm.submit()

        XCTAssertEqual(cards.readCount, 0)
        XCTAssertTrue(cards.captureConsents.isEmpty)
    }

    // MARK: - After PaymentSheet saved the card

    func testTheBookingGoesOutOnceTheServerHoldsTheCard() async {
        let cards = FakeSavedCardClient(reads: [
            .success([]),
            .success([]),
            .success([]),
            .success([PaymentsFixtures.czkCard])
        ])
        let create = FakeOrderCreateClient()
        var pauses = 0
        let vm = makeVM(cards: cards, create: create, pauses: { pauses += 1 })
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }

        let outcome = await vm.submitAfterCardGuarantee()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(create.commands.count, 1)
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
        XCTAssertEqual(pauses, 2, "the reads were not spaced")
        XCTAssertEqual(cards.captureConsents.count, 1, "a second card was captured")
    }

    func testACardTheServerNeverConfirmsBooksNothing() async {
        let cards = FakeSavedCardClient()
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())
        _ = await vm.submit()

        let outcome = await vm.submitAfterCardGuarantee()

        XCTAssertEqual(outcome, .cardGuaranteePending)
        XCTAssertTrue(create.commands.isEmpty)
        XCTAssertEqual(cards.readCount, 1 + BookingViewModel.cardCaptureReads)
        XCTAssertFalse(vm.submitState.isSubmitting)
    }

    /// Saving the card can take a while; a time that stopped holding meanwhile books nothing and waits for no
    /// card — the customer picks a time again first.
    func testATimeThatStoppedHoldingWhileTheCardWasSavedBooksNothing() async throws {
        let cards = FakeSavedCardClient()
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }
        let readsBefore = cards.readCount
        let calendar = Calendar.current
        let yesterday = try XCTUnwrap(calendar.date(byAdding: .day, value: -1, to: calendar.startOfDay(for: Date())))
        vm.selectDay(yesterday)
        vm.selectTime("10:00", on: yesterday)

        let outcome = await vm.submitAfterCardGuarantee()

        XCTAssertEqual(outcome, .timeNoLongerHolds)
        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertTrue(create.commands.isEmpty)
        XCTAssertEqual(cards.readCount, readsBefore, "it waited for the card")
        XCTAssertFalse(vm.submitState.isSubmitting)
    }

    /// A time already cleared — a re-check ran while the card was being saved — is refused the same way: no card
    /// is waited for and nothing is sent, and the customer is told the time changed, not that the connection
    /// failed.
    func testATimeClearedWhileTheCardWasSavedBooksNothing() async throws {
        let cards = FakeSavedCardClient()
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }
        let readsBefore = cards.readCount
        let calendar = Calendar.current
        let yesterday = try XCTUnwrap(calendar.date(byAdding: .day, value: -1, to: calendar.startOfDay(for: Date())))
        vm.selectDay(yesterday)
        vm.selectTime("10:00", on: yesterday)
        XCTAssertTrue(vm.revalidateResumedTime(), "the time was not cleared")

        let outcome = await vm.submitAfterCardGuarantee()

        XCTAssertEqual(outcome, .timeNoLongerHolds)
        XCTAssertEqual(cards.readCount, readsBefore, "it waited for the card")
        XCTAssertTrue(create.commands.isEmpty)
        XCTAssertFalse(vm.submitState.isSubmitting)
    }

    /// "Still saving your card" asks the customer to slide again. That slide waits for the card PaymentSheet
    /// already saved; it never opens a second capture because the first has not reached the server yet
    /// (Android's `submit`).
    func testASlideAfterTheCardWasStillSavingWaitsForThatCardRatherThanCapturingASecond() async {
        let notYet = [ApiResult<[SavedCard]>](repeating: .success([]), count: 1 + BookingViewModel.cardCaptureReads + 1)
        let cards = FakeSavedCardClient(reads: notYet + [.success([PaymentsFixtures.czkCard])])
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }
        let stillSaving = await vm.submitAfterCardGuarantee()
        XCTAssertEqual(stillSaving, .cardGuaranteePending)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(cards.captureConsents.count, 1, "a second card was captured")
        XCTAssertEqual(create.commands.count, 1)
    }

    /// A time that stopped holding while the card was saved sends the customer back to When; the slide after
    /// they pick again books on the card already saved.
    func testASlideAfterATimeThatStoppedHoldingWaitsForTheCardAlreadySaved() async throws {
        let cards = FakeSavedCardClient(reads: [.success([]), .success([]), .success([PaymentsFixtures.czkCard])])
        let create = FakeOrderCreateClient()
        let vm = makeVM(cards: cards, create: create)
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        let yesterday = try XCTUnwrap(calendar.date(byAdding: .day, value: -1, to: today))
        vm.selectDay(yesterday)
        vm.selectTime("10:00", on: yesterday)
        let timeGone = await vm.submitAfterCardGuarantee()
        XCTAssertEqual(timeGone, .timeNoLongerHolds)
        let later = try XCTUnwrap(calendar.date(byAdding: .day, value: 3, to: today))
        vm.selectDay(later)
        vm.selectTime("10:00", on: later)

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "order-1", confirmationCode: "CLN-001"))
        XCTAssertEqual(cards.captureConsents.count, 1, "a second card was captured")
    }

    /// A setup sheet the customer cancelled saved nothing: the next slide captures afresh at once instead of
    /// waiting out the reads for a card that is not coming.
    func testAfterTheSetupSheetIsAbandonedTheNextSlideCapturesAfresh() async {
        let cards = FakeSavedCardClient()
        let vm = makeVM(cards: cards)
        vm.update(readyState())
        guard case .cardGuaranteeNeeded = await vm.submit() else { return XCTFail("no capture was asked for") }

        vm.abandonCardGuarantee()
        let outcome = await vm.submit()

        guard case .cardGuaranteeNeeded = outcome else { return XCTFail("no fresh capture: \(outcome)") }
        XCTAssertEqual(cards.captureConsents.count, 2)
        XCTAssertEqual(cards.readCount, 2, "it waited for a card the cancelled sheet never saved")
    }

    func testWithoutACaptureInFlightThereIsNothingToWaitFor() async {
        let cards = FakeSavedCardClient()
        let vm = makeVM(cards: cards)
        vm.update(readyState())

        let outcome = await vm.submitAfterCardGuarantee()

        XCTAssertEqual(outcome, .failed(nil))
        XCTAssertEqual(cards.readCount, 0)
    }

    /// The server stamps every saved card with the version of the consent wording in force, so the text
    /// the review step shows is the one named for that version: bumping the version on the server fails
    /// here until the new wording is in the catalog and is the one rendered.
    func testTheConsentShownIsTheWordingOfTheVersionTheServerRecords() throws {
        let savedCard = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("src/Cleansia.Core.Domain/Users/SavedCard.cs")
        let source = try String(contentsOf: savedCard, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"ConsentTextVersionInForce\s*=\s*"([^"]+)""#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "SavedCard.ConsentTextVersionInForce not found — the parser needs updating"
        )
        let version = try String(source[XCTUnwrap(Range(match.range(at: 1), in: source))])
        let key = "consent_" + version.replacingOccurrences(of: "-", with: "_")

        XCTAssertNotEqual(L10n.localized(key), key, "\(key) is not in the catalog")
        XCTAssertEqual(L10n.Booking.cardGuaranteeConsent, L10n.localized(key))
    }

    // MARK: - The card itself

    /// The server's `SavedCard.IsUsableOn`: good through the last day of its expiry month, in UTC.
    func testACardIsUsableThroughItsExpiryMonth() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let lastDay = try XCTUnwrap(calendar.date(from: DateComponents(year: 2027, month: 8, day: 31, hour: 23)))
        let nextMonth = try XCTUnwrap(calendar.date(from: DateComponents(year: 2027, month: 9, day: 1, hour: 0)))
        let card = PaymentsFixtures.card(id: "c", currencyCode: "CZK", expMonth: 8, expYear: 2027)

        XCTAssertTrue(card.isUsable(on: lastDay))
        XCTAssertFalse(card.isUsable(on: nextMonth))
        XCTAssertEqual(SavedCard.usable(in: [card], currencyCode: "czk", on: lastDay), card)
        XCTAssertNil(SavedCard.usable(in: [card], currencyCode: "EUR", on: lastDay))
    }
}
