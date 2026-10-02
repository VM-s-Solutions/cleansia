import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension CreateRecurringViewModelTests {
    // MARK: - The market follows the picked address

    /// A schedule is priced in the currency of its address's country, so the catalogue the form
    /// offers is the one priced for the seeded address's market — read once, after the addresses.
    func testTheCatalogueIsReadForTheSeededAddressesMarket() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true)])
        let (vm, _) = makeVM(catalog: catalog, addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])
        XCTAssertEqual(vm.selectedCountryId, "cze")
    }

    func testAnAddressWithoutACountryReadsThePlatformDefaultCatalogue() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([RecurringFixtures.address(id: "addr-9", isDefault: true)])
        let (vm, _) = makeVM(catalog: catalog, addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(catalog.requestedCountryIds, [nil])
        XCTAssertNil(vm.selectedCountryId)
    }

    func testPickingAnAddressInAnotherCountryReloadsTheCatalogueForThatMarket() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _) = makeVM(catalog: catalog, addressClient: twoMarkets())
        await vm.load()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])

        catalog.result = .success(CatalogFixtures.slovak)
        vm.setSavedAddressId("addr-sk")
        await drain()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.slovak)
        XCTAssertEqual(vm.catalogState.loadedValue?.currencyCode, "EUR")
    }

    func testASameCountryRepickDoesNotReloadTheCatalogue() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true),
            RecurringFixtures.address(id: "addr-cz-2", countryId: "cze")
        ])
        let (vm, _) = makeVM(catalog: catalog, addressClient: addressClient)
        await vm.load()

        vm.setSavedAddressId("addr-cz-2")
        await drain()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])
    }

    /// What the new market does not price is not offered there: the schedule keeps only what the
    /// reloaded catalogue lists, and the screen is told so it can say why the selection shrank.
    func testSelectionsTheMarketDoesNotOfferArePrunedWithANotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _) = makeVM(catalog: catalog, addressClient: twoMarkets())
        await vm.load()
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        vm.toggleService("s-1")
        vm.toggleService("s-2")
        vm.togglePackage("p-1")

        catalog.result = .success(CatalogFixtures.slovak)
        vm.setSavedAddressId("addr-sk")
        await drain()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.formState.selectedPackageIds, [])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
    }

    func testASelectionTheMarketStillOffersIsLeftAloneWithoutANotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _) = makeVM(catalog: catalog, addressClient: twoMarkets())
        await vm.load()
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        vm.toggleService("s-1")

        catalog.result = .success(CatalogFixtures.slovak)
        vm.setSavedAddressId("addr-sk")
        await drain()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [])
    }

    /// A reload that fails keeps the catalogue on screen and touches nothing.
    func testAFailedMarketReloadKeepsTheCatalogueAndTheSelection() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _) = makeVM(catalog: catalog, addressClient: twoMarkets())
        await vm.load()
        vm.toggleService("s-2")

        catalog.result = .failure(ApiError(code: "x"))
        vm.setSavedAddressId("addr-sk")
        await drain()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-2"])
    }

    /// An address picked in the inline manager is not in the form's list until it is re-read; the
    /// market waits for the list rather than flapping through the default and back.
    func testAnAddressTheListDoesNotKnowYetLeavesTheMarketAloneUntilTheListLands() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true)])
        let (vm, _) = makeVM(catalog: catalog, addressClient: addressClient)
        await vm.load()

        vm.setSavedAddressId("addr-new")
        await drain()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])

        catalog.result = .success(CatalogFixtures.slovak)
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true),
            RecurringFixtures.address(id: "addr-new", countryId: "svk")
        ])
        await vm.reloadAddresses()
        await drain()

        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.slovak)
    }

    // MARK: - The start is one the server books: a quarter-hour from 08:00 to 19:45

    func testANewScheduleStartsAtABookableTime() {
        let (vm, _) = makeVM()

        XCTAssertTrue(RecurringTime.bookableTimes.contains(vm.formState.timeOfDay))
    }

    func testAStartTheServerRefusesDoesNotAdvanceAndIsNeverSent() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)

        for refused in ["10:08", "07:45", "20:00", "03:07"] {
            vm.setTimeOfDay(refused)
            XCTAssertFalse(vm.canAdvance(step: 1), "\(refused) advances")
            XCTAssertFalse(vm.isValid, "\(refused) is submittable")
        }
        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(client.createInputs.isEmpty)

        vm.setTimeOfDay("19:45")
        XCTAssertTrue(vm.isValid)
    }

    /// The server refuses an edit that keeps a start outside the window, and the wheel cannot show one.
    func testEditingAScheduleOutsideTheWindowSeedsAndSendsTheNearestBookableTime() async {
        let (vm, client) = makeVM(editing: RecurringFixtures.template(timeOfDay: "21:10"))
        await vm.load()

        XCTAssertEqual(vm.formState.timeOfDay, "19:45")

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.first?.timeOfDay, "19:45")
    }

    func testAScheduleFromAnOrderOffTheGridStartsAtTheNearestBookableTime() async throws {
        let cleaningDate = try XCTUnwrap(Calendar.current.date(from: DateComponents(
            year: 2026, month: 10, day: 5, hour: 7, minute: 20
        )))
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "ord-7",
            cleaningDateTime: cleaningDate,
            services: [OrderFixtures.service(id: "s-1")]
        ))]
        let (vm, _) = makeVM(sourceOrderId: "ord-7", orderClient: orderClient)

        await vm.load()

        XCTAssertEqual(vm.formState.timeOfDay, "08:00")
    }

    func testARefusalForTheBookingWindowIsShownInTheCustomersLanguage() async {
        let refusal = ApiError(code: "order.cleaning_date.outside_booking_window", httpStatus: 400)
        let client = FakeRecurringBookingClient()
        client.createResult = .failure(refusal)
        let snackbar = SnackbarController()
        let (vm, _) = makeVM(recurringClient: client, snackbar: snackbar)
        await vm.load()
        fillValid(vm)

        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertEqual(snackbar.current?.text, ApiErrorLocalizer().message(for: refusal))
        XCTAssertNotEqual(snackbar.current?.text, refusal.code, "the catalog entry is missing")
    }

    func twoMarkets() -> FakeRecurringSavedAddressClient {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true),
            RecurringFixtures.address(id: "addr-sk", countryId: "svk")
        ])
        return addressClient
    }

    func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }
}
