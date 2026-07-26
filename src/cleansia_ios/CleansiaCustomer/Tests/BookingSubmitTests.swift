import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingSubmitTests: XCTestCase {
    private func makeVM(
        quote: FakeQuoteClient = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 1234,
            currencyId: "cur-czk",
            currencyCode: "CZK"
        ))),
        profile: FakeProfileClient = FakeProfileClient(),
        create: FakeOrderCreateClient = FakeOrderCreateClient(),
        country: FakeCountryResolver = FakeCountryResolver(),
        tokenStore: FakeTokenStore = .signedIn()
    ) -> BookingViewModel {
        BookingViewModel(
            quoteClient: quote,
            profileClient: profile,
            orderCreateClient: create,
            countryResolver: country,
            tokenStore: tokenStore,
            quoteDebounce: .milliseconds(400),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private func readyState(payment: PaymentMethod = .cash) -> (BookingState) -> BookingState {
        { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.rooms = 2
            s.bathrooms = 1
            s.street = "Zenklova 6"
            s.city = "Praha"
            s.zipCode = "18000"
            s.countryIsoCode = "cz"
            s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            s.paymentMethod = payment
            return s
        }
    }

    func testCashSubmitReturnsSuccess() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-9", confirmationCode: "CLN-9")))
        let vm = makeVM(create: create)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "o-9", confirmationCode: "CLN-9"))
        XCTAssertEqual(create.commands.first?.paymentType, ._1)
        XCTAssertEqual(vm.submitState, .idle)
    }

    /// The duplicate-order guard: the session-lived draft must be wiped AT the
    /// success outcome — a sheet swiped away over the success screen skips the
    /// exit closures, and a stale draft would re-arm slide-to-pay at step 3.
    func testSuccessfulCashSubmitWipesTheDraftForTheNextBooking() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-10", confirmationCode: "CLN-10")))
        let vm = makeVM(create: create)
        vm.update(readyState(payment: .cash))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "o-10", confirmationCode: "CLN-10"))
        XCTAssertEqual(vm.state, BookingState())
        XCTAssertEqual(vm.currentStep, 1)
    }

    func testCardSelectedWhenUnconfiguredCreatesOrderWithoutStripeHop() async {
        let create = FakeOrderCreateClient(result: .success(CreatedOrder(id: "o-card", confirmationCode: "CLN-C")))
        let vm = makeVM(create: create)
        vm.update(readyState(payment: .card))

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .success(orderId: "o-card", confirmationCode: "CLN-C"))
        XCTAssertEqual(create.commands.first?.paymentType, ._2)
    }

    func testGuestWithNoTokenFailsWithoutCreatingOrder() async {
        let create = FakeOrderCreateClient()
        let profile = FakeProfileClient()
        let vm = makeVM(profile: profile, create: create, tokenStore: .guest)
        vm.update(readyState())

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .failed(nil))
        XCTAssertEqual(create.callCount, 0)
        XCTAssertEqual(profile.callCount, 0)
    }

    func testBlankPhoneYieldsProfileIncomplete() async {
        let profile = FakeProfileClient(result: .success(BookingProfile(
            firstName: "Jana", lastName: "Nováková", email: "jana@example.com", phoneNumber: " "
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(profile: profile, create: create)
        vm.update(readyState())

        let outcome = await vm.submit()

        XCTAssertEqual(outcome, .profileIncomplete)
        XCTAssertEqual(create.callCount, 0)
    }

    func testBlankNameYieldsProfileIncomplete() async {
        let profile = FakeProfileClient(result: .success(BookingProfile(
            firstName: "", lastName: "", email: "jana@example.com", phoneNumber: "+420777123456"
        )))
        let vm = makeVM(profile: profile)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .profileIncomplete)
    }

    func testBlankEmailYieldsProfileIncomplete() async {
        let profile = FakeProfileClient(result: .success(BookingProfile(
            firstName: "Jana", lastName: "Nováková", email: " ", phoneNumber: "+420777123456"
        )))
        let vm = makeVM(profile: profile)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .profileIncomplete)
    }

    func testProfileFetchFailureFails() async {
        let profile = FakeProfileClient(result: .failure(ApiError(code: "network.unreachable")))
        let create = FakeOrderCreateClient()
        let vm = makeVM(profile: profile, create: create)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(ApiError(code: "network.unreachable")))
        XCTAssertEqual(create.callCount, 0)
    }

    func testMissingInstantFails() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.street = "Zenklova 6"
            s.paymentMethod = .cash
            s.selectedInstant = nil
            return s
        }

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(nil))
        XCTAssertEqual(create.callCount, 0)
    }

    /// The headline regression: a rejected create used to collapse into one
    /// generic network toast, so "that slot was just taken" / "the price
    /// changed" / "we don't serve that city" all reached the customer as
    /// "something went wrong". The whole `ApiError` — key, server detail and
    /// status — has to reach the sheet for `showApiError` to catalog-match it.
    func testCreateFailurePropagatesTheServerErrorCodeToTheCaller() async {
        let serverError = ApiError(
            code: "order.total_price.not_match",
            message: "The total price does not match the current quote.",
            httpStatus: 400
        )
        let create = FakeOrderCreateClient(result: .failure(serverError))
        let vm = makeVM(create: create)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(serverError))
        XCTAssertEqual(create.callCount, 1)
    }

    /// The other half of the same fix: a business rejection that is not about
    /// price. Pins that nothing in the submit path filters the error by code.
    func testCreateRejectionForAnUnservicedCityReachesTheCaller() async {
        let serverError = ApiError(code: "city.not_serviced", httpStatus: 400)
        let create = FakeOrderCreateClient(result: .failure(serverError))
        let vm = makeVM(create: create)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(serverError))
    }

    func testPriceEchoesQuotedRawTotalVerbatim() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 1899,
            originalSubtotal: 2100,
            currencyId: "cur-czk",
            currencyCode: "CZK",
            tierDiscountAmount: 201
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(quote: quote, create: create)
        vm.update(readyState())

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.totalPrice, 1899)
        XCTAssertEqual(create.commands.first?.currencyId, "cur-czk")
    }

    func testSubmitReusesCachedQuoteWhenInputsUnchanged() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 1500, currencyId: "cur-czk", currencyCode: "CZK"
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(quote: quote, create: create)
        vm.update(readyState())

        await vm.refreshQuoteForTest()
        XCTAssertEqual(quote.callCount, 1)

        _ = await vm.submit()

        XCTAssertEqual(quote.callCount, 1)
        XCTAssertEqual(create.commands.first?.totalPrice, 1500)
    }

    func testSubmitRequotesWhenNoCachedQuote() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 1700, currencyId: "cur-czk", currencyCode: "CZK"
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(quote: quote, create: create)
        vm.update(readyState())

        _ = await vm.submit()

        XCTAssertEqual(quote.callCount, 1)
        XCTAssertEqual(create.commands.first?.totalPrice, 1700)
    }

    /// `resolvedQuote` used to flatten its `ApiResult` to `BookingQuote?`, which
    /// threw the error away one frame before `submit` could return it. It now
    /// hands the whole result back.
    func testQuoteFailureOnSubmitFails() async {
        let serverError = ApiError(code: "order.no_available_spots", httpStatus: 409)
        let quote = FakeQuoteClient(result: .failure(serverError))
        let create = FakeOrderCreateClient()
        let vm = makeVM(quote: quote, create: create)
        vm.update(readyState())

        let outcome = await vm.submit()
        XCTAssertEqual(outcome, .failed(serverError))
        XCTAssertEqual(create.callCount, 0)
    }

    func testResolvedCountryIdIsSentOnInlineAddress() async {
        let country = FakeCountryResolver(resolved: "country-99")
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create, country: country)
        vm.update(readyState())

        _ = await vm.submit()

        XCTAssertEqual(country.requestedIsoCodes, ["cz"])
        XCTAssertEqual(create.commands.first?.customerAddress?.countryId, "country-99")
        XCTAssertNil(create.commands.first?.savedAddressId)
    }

    func testSavedAddressIdSentAsXorWithoutInlineAddress() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.savedAddressId = "addr-7"
            s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            s.paymentMethod = .cash
            return s
        }

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.savedAddressId, "addr-7")
        XCTAssertNil(create.commands.first?.customerAddress)
    }

    func testPromoSentOnlyWhenValidationValid() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update(readyState())
        vm.update { var s = $0
            s.promoCode = "SAVE10"
            return s
        }

        _ = await vm.submit()

        XCTAssertNil(create.commands.first?.promoCode)
    }

    func testCustomerNameAndContactComeFromProfile() async {
        let profile = FakeProfileClient(result: .success(BookingProfile(
            firstName: "Jana", lastName: "Nováková", email: "jana@example.com", phoneNumber: "+420777123456"
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(profile: profile, create: create)
        vm.update(readyState())

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.customerName, "Jana Nováková")
        XCTAssertEqual(create.commands.first?.customerEmail, "jana@example.com")
        XCTAssertEqual(create.commands.first?.customerPhone, "+420777123456")
    }

    func testExtrasSentAsSlugKeyedTrueMap() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update(readyState())
        vm.update { var s = $0
            s.selectedExtraSlugs = ["windows", "inside-oven"]
            return s
        }

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.extras, ["windows": true, "inside-oven": true])
    }

    func testSameCleaningDateSentToQuoteAndCreate() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 1000, currencyId: "cur-czk", currencyCode: "CZK"
        )))
        let create = FakeOrderCreateClient()
        let vm = makeVM(quote: quote, create: create)
        let instant = Date(timeIntervalSince1970: 1_900_000_000)
        vm.update { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.street = "Zenklova 6"
            s.selectedInstant = instant
            s.paymentMethod = .cash
            return s
        }

        _ = await vm.submit()

        XCTAssertEqual(quote.requests.first?.cleaningDate, instant)
        XCTAssertEqual(create.commands.first?.cleaningDate, instant)
    }

    func testDoubleSubmitYieldsSingleCreateCall() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update(readyState())

        async let first = vm.submit()
        async let second = vm.submit()
        let outcomes = await [first, second]

        XCTAssertEqual(create.callCount, 1)
        let successCount = outcomes.filter { if case .success = $0 { return true }
            return false
        }.count
        // The loser is rejected by the local in-flight guard, not by the server,
        // so it must carry no ApiError — otherwise a second tap would toast.
        let failedCount = outcomes.filter { $0 == .failed(nil) }.count
        XCTAssertEqual(successCount, 1)
        XCTAssertEqual(failedCount, 1)
    }
}
