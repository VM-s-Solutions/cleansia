import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingCashRefusalRecoveryTests: XCTestCase {
    func testDeferredPostRefusalImmediatelyInvalidatesCardsAndReadsOnce() async {
        let refusal = ApiError(code: "order.cash_requires_saved_card", httpStatus: 400)
        let create = FakeOrderCreateClient(result: .failure(refusal))
        let cards = FakeSavedCardClient(reads: [
            .success([PaymentsFixtures.czkCard]), .success([PaymentsFixtures.czkCard]), .success([])
        ])
        let vm = BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 900, currencyCode: "CZK", requiredEmployees: 1
            ))),
            profileClient: FakeProfileClient(), orderCreateClient: create,
            countryResolver: FakeCountryResolver(), tokenStore: FakeTokenStore.signedIn(), savedCardClient: cards
        )
        vm.update { _ in
            var state = BookingState()
            state.selectedServiceIds = ["s-1"]
            state.street = "Test 6"
            state.city = "Praha"
            state.zipCode = "18000"
            state.countryIsoCode = "cz"
            state.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            state.paymentMethod = .cash
            return state
        }
        await vm.refreshCashCard()
        await vm.refreshQuoteForTest()
        var post: CheckedContinuation<Void, Never>?
        var recovery: CheckedContinuation<Void, Never>?
        create.beforeCreate = { await withCheckedContinuation { post = $0 } }
        cards.beforeRead = {
            if cards.readCount == 3 { await withCheckedContinuation { recovery = $0 } }
        }
        let submit = Task { await vm.submit() }
        await eventually { post != nil }
        XCTAssertEqual(vm.cashEligibility, .available)
        post?.resume()
        await eventually { recovery != nil }
        XCTAssertEqual(vm.cashEligibility, .pending)
        XCTAssertNil(vm.state.paymentMethod)
        vm.selectPayment(.cash)
        XCTAssertNil(vm.state.paymentMethod)
        recovery?.resume()
        let outcome = await submit.value
        await eventually { vm.cashEligibility == .needsSavedCard }
        XCTAssertEqual(outcome, .failed(refusal))
        XCTAssertEqual(vm.cashEligibility, .needsSavedCard)
        XCTAssertEqual(cards.readCount, 3, "the refusal should start exactly one recovery read")
        XCTAssertEqual(create.callCount, 1)
    }

    private func eventually(_ condition: () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() { return }
            await Task.yield()
        }
    }
}
