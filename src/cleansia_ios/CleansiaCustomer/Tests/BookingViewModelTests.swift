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
        XCTAssertEqual(vm.quoteState, .quoting(previous: nil))

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

    /// The confirm step reads `quoteState.quote?.totalPrice ?? 0`, so while a
    /// re-quote is in flight the whole price summary used to flash to 0/CZK for
    /// the 400 ms debounce plus the round trip. `.quoting` carries the previous
    /// quote so every reader keeps the stale-but-correct number until the new
    /// one lands.
    func testReQuoteKeepsThePreviousTotalOnScreen() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1200, currencyCode: "CZK")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1200)

        quote.result = .success(BookingQuote(totalPrice: 1500, currencyCode: "CZK"))
        vm.update { var s = $0
            s.rooms = 2
            return s
        }
        scheduler.advance(by: .milliseconds(400))

        XCTAssertTrue(vm.isQuoting)
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1200)
        XCTAssertEqual(vm.quoteState.quote?.currencyCode, "CZK")

        await drainQuote()
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1500)
    }

    /// The very first quote has nothing to fall back on, so the summary must
    /// still be absent rather than showing a fabricated zero.
    func testFirstQuoteHasNoPreviousTotalToKeep() {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1200, currencyCode: "CZK")))
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))

        XCTAssertTrue(vm.isQuoting)
        XCTAssertNil(vm.quoteState.quote)
    }

    /// The carried-over quote is for display only. `lastQuoteRequest` is written
    /// only on a successful quote, so `resolvedQuote` still misses the cache
    /// while a re-quote is in flight and the stale total can never be submitted.
    /// The scheduler is deliberately never advanced here, so the debounced
    /// watcher never fires and `quoteClient` sees exactly the one call made by
    /// `resolvedQuote` itself.
    func testCarriedOverQuoteIsNeverServedToSubmit() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 1500, currencyCode: "CZK")))
        let vm = makeVM(quote: quote, scheduler: .dispatch)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        vm.quoteState = .quoting(previous: BookingQuote(totalPrice: 1200, currencyCode: "CZK"))
        vm.lastQuoteRequest = nil
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1200)

        let resolved = await vm.resolvedQuote(for: vm.state)

        XCTAssertEqual(try? resolved.get().totalPrice, 1500)
        XCTAssertEqual(quote.callCount, 1)
        XCTAssertEqual(vm.quoteState.quote?.totalPrice, 1500)
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

    func testAGuestNeverAsksTheServerAboutAMembership() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(hasMembership: true, freeCancellationWindowHours: 48)
        ))
        let vm = BookingViewModel(membershipClient: membership, tokenStore: FakeTokenStore.guest)

        let snapshot = await vm.loadMembership()

        XCTAssertNil(snapshot)
        XCTAssertEqual(membership.callCount, 0)
        XCTAssertEqual(vm.expressWaiverStatus, .none)
        XCTAssertEqual(vm.expressUpgradesRemaining, 0)
    }

    /// The slot grid and the confirm step both need the answer; two reads of `/Membership/Mine` for one
    /// wizard session is the duplication this seam exists to prevent.
    func testConcurrentMembershipReadsShareOneCall() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(
                hasMembership: true,
                freeCancellationWindowHours: 48,
                expressUpgradesPerMonth: 2,
                expressUpgradesRemaining: 1
            )
        ))
        let vm = BookingViewModel(membershipClient: membership, tokenStore: FakeTokenStore.signedIn())

        async let first = vm.loadMembership()
        async let second = vm.loadMembership()
        _ = await (first, second)
        await vm.loadMembership()

        XCTAssertEqual(membership.callCount, 1)
        XCTAssertEqual(vm.expressWaiverStatus, .available)
        XCTAssertEqual(vm.expressUpgradesRemaining, 1)
    }

    func testAFailedMembershipReadDegradesToSilence() async {
        let membership = FakeMembershipClient(result: .failure(ApiError(httpStatus: 500)))
        let vm = BookingViewModel(membershipClient: membership, tokenStore: FakeTokenStore.signedIn())

        await vm.loadMembership()

        XCTAssertNil(vm.membership)
        XCTAssertEqual(vm.expressWaiverStatus, .none)
    }

    /// The remaining count is the server's, rendered verbatim — never decremented for the booking being
    /// composed, or it disagrees the first time a cancellation releases a slot.
    func testTheRemainingCountIsReportedExactlyAsTheServerSentIt() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(
                hasMembership: true,
                freeCancellationWindowHours: 48,
                expressUpgradesPerMonth: 3,
                expressUpgradesRemaining: 3
            )
        ))
        let vm = BookingViewModel(membershipClient: membership, tokenStore: FakeTokenStore.signedIn())

        await vm.loadMembership()

        XCTAssertEqual(vm.expressUpgradesRemaining, 3)
    }

    func testResetForcesTheNextBookingToRereadTheQuota() async {
        let membership = FakeMembershipClient(result: .success(
            MembershipSnapshot(
                hasMembership: true,
                freeCancellationWindowHours: 48,
                expressUpgradesPerMonth: 2,
                expressUpgradesRemaining: 1
            )
        ))
        let vm = BookingViewModel(membershipClient: membership, tokenStore: FakeTokenStore.signedIn())

        await vm.loadMembership()
        vm.reset()
        XCTAssertEqual(vm.expressWaiverStatus, .none)
        await vm.loadMembership()

        XCTAssertEqual(membership.callCount, 2)
    }

    func testEffectiveDiscountPrefersTheLargerOfTheServerPairAndThePromo() async {
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(
            quote: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1000,
                currencyCode: "CZK",
                tierDiscountAmount: 100,
                membershipDiscountAmount: 50
            ))),
            scheduler: scheduler
        )
        vm.update { var s = $0
            s.selectedServiceIds.insert("s-1")
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(vm.effectiveDiscount, 150, accuracy: 0.0001)
    }

    func testEffectiveDiscountIsZeroWithoutAQuote() {
        XCTAssertEqual(BookingViewModel().effectiveDiscount, 0, accuracy: 0.0001)
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
