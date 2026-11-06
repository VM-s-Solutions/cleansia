import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
extension BookingCodesViewModelTests {
    // MARK: - A valid promo follows the quote's currency

    /// A code validated in one currency says nothing about another: when the address moves the quote
    /// into a new currency, the applied promo is dropped rather than carried as still valid.
    func testAValidPromoIsDroppedWhenTheQuoteMovesToAnotherCurrency() async {
        let promo = FakePromoCodeClient()
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 2400,
            currencyId: "cur-czk",
            currencyCode: "CZK"
        )))
        let scheduler = TestScheduler.dispatch
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: quote,
            extraClient: FakeExtraClient(),
            promoClient: promo,
            referralClient: FakeReferralClient(),
            countryResolver: FakeCountryResolver(),
            scheduler: scheduler.eraseToAnyScheduler(),
            savedCardClient: FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        )
        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState.quote != nil }
        _ = await vm.validatePromoCode("WELCOME20")
        XCTAssertEqual(vm.promoState, .valid(discountAmount: 100))

        quote.result = .success(BookingQuote(totalPrice: 95, currencyId: "cur-eur", currencyCode: "EUR"))
        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState.quote?.currencyCode == "EUR" }

        XCTAssertEqual(vm.quoteState.quote?.currencyCode, "EUR")
        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.state.promoCode, "")
    }

    func testAValidPromoSurvivesAReQuoteInTheSameCurrency() async {
        let promo = FakePromoCodeClient()
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 2400,
            currencyId: "cur-czk",
            currencyCode: "CZK"
        )))
        let scheduler = TestScheduler.dispatch
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: quote,
            extraClient: FakeExtraClient(),
            promoClient: promo,
            referralClient: FakeReferralClient(),
            countryResolver: FakeCountryResolver(),
            scheduler: scheduler.eraseToAnyScheduler(),
            savedCardClient: FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
        )
        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState.quote != nil }
        _ = await vm.validatePromoCode("WELCOME20")

        quote.result = .success(BookingQuote(totalPrice: 2600, currencyId: "cur-czk", currencyCode: "CZK"))
        vm.update { var s = $0
            s.rooms = 3
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await eventually { vm.quoteState.quote?.totalPrice == 2600 }

        XCTAssertEqual(vm.promoState, .valid(discountAmount: 100))
        XCTAssertEqual(vm.state.promoCode, "WELCOME20")
    }

    /// The fake quote answers off the main actor, so a fixed number of yields can finish first, as they
    /// usually do in an optimised build; this waits for the answer itself.
    private func eventually(_ condition: () -> Bool) async {
        for _ in 0 ..< 500 {
            if condition() { return }
            await Task.yield()
        }
    }
}
