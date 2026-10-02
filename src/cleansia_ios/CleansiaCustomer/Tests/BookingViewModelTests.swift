import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingViewModelTests: XCTestCase {
    var cancellables = Set<AnyCancellable>()

    func makeVM(
        catalog: FakeCatalogClient = FakeCatalogClient(),
        quote: FakeQuoteClient = FakeQuoteClient(),
        country: FakeCountryResolver = FakeCountryResolver(),
        market: MarketStore? = nil,
        scheduler: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions>
    ) -> BookingViewModel {
        BookingViewModel(
            catalogClient: catalog,
            quoteClient: quote,
            countryResolver: country,
            market: market?.statePublisher ?? Just(.unavailable).eraseToAnyPublisher(),
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
    }

    // MARK: The chosen market, before there is an address

    func testTheCatalogueIsPricedForTheChosenMarketBeforeThereIsAnAddress() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.slovak))
        let market = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        let vm = makeVM(catalog: catalog, market: market, scheduler: .dispatch)

        await vm.loadCatalog()

        XCTAssertEqual(catalog.requestedCountryIds, ["svk"])
        XCTAssertEqual(vm.catalogCountryId, "svk")
    }

    func testSwitchingTheMarketReloadsTheCatalogueForIt() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let market = await MarketFixtures.resolved()
        let vm = makeVM(catalog: catalog, market: market, scheduler: .dispatch)
        await vm.loadCatalog()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])

        catalog.result = .success(CatalogFixtures.slovak)
        market.select(isoCode: "SVK")
        await drainQuote()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.displayCurrencyCode, "EUR")
    }

    /// Address > market: the address's country prices the booking, and a market chosen afterwards
    /// leaves the booking alone.
    func testTheAddressOverridesTheMarketAndALaterMarketSwitchLeavesTheBookingAlone() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let market = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        let vm = makeVM(catalog: catalog, market: market, scheduler: .dispatch)
        await vm.loadCatalog()
        XCTAssertEqual(catalog.requestedCountryIds, ["svk"])

        vm.update { var s = $0
            s.countryId = "cze"
            return s
        }
        await drainQuote()
        XCTAssertEqual(catalog.requestedCountryIds, ["svk", "cze"])
        XCTAssertEqual(vm.catalogCountryId, "cze")

        market.select(isoCode: "CZE")
        market.select(isoCode: "SVK")
        await drainQuote()

        XCTAssertEqual(catalog.requestedCountryIds, ["svk", "cze"], "the booking's address decides, not the chip")
    }

    func testTheQuoteCarriesTheChosenMarketUntilAnAddressDecides() async {
        let quote = FakeQuoteClient()
        let market = await MarketFixtures.resolved(selected: MarketFixtures.slovakia)
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, market: market, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(quote.requests.last?.countryId, "svk")

        vm.update { var s = $0
            s.countryId = "cze"
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(quote.requests.last?.countryId, "cze")
    }

    func testWithoutADirectoryTheCatalogueAndTheQuoteCarryNoCountry() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let quote = FakeQuoteClient()
        let market = await MarketFixtures.unavailable()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(catalog: catalog, quote: quote, market: market, scheduler: scheduler)
        await vm.loadCatalog()
        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(catalog.requestedCountryIds, [nil])
        XCTAssertEqual(quote.requests.last?.countryId, nil)
    }

    func testStartsOnStepOne() {
        let vm = BookingViewModel()
        XCTAssertEqual(vm.currentStep, 1)
        XCTAssertTrue(vm.isFirstStep)
        XCTAssertFalse(vm.isLastStep)
    }

    func testAdvanceWalksEveryStepAndStopsAtTheLast() {
        let vm = BookingViewModel()

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 2)

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertFalse(vm.isLastStep)

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 4)
        XCTAssertTrue(vm.isLastStep)

        XCTAssertFalse(vm.advance())
        XCTAssertEqual(vm.currentStep, 4)
    }

    func testBackWalksThreeTwoOneAndStopsAtOne() {
        let vm = BookingViewModel()
        vm.advance()
        vm.advance()
        XCTAssertEqual(vm.currentStep, 3)

        XCTAssertTrue(vm.back())
        XCTAssertEqual(vm.currentStep, 2)

        XCTAssertTrue(vm.back())
        XCTAssertEqual(vm.currentStep, 1)

        XCTAssertFalse(vm.back())
        XCTAssertEqual(vm.currentStep, 1)
    }

    func testBackOnStepOneDoesNotMoveSoTheViewCanClose() {
        let vm = BookingViewModel()
        XCTAssertTrue(vm.isFirstStep)
        XCTAssertFalse(vm.back())
        XCTAssertEqual(vm.currentStep, 1)
    }

    /// The swipe-down dismissal is held exactly while the leading control steps back rather than
    /// closes: past step one the gesture would discard the draft, on step one it is the close.
    func testTheSheetCanOnlyBeSwipedAwayOnTheFirstStep() {
        let vm = BookingViewModel()
        XCTAssertFalse(vm.canStepBack)

        vm.advance()
        XCTAssertTrue(vm.canStepBack)
        vm.advance()
        XCTAssertTrue(vm.canStepBack)

        vm.back()
        XCTAssertTrue(vm.canStepBack)
        vm.back()
        XCTAssertFalse(vm.canStepBack)
    }

    func testResetReleasesTheSwipeToDismissHold() {
        let vm = BookingViewModel()
        vm.advance()
        vm.advance()

        vm.reset()

        XCTAssertFalse(vm.canStepBack)
    }

    func testUpdateRebuildsStateViaCopy() {
        let vm = BookingViewModel()
        vm.update { current in
            var next = current
            next.rooms = 3
            return next
        }
        XCTAssertEqual(vm.state.rooms, 3)
    }

    /// Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`.
    func testTheSizeSettersStopAtTheLargestHomeTheServerAccepts() {
        let vm = BookingViewModel()
        vm.setRooms(PropertySize.maxRooms + 1)
        vm.setBathrooms(PropertySize.maxBathrooms + 1)

        XCTAssertEqual(vm.state.rooms, PropertySize.maxRooms)
        XCTAssertEqual(vm.state.bathrooms, PropertySize.maxBathrooms)
    }

    func testTheSizeSettersNeverGoBelowOne() {
        let vm = BookingViewModel()
        vm.setRooms(0)
        vm.setBathrooms(0)

        XCTAssertEqual(vm.state.rooms, 1)
        XCTAssertEqual(vm.state.bathrooms, 1)
    }

    func testAccessInstructionsAreCappedAtTheBackendLimit() {
        let vm = BookingViewModel()
        vm.setAccessInstructions(String(repeating: "a", count: BookingInstructions.maxUtf16Length + 250))

        XCTAssertEqual(vm.state.accessInstructions.utf16.count, BookingInstructions.maxUtf16Length)
    }

    func testSpecialInstructionsAreCappedAtTheBackendLimit() {
        let vm = BookingViewModel()
        vm.setSpecialInstructions(String(repeating: "a", count: BookingInstructions.maxUtf16Length + 250))

        XCTAssertEqual(vm.state.specialInstructions.utf16.count, BookingInstructions.maxUtf16Length)
    }

    /// The field the customer actually types into must hold the server's
    /// measure, not Swift's: 1500 emoji are under Swift's 2000 and over the
    /// validator's.
    func testAstralInstructionsAreCappedByUtf16Width() {
        let vm = BookingViewModel()
        vm.setAccessInstructions(String(repeating: "😀", count: 1500))

        XCTAssertLessThanOrEqual(
            vm.state.accessInstructions.utf16.count,
            BookingInstructions.maxUtf16Length
        )
        XCTAssertEqual(vm.state.accessInstructions.count, 1000)
    }

    func testInstructionsUnderTheLimitAreStoredVerbatim() {
        let vm = BookingViewModel()
        vm.setAccessInstructions("  Key box, code 4321 ")
        vm.setSpecialInstructions("Eco products only")

        XCTAssertEqual(vm.state.accessInstructions, "  Key box, code 4321 ")
        XCTAssertEqual(vm.state.specialInstructions, "Eco products only")
    }

    func testResetReturnsToStepOneAndCleanState() {
        let vm = BookingViewModel()
        vm.update { current in
            var next = current
            next.selectedServiceIds = ["s-1"]
            next.street = "X"
            return next
        }
        vm.advance()
        vm.advance()

        vm.reset()

        XCTAssertEqual(vm.currentStep, 1)
        XCTAssertEqual(vm.state, BookingState())
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.referralState, .idle)
    }

    func testInitialSealedStatesAreIdle() {
        let vm = BookingViewModel()
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.referralState, .idle)
    }

    func testCatalogStartsLoadingAndBecomesLoaded() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)

        XCTAssertTrue(vm.catalogState.isLoading)
        await vm.loadCatalog()

        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
        XCTAssertEqual(catalog.callCount, 1)
    }

    func testCatalogLoadFailureSurfacesError() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)

        await vm.loadCatalog()

        guard case .error = vm.catalogState else {
            return XCTFail("expected error state")
        }
    }

    func testRetryRefetchesAfterFailure() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()

        catalog.result = .success(CatalogFixtures.populated)
        await vm.retryCatalog()

        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
        XCTAssertEqual(catalog.callCount, 2)
    }

    func testLoadCatalogIsIdempotentOnceLoaded() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()
        await vm.loadCatalog()

        XCTAssertEqual(catalog.callCount, 1)
    }

    func testConcurrentLoadCatalogFetchesOnce() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)

        async let first: Void = vm.loadCatalog()
        async let second: Void = vm.loadCatalog()
        _ = await (first, second)

        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
        XCTAssertEqual(catalog.callCount, 1)
    }

    /// The quote's own currency wins the moment it lands; until then the catalogue's default labels
    /// the catalogue prices, which used to be a `"CZK"` literal.
    func testDisplayCurrencyIsTheCatalogueDefaultBeforeTheFirstQuote() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.catalog(currencyCode: "EUR")))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        XCTAssertNil(vm.displayCurrencyCode)

        await vm.loadCatalog()

        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(vm.displayCurrencyCode, "EUR")
        XCTAssertTrue(BookingPricing.formatTotal(1500, currencyCode: vm.displayCurrencyCode ?? "").hasSuffix(" €"))
    }

    func testTheQuotesOwnCurrencyWinsOverTheCatalogueDefault() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.catalog(currencyCode: "CZK")))
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1000, currencyCode: "EUR")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(catalog: catalog, quote: quote, scheduler: scheduler)
        await vm.loadCatalog()
        XCTAssertEqual(vm.displayCurrencyCode, "CZK")

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(vm.quoteState.quote?.currencyCode, "EUR")
        XCTAssertEqual(vm.displayCurrencyCode, "EUR")
    }

    // MARK: The tier floor hint

    func testTheTierFloorHintShowsOnlyBelowTheFloorWhileNoDiscountWins() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(
            totalPrice: 800,
            currencyCode: "CZK",
            tierDiscountMinOrderAmount: 1000
        )))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)
        XCTAssertNil(vm.unmetTierDiscountFloor)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(vm.unmetTierDiscountFloor, 1000)

        quote.result = .success(BookingQuote(
            totalPrice: 1200,
            currencyCode: "CZK",
            tierDiscountAmount: 60,
            tierDiscountMinOrderAmount: 1000
        ))
        vm.update { var s = $0
            s.rooms = 2
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertNil(vm.unmetTierDiscountFloor)

        quote.result = .success(BookingQuote(totalPrice: 800, currencyCode: "EUR", tierDiscountMinOrderAmount: nil))
        vm.update { var s = $0
            s.rooms = 3
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertNil(vm.unmetTierDiscountFloor)
    }

    func testSelectionMutationsUpdateState() {
        let vm = BookingViewModel()
        vm.update { var s = $0
            s.selectedServiceIds.insert("s-1")
            return s
        }
        vm.update { var s = $0
            s.selectedPackageIds.insert("p-1")
            return s
        }
        vm.update { var s = $0
            s.rooms = 4
            s.bathrooms = 2
            return s
        }

        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1"])
        XCTAssertEqual(vm.state.rooms, 4)
        XCTAssertEqual(vm.state.bathrooms, 2)
    }

    func drainQuote() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }
}
