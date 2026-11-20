import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
extension PaymentsViewModelTests {
    func testACompletedSheetShowsTheCardTheServerConfirmsAheadOfTheCardsItDoesNotRetire() async {
        let expired = PaymentsFixtures.card(id: "card-old", currencyCode: "CZK", expMonth: 1, expYear: 2020)
        let euro = PaymentsFixtures.card(id: "card-eur", currencyCode: "EUR")
        let saved = PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")
        cards.reads = [.success([expired, euro])]
        cards.confirmResult = .success(saved)
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        await finish(vm, .completed)

        XCTAssertEqual(cards.confirmedSetupIntentIds, ["seti_123"])
        XCTAssertEqual(vm.state.loadedValue?.cards, [saved, euro])
        XCTAssertEqual(cards.readCount, 1, "a confirmed card still waited on the list")
        XCTAssertNil(vm.cardCapture)
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAdded)
        XCTAssertFalse(vm.cardConsentAccepted)
        XCTAssertEqual(vm.addCardState, .idle)
    }

    func testARefusedConfirmSaysWhyKeepsTheCardPendingAndTheNextCheckConfirmsAgain() async {
        let refusal = ApiError(code: "saved_card.not_found", httpStatus: 400)
        cards.reads = [.success([])]
        cards.confirmResult = .failure(refusal)
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        await finish(vm, .completed)

        XCTAssertEqual(snackbar.current?.text, ApiErrorLocalizer().message(for: refusal))
        XCTAssertEqual(cards.readCount, 1, "a refused confirm polled the list")
        XCTAssertEqual(vm.cardCapture?.stage, .pending(PaymentsFixtures.setup.savedCardId))
        XCTAssertEqual(vm.addCardState, .idle)

        let saved = PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")
        cards.confirmResult = .success(saved)
        await vm.checkSavedCard()

        XCTAssertEqual(cards.confirmedSetupIntentIds, ["seti_123", "seti_123"], "the check did not confirm again")
        XCTAssertEqual(vm.state.loadedValue?.cards, [saved])
        XCTAssertEqual(cards.readCount, 1)
        XCTAssertNil(vm.cardCapture)
    }

    func testAConfirmThatNeverReachesTheServerFallsBackToReadingTheList() async {
        let saved = PaymentsFixtures.card(id: PaymentsFixtures.setup.savedCardId, currencyCode: "CZK")
        cards.reads = [.success([]), .success([saved])]
        cards.confirmResult = .failure(ApiError(code: "test.offline", httpStatus: -1))
        let vm = makeVM(currencyCode: "CZK")
        await vm.load()
        vm.setCardConsentAccepted(true)
        await vm.addCard()

        await finish(vm, .completed)

        XCTAssertEqual(cards.confirmedSetupIntentIds, ["seti_123"])
        XCTAssertEqual(vm.state.loadedValue?.cards, [saved])
        XCTAssertNil(vm.cardCapture)
        XCTAssertEqual(snackbar.current?.text, L10n.Payments.cardAdded)
    }
}
