import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingViewModelTests: XCTestCase {
    private func makeVM(
        catalog: FakeCatalogClient = FakeCatalogClient(),
        quote: FakeQuoteClient = FakeQuoteClient(),
        scheduler: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions>
    ) -> BookingViewModel {
        BookingViewModel(
            catalogClient: catalog,
            quoteClient: quote,
            quoteDebounce: .milliseconds(400),
            scheduler: scheduler.eraseToAnyScheduler()
        )
    }

    func testStartsOnStepOne() {
        let vm = BookingViewModel()
        XCTAssertEqual(vm.currentStep, 1)
        XCTAssertTrue(vm.isFirstStep)
        XCTAssertFalse(vm.isLastStep)
    }

    func testAdvanceWalksOneTwoThreeAndStopsAtThree() {
        let vm = BookingViewModel()

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 2)

        XCTAssertTrue(vm.advance())
        XCTAssertEqual(vm.currentStep, 3)
        XCTAssertTrue(vm.isLastStep)

        XCTAssertFalse(vm.advance())
        XCTAssertEqual(vm.currentStep, 3)
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

    func testUpdateRebuildsStateViaCopy() {
        let vm = BookingViewModel()
        vm.update { current in
            var next = current
            next.rooms = 3
            return next
        }
        XCTAssertEqual(vm.state.rooms, 3)
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

    func testQuoteStaysIdleWithNoSelection() async {
        let quote = FakeQuoteClient()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.rooms = 3
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await Task.yield()

        XCTAssertEqual(vm.quoteState, .idle)
        XCTAssertEqual(quote.callCount, 0)
    }

    func testSelectingServiceWalksIdleQuotingQuoted() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1200, currencyCode: "CZK")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        XCTAssertEqual(vm.quoteState, .quoting)

        await drainQuote()

        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1200)
        XCTAssertEqual(quote.callCount, 1)
    }

    func testDebounceCoalescesRapidEditsIntoOneQuote() async {
        let quote = FakeQuoteClient()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(100))
        vm.update { var s = $0
            s.rooms = 2
            return s
        }
        scheduler.advance(by: .milliseconds(100))
        vm.update { var s = $0
            s.rooms = 3
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(quote.callCount, 1)
        XCTAssertEqual(quote.requests.last?.rooms, 3)
    }

    func testUnchangedInputDoesNotRequote() async {
        let quote = FakeQuoteClient()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(quote.callCount, 1)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(quote.callCount, 1)
    }

    func testQuoteErrorKeepsPreviousQuote() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 900, currencyCode: "CZK")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 900)

        quote.result = .failure(ApiError(code: "x"))
        vm.update { var s = $0
            s.rooms = 2
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 900)
    }

    func testClearingSelectionReturnsQuoteToIdle() async {
        let quote = FakeQuoteClient()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertNotNil(vm.quoteState.quote)

        vm.update { var s = $0
            s.selectedServiceIds = []
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await Task.yield()

        XCTAssertEqual(vm.quoteState, .idle)
    }

    func testIsQuotingTracksQuoteStateSoContinueCanShowALoader() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1200, currencyCode: "CZK")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        XCTAssertFalse(vm.isQuoting)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        XCTAssertTrue(vm.isQuoting)

        await drainQuote()
        XCTAssertFalse(vm.isQuoting)
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

    private func drainQuote() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }
}
