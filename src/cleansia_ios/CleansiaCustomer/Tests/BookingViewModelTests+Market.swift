import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension BookingViewModelTests {
    // MARK: The market follows the service address

    /// The booking is priced in the currency of the address's country, so the quote names that
    /// country; before the address step it names none and the server prices in the platform default.
    func testTheQuoteNamesTheDraftsCountry() async {
        let quote = FakeQuoteClient()
        let scheduler = TestScheduler.dispatch
        let vm = makeVM(quote: quote, scheduler: scheduler)

        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()
        XCTAssertNil(quote.requests.last?.countryId)

        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        await drainQuote()

        XCTAssertEqual(quote.callCount, 2)
        XCTAssertEqual(quote.requests.last?.countryId, "svk")
    }

    func testAnAddressCountryReloadsTheCatalogueForThatMarket() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()
        XCTAssertEqual(catalog.requestedCountryIds, [nil])

        catalog.result = .success(CatalogFixtures.slovak)
        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        await drainQuote()

        XCTAssertEqual(catalog.requestedCountryIds, [nil, "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue?.currencyCode, "EUR")
        XCTAssertEqual(vm.displayCurrencyCode, "EUR")
    }

    /// What the new market does not price is not offered there: the draft keeps only what the reloaded
    /// catalogue lists, and the sheet is told so it can say why the basket shrank.
    func testSelectionsTheMarketDoesNotOfferArePrunedWithANotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()
        var events: [BookingEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        vm.update { var s = $0
            s.selectedServiceIds = ["s-1", "s-2"]
            s.selectedPackageIds = ["p-1"]
            return s
        }

        catalog.result = .success(CatalogFixtures.slovak)
        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        await drainQuote()

        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.state.selectedPackageIds, [])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
    }

    func testASelectionTheMarketStillOffersIsLeftAloneWithoutANotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()
        var events: [BookingEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        vm.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }

        catalog.result = .success(CatalogFixtures.slovak)
        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        await drainQuote()

        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [])
    }

    /// A reload that fails keeps the catalogue on screen and touches nothing, like a failed re-quote.
    func testAFailedMarketReloadKeepsTheCatalogueAndTheSelection() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let vm = makeVM(catalog: catalog, scheduler: .dispatch)
        await vm.loadCatalog()
        vm.update { var s = $0
            s.selectedServiceIds = ["s-2"]
            return s
        }

        catalog.result = .failure(ApiError(code: "x"))
        vm.update { var s = $0
            s.countryId = "svk"
            return s
        }
        await drainQuote()

        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-2"])
    }

    /// The country arrives after the geocoder's ISO code is resolved against the serviced list; the
    /// draft carries it from then on so the quote and the catalogue follow the address.
    func testAnInlineAddressResolvesItsCountryIntoTheDraft() async {
        let country = FakeCountryResolver(resolved: "svk")
        let vm = makeVM(country: country, scheduler: .dispatch)

        vm.applyAddress(GeocodedAddress(
            latitude: 48.1,
            longitude: 17.1,
            street: "Hlavná 1",
            city: "Bratislava",
            zipCode: "811 01",
            country: "Slovakia",
            countryIsoCode: "sk",
            formatted: "Hlavná 1, Bratislava"
        ))
        await drainQuote()

        XCTAssertEqual(country.requestedIsoCodes, ["sk"])
        XCTAssertEqual(vm.state.countryId, "svk")
    }

    func testASavedAddressCarriesItsCountryIntoTheDraft() {
        var saved = SavedAddressFixtures.address(id: "addr-sk")
        saved.countryId = "svk"

        let applied = BookingSavedAddressApply.applied(BookingState(), address: saved)
        let hydrated = BookingPrefill.hydratedWithPreferred(BookingState(), preferred: saved)

        XCTAssertEqual(applied.countryId, "svk")
        XCTAssertEqual(hydrated.countryId, "svk")
    }
}
