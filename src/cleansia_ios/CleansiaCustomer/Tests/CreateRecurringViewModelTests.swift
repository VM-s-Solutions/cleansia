import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateRecurringViewModelTests: XCTestCase {
    var cancellables = Set<AnyCancellable>()

    func makeVM(
        sourceOrderId: String? = nil,
        editing: RecurringTemplate? = nil,
        recurringClient: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        catalog: FakeCatalogClient = FakeCatalogClient(result: .success(CatalogFixtures.populated)),
        addressClient: FakeRecurringSavedAddressClient = FakeRecurringSavedAddressClient(),
        orderClient: FakeOrderClient = FakeOrderClient(),
        snackbar: SnackbarController? = nil
    ) -> (CreateRecurringViewModel, FakeRecurringBookingClient) {
        let repo = RecurringBookingRepository(client: recurringClient)
        let vm = CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
            editing: editing,
            repository: repo,
            catalogClient: catalog,
            addressClient: addressClient,
            orderClient: orderClient,
            quoteClient: FakeQuoteClient(),
            cleanersClient: FakeServingCleanersClient(),
            consentClient: FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy]),
            snackbar: snackbar ?? SnackbarController(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
        return (vm, recurringClient)
    }

    func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setDirtiness(.normal)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        vm.setEarlyPerformanceRequested(true)
    }

    func testStartsIdleAndInvalid() {
        let (vm, _) = makeVM()
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertFalse(vm.isValid)
    }

    func testIsValidRequiresAddressServiceLevelAndStart() async {
        let (vm, _) = makeVM()
        await vm.load()
        vm.setSavedAddressId("addr-1")
        XCTAssertFalse(vm.isValid)
        vm.toggleService("s-1")
        XCTAssertFalse(vm.isValid)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        XCTAssertFalse(vm.isValid, "a new schedule is submittable without a level")
        vm.setDirtiness(.normal)
        XCTAssertFalse(vm.isValid, "a new schedule is submittable without the early-performance request")
        vm.setEarlyPerformanceRequested(true)
        XCTAssertTrue(vm.isValid)
    }

    /// The Android form's per-step gate, step for step: the schedule, the selection, the address and
    /// the start. Its conjunction is what the one-page form submits on.
    func testCanAdvanceGatesEachStepLikeAndroid() async {
        let (vm, _) = makeVM(addressClient: {
            let client = FakeRecurringSavedAddressClient()
            client.result = .success([])
            return client
        }())
        await vm.load()
        XCTAssertTrue(vm.canAdvance(step: 1), "the default 10:00 satisfies the schedule step")
        XCTAssertFalse(vm.canAdvance(step: 2))
        XCTAssertFalse(vm.canAdvance(step: 3))
        XCTAssertFalse(vm.canAdvance(step: 4))

        vm.setTimeOfDay("  ")
        XCTAssertFalse(vm.canAdvance(step: 1))
        vm.setTimeOfDay("09:30")
        XCTAssertTrue(vm.canAdvance(step: 1))

        vm.togglePackage("p-1")
        XCTAssertFalse(vm.canAdvance(step: 2), "a selection without a level advances")
        vm.setDirtiness(.increased)
        XCTAssertTrue(vm.canAdvance(step: 2))
        vm.togglePackage("p-1")
        vm.toggleService("s-1")
        XCTAssertTrue(vm.canAdvance(step: 2))

        vm.setSavedAddressId("addr-1")
        XCTAssertFalse(vm.canAdvance(step: 3), "an address without a start date does not advance")
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        XCTAssertTrue(vm.canAdvance(step: 3))
        vm.setEarlyPerformanceRequested(true)
        XCTAssertTrue(vm.isValid)
    }

    func testIsValidIsTheConjunctionOfEveryStep() {
        var state = CreateRecurringFormState()
        state.savedAddressId = "addr-1"
        state.selectedServiceIds = ["s-1"]
        state.dirtiness = .normal
        state.startsOn = Date(timeIntervalSince1970: 1_780_000_000)
        XCTAssertTrue(state.isValid)

        state.timeOfDay = ""
        XCTAssertFalse(state.isValid, "a blank time fails step 1 and therefore the form")
    }

    func testSubmitSuccessReturnsTrueAndCallsCreateOnce() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)

        let succeeded = await vm.submit()

        XCTAssertTrue(succeeded)
        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertEqual(client.createInputs.first?.savedAddressId, "addr-1")
        XCTAssertEqual(client.createInputs.first?.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testSubmitFailureSetsActionError() async {
        let client = FakeRecurringBookingClient()
        client.createResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(recurringClient: client)
        await vm.load()
        fillValid(vm)

        let succeeded = await vm.submit()

        XCTAssertFalse(succeeded)
        if case .error = vm.submitState {} else { XCTFail("expected submit error") }
    }

    // MARK: - The request to start within the withdrawal period

    /// One tick covers every occurrence the schedule creates, and the server refuses a schedule without it.
    func testANewScheduleWithoutTheEarlyPerformanceRequestIsNeitherSubmittableNorSent() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)
        vm.setEarlyPerformanceRequested(false)

        XCTAssertFalse(vm.isValid)
        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(client.createInputs.isEmpty)
    }

    func testTheRequestRidesTheNewSchedule() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.first?.earlyPerformanceRequested, true)
    }

    /// The schedule's act was recorded when it was created and survives the edit; the update carries none.
    func testAnEditAsksForNoRequest() async {
        let (vm, client) = makeVM(editing: RecurringFixtures.template())
        await vm.load()

        XCTAssertFalse(vm.formState.earlyPerformanceRequested)
        XCTAssertTrue(vm.isValid)
        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertEqual(client.updateInputs.count, 1)
    }

    func testIncompleteFormDoesNotSubmit() async {
        let (vm, client) = makeVM()
        await vm.load()

        let succeeded = await vm.submit()

        XCTAssertFalse(succeeded)
        XCTAssertTrue(client.createInputs.isEmpty)
    }

    // MARK: - The catalogue must have landed before anything is submitted

    /// The form fills in without a catalogue (an address, a prefilled pick, a date), so a load that
    /// failed once would otherwise let a selection nobody could see go out unpruned.
    func testAFailedCatalogueLoadIsAnErrorStateThatHoldsSubmit() async {
        let (vm, client) = makeVM(catalog: FakeCatalogClient(result: .failure(ApiError(code: "x"))))
        await vm.load()
        fillValid(vm)

        if case .error = vm.catalogState {} else { XCTFail("expected a catalogue error state") }
        XCTAssertTrue(vm.formState.isValid)
        XCTAssertFalse(vm.isValid)
        let succeeded = await vm.submit()
        XCTAssertFalse(succeeded)
        XCTAssertTrue(client.createInputs.isEmpty)
        XCTAssertEqual(vm.submitState, .idle)
    }

    func testRetryAfterAFailedLoadLandsTheCatalogueForTheSelectedMarketAndPrunesThePrefill() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let vm = prefilledFromOrder(
            catalog: catalog,
            services: [OrderFixtures.service(id: "s-1"), OrderFixtures.service(id: "s-2")],
            packages: [OrderFixtures.package(id: "p-1")]
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        await vm.load()
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1", "s-2"])
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-1"])
        XCTAssertEqual(events, [])

        catalog.result = .success(CatalogFixtures.slovak)
        await vm.retryCatalog()

        XCTAssertEqual(catalog.requestedCountryIds, ["svk", "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.slovak)
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.formState.selectedPackageIds, [])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        vm.setDirtiness(.normal)
        vm.setEarlyPerformanceRequested(true)
        XCTAssertTrue(vm.isValid)
    }

    /// While the load is in error there is nothing to re-read for a new market; the first catalogue
    /// the retry lands is the selected market's, and from then on the address drives the market.
    func testTheMarketFollowsTheAddressAgainOnceARetriedCatalogueLands() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let (vm, _) = makeVM(catalog: catalog, addressClient: twoMarkets())
        await vm.load()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])

        vm.setSavedAddressId("addr-sk")
        await drain()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze"])

        catalog.result = .success(CatalogFixtures.slovak)
        await vm.retryCatalog()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.slovak)

        catalog.result = .success(CatalogFixtures.populated)
        vm.setSavedAddressId("addr-cz")
        await drain()
        XCTAssertEqual(catalog.requestedCountryIds, ["cze", "svk", "cze"])
        XCTAssertEqual(vm.catalogState.loadedValue, CatalogFixtures.populated)
    }

    func testARetryThatFailsAgainStaysInError() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let (vm, _) = makeVM(catalog: catalog)
        await vm.load()

        await vm.retryCatalog()

        XCTAssertEqual(catalog.callCount, 2)
        if case .error = vm.catalogState {} else { XCTFail("expected a catalogue error state") }
        XCTAssertFalse(vm.isValid)
    }

    func testPathADefaultsAddressToDefaultSaved() async {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-9", isDefault: true)
        ])
        let (vm, _) = makeVM(addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-9")
        XCTAssertEqual(vm.savedAddresses.count, 1)
    }

    func testPathBPrefillsFromCompletedOrder() async {
        let orderClient = FakeOrderClient()
        let order = OrderFixtures.detail(
            id: "ord-7",
            statusCode: Code(type: "OrderStatus", name: nil, value: 5),
            rooms: 3,
            bathrooms: 2,
            services: [OrderFixtures.service(id: "s-1")],
            paymentType: Code(type: "PaymentType", name: nil, value: 2)
        )
        orderClient.detailResults = [.success(order)]
        let (vm, _) = makeVM(sourceOrderId: "ord-7", orderClient: orderClient)

        await vm.load()

        XCTAssertEqual(vm.formState.rooms, 3)
        XCTAssertEqual(vm.formState.bathrooms, 2)
        XCTAssertEqual(vm.formState.paymentType, 2)
        XCTAssertTrue(vm.formState.selectedServiceIds.contains("s-1"))
    }

    /// The default address decides the market the repeated order is priced in: a Slovak address reads
    /// the Slovak catalogue (EUR), and the order's picks land on top of that address, not the reverse.
    func testPathBWithASlovakSavedAddressPrefillsOntoTheSlovakMarket() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.slovak))
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze"),
            RecurringFixtures.address(id: "addr-sk", countryId: "svk", isDefault: true)
        ])
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "ord-7",
            rooms: 3,
            bathrooms: 2,
            services: [OrderFixtures.service(id: "s-1")],
            paymentType: Code(type: "PaymentType", name: nil, value: 2)
        ))]
        let (vm, _) = makeVM(
            sourceOrderId: "ord-7",
            catalog: catalog,
            addressClient: addressClient,
            orderClient: orderClient
        )

        await vm.load()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-sk")
        XCTAssertEqual(vm.selectedCountryId, "svk")
        XCTAssertEqual(catalog.requestedCountryIds, ["svk"])
        XCTAssertEqual(vm.catalogState.loadedValue?.currencyCode, "EUR")
        XCTAssertEqual(vm.formState.rooms, 3)
        XCTAssertEqual(vm.formState.bathrooms, 2)
        XCTAssertEqual(vm.formState.paymentType, 2)
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
    }

    // MARK: - A prefilled selection is priced in the address's market, not the source order's

    /// The order being repeated may have been priced for another market; what the seeded address's
    /// catalogue does not list is dropped, and the screen is told, exactly as when the address moves.
    func testAPrefilledPickTheMarketDoesNotOfferIsPrunedWithANotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.slovak))
        let vm = prefilledFromOrder(
            catalog: catalog,
            services: [OrderFixtures.service(id: "s-1"), OrderFixtures.service(id: "s-2")],
            packages: [OrderFixtures.package(id: "p-1")]
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(catalog.requestedCountryIds, ["svk"])
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.formState.selectedPackageIds, [])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
    }

    func testAPrefilledPickTheMarketOffersSurvives() async {
        let vm = prefilledFromOrder(
            catalog: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            services: [OrderFixtures.service(id: "s-2")],
            packages: [OrderFixtures.package(id: "p-1")]
        )

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-2"])
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-1"])
    }

    func testAPrefilledSelectionTheMarketFullyOffersRaisesNoNotice() async {
        let vm = prefilledFromOrder(
            catalog: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            services: [OrderFixtures.service(id: "s-1")]
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [])
    }

    func prefilledFromOrder(
        catalog: FakeCatalogClient,
        services: [CustomerOrderService] = [],
        packages: [CustomerOrderPackage] = []
    ) -> CreateRecurringViewModel {
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [
            .success(OrderFixtures.detail(id: "ord-7", services: services, packages: packages))
        ]
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([RecurringFixtures.address(id: "addr-sk", countryId: "svk", isDefault: true)])
        let (vm, _) = makeVM(
            sourceOrderId: "ord-7",
            catalog: catalog,
            addressClient: addressClient,
            orderClient: orderClient
        )
        return vm
    }

    // MARK: - The start date

    /// The server refuses a start on or after the end date (`recurring_template.ends_on_before_start`),
    /// and this form cannot move the end date, so the picker stops the day before it, as the web does.
    func testAnEditOffersNoStartOnOrAfterTheStoredEndDate() throws {
        let endsOn = Date(timeIntervalSince1970: 1_800_000_000)
        let (vm, _) = makeVM(editing: RecurringFixtures.template(endsOn: endsOn))

        let dayBefore = try XCTUnwrap(Calendar.current.date(byAdding: .day, value: -1, to: endsOn))
        XCTAssertEqual(vm.latestStart, dayBefore)
        XCTAssertEqual(vm.startRange.upperBound, dayBefore)
        XCTAssertFalse(vm.startRange.contains(endsOn))
    }

    func testAStartWithNoEndDateIsOpenEnded() {
        let (create, _) = makeVM()
        let (edit, _) = makeVM(editing: RecurringFixtures.template())

        XCTAssertNil(create.latestStart)
        XCTAssertNil(edit.latestStart)
        XCTAssertEqual(edit.startRange.upperBound, .distantFuture)
    }

    /// An end date less than a day after the start leaves no day to offer but the earliest one, and a
    /// range whose bounds cross would trap.
    func testAnEndDateInsideTheFirstDayCollapsesTheRangeInsteadOfCrossingIt() {
        let startsOn = RecurringFixtures.template().startsOn
        let (vm, _) = makeVM(editing: RecurringFixtures.template(endsOn: startsOn.addingTimeInterval(3600)))

        XCTAssertEqual(vm.startRange.lowerBound, vm.startRange.upperBound)
    }

    // MARK: - Addresses added from inside the form

    /// The form's address list is a snapshot taken on `load()`. An address added
    /// through the inline manager is invisible until it is re-read, so the row
    /// the customer just created would not be there to select.
    func testReloadAddressesPicksUpOneAddedWhileTheFormWasOpen() async {
        let addressClient = FakeRecurringSavedAddressClient()
        let (vm, _) = makeVM(addressClient: addressClient)
        await vm.load()
        XCTAssertTrue(vm.savedAddresses.isEmpty)

        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-new")
        ])
        await vm.reloadAddresses()

        XCTAssertEqual(vm.savedAddresses.map(\.id), ["addr-new"])
    }

    /// The customer picked the new address in the manager; a reload that also
    /// re-ran the "default ?? first" seeding would silently move them off it.
    func testReloadAddressesLeavesAHandPickedSelectionAlone() async {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-default", isDefault: true),
            RecurringFixtures.address(id: "addr-new")
        ])
        let (vm, _) = makeVM(addressClient: addressClient)
        await vm.load()
        vm.setSavedAddressId("addr-new")

        await vm.reloadAddresses()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-new")
    }

    func testCreateModeStillCreates() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertTrue(client.updateInputs.isEmpty)
    }
}

final class RecurringTimeTests: XCTestCase {
    func testTheWheelOffersEveryQuarterHourFromEightToQuarterToEight() {
        let times = RecurringTime.bookableTimes

        XCTAssertEqual(times.count, 48)
        XCTAssertEqual(times.first, "08:00")
        XCTAssertEqual(times.last, "19:45")
        XCTAssertTrue(times.contains("12:15"))
        XCTAssertFalse(times.contains("12:10"))
        XCTAssertFalse(times.contains("20:00"))
    }

    func testTheNearestBookableTimeRoundsToTheQuarterHourAndStaysInTheWindow() {
        XCTAssertEqual(RecurringTime.nearestBookable("11:30"), "11:30")
        XCTAssertEqual(RecurringTime.nearestBookable("10:07"), "10:00")
        XCTAssertEqual(RecurringTime.nearestBookable("10:08"), "10:15")
        XCTAssertEqual(RecurringTime.nearestBookable("07:59"), "08:00")
        XCTAssertEqual(RecurringTime.nearestBookable("00:00"), "08:00")
        XCTAssertEqual(RecurringTime.nearestBookable("19:53"), "19:45")
        XCTAssertEqual(RecurringTime.nearestBookable("23:59"), "19:45")
        XCTAssertEqual(RecurringTime.nearestBookable("09:30:00"), "09:30")
    }

    func testAnUnreadableTimeFallsBackToTheDefaultStart() {
        XCTAssertEqual(RecurringTime.nearestBookable(""), RecurringTime.defaultTime)
        XCTAssertEqual(RecurringTime.nearestBookable("soon"), RecurringTime.defaultTime)
        XCTAssertTrue(RecurringTime.bookableTimes.contains(RecurringTime.defaultTime))
    }
}
