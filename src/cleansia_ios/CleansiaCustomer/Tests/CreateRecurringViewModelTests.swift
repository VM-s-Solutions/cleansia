import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateRecurringViewModelTests: XCTestCase {
    private var cancellables = Set<AnyCancellable>()

    private func makeVM(
        sourceOrderId: String? = nil,
        editing: RecurringTemplate? = nil,
        recurringClient: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        catalog: FakeCatalogClient = FakeCatalogClient(result: .success(CatalogFixtures.populated)),
        addressClient: FakeRecurringSavedAddressClient = FakeRecurringSavedAddressClient(),
        orderClient: FakeOrderClient = FakeOrderClient()
    ) -> (CreateRecurringViewModel, FakeRecurringBookingClient) {
        let repo = RecurringBookingRepository(client: recurringClient)
        let vm = CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
            editing: editing,
            repository: repo,
            catalogClient: catalog,
            addressClient: addressClient,
            orderClient: orderClient,
            snackbar: SnackbarController()
        )
        return (vm, recurringClient)
    }

    private func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
    }

    func testStartsIdleAndInvalid() {
        let (vm, _) = makeVM()
        XCTAssertEqual(vm.submitState, .idle)
        XCTAssertFalse(vm.isValid)
    }

    func testIsValidRequiresAddressServiceAndStart() async {
        let (vm, _) = makeVM()
        await vm.load()
        vm.setSavedAddressId("addr-1")
        XCTAssertFalse(vm.isValid)
        vm.toggleService("s-1")
        XCTAssertFalse(vm.isValid)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
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
        XCTAssertTrue(vm.canAdvance(step: 2))
        vm.togglePackage("p-1")
        vm.toggleService("s-1")
        XCTAssertTrue(vm.canAdvance(step: 2))

        vm.setSavedAddressId("addr-1")
        XCTAssertFalse(vm.canAdvance(step: 3), "an address without a start date does not advance")
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        XCTAssertTrue(vm.canAdvance(step: 3))
        XCTAssertTrue(vm.isValid)
    }

    func testIsValidIsTheConjunctionOfEveryStep() {
        var state = CreateRecurringFormState()
        state.savedAddressId = "addr-1"
        state.selectedServiceIds = ["s-1"]
        state.startsOn = Date(timeIntervalSince1970: 1_780_000_000)
        XCTAssertTrue(state.isValid)

        state.timeOfDay = ""
        XCTAssertFalse(state.isValid, "a blank time fails step 1 and therefore the form")
    }

    func testSubmitSuccessReturnsTrueAndCallsCreateOnce() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)

        let ok = await vm.submit()

        XCTAssertTrue(ok)
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

        let ok = await vm.submit()

        XCTAssertFalse(ok)
        if case .error = vm.submitState {} else { XCTFail("expected submit error") }
    }

    func testIncompleteFormDoesNotSubmit() async {
        let (vm, client) = makeVM()
        await vm.load()

        let ok = await vm.submit()

        XCTAssertFalse(ok)
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
        let ok = await vm.submit()
        XCTAssertFalse(ok)
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

    private func prefilledFromOrder(
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

    // MARK: - Edit mode

    /// A template is pruned against its market's catalogue on first load, the way Android's
    /// `followMarket` prunes it: a pick the catalogue no longer lists would be refused at submit.
    func testEditingPrunesWhatTheTemplatesMarketNoLongerOffersWithANotice() async {
        let template = RecurringFixtures.template(selectedServiceIds: ["s-1", "retired"])
        let (vm, _) = makeVM(editing: template)
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
        XCTAssertTrue(vm.isValid)
    }

    func testEditingATemplateTheMarketFullyOffersRaisesNoNotice() async {
        let (vm, _) = makeVM(editing: RecurringFixtures.template())
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [])
    }

    func testEditingSeedsTheFormFromTheTemplate() async {
        let template = RecurringFixtures.template(frequency: 2)
        let (vm, _) = makeVM(editing: template)
        await vm.load()

        XCTAssertTrue(vm.isEditing)
        XCTAssertTrue(vm.isValid)
        XCTAssertEqual(vm.formState.frequency, .biweekly)
        XCTAssertEqual(vm.formState.dayOfWeek, template.dayOfWeek)
        XCTAssertEqual(vm.formState.timeOfDay, template.timeOfDay)
        XCTAssertEqual(vm.formState.rooms, template.rooms)
        XCTAssertEqual(vm.formState.bathrooms, template.bathrooms)
        XCTAssertEqual(vm.formState.savedAddressId, template.savedAddressId)
        XCTAssertEqual(vm.formState.selectedServiceIds, Set(template.selectedServiceIds))
        XCTAssertEqual(vm.formState.paymentType, template.paymentType)
        XCTAssertEqual(vm.formState.startsOn, template.startsOn)
    }

    func testLoadDoesNotOverwriteTheEditedTemplateAddressWithTheDefaultOne() async {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-9", isDefault: true),
            RecurringFixtures.address(id: "addr-1")
        ])
        let (vm, _) = makeVM(editing: RecurringFixtures.template(), addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-1")
    }

    func testSubmitInEditModeUpdatesInsteadOfCreating() async {
        let template = RecurringFixtures.template()
        let (vm, client) = makeVM(editing: template)
        await vm.load()
        vm.setRooms(5)

        let ok = await vm.submit()

        XCTAssertTrue(ok)
        XCTAssertTrue(client.createInputs.isEmpty)
        XCTAssertEqual(client.updateInputs.count, 1)
        XCTAssertEqual(client.updateInputs.first?.templateId, "tpl-1")
        XCTAssertEqual(client.updateInputs.first?.rooms, 5)
    }

    /// `UpdateRecurringBooking` replaces `EndsOn` with whatever it is sent, so an edit that omits the
    /// template's existing end date silently makes the schedule run forever.
    func testUpdateCarriesTheTemplateEndDateForward() async {
        let endsOn = Date(timeIntervalSince1970: 1_800_000_000)
        let template = RecurringFixtures.template(endsOn: endsOn)
        let (vm, client) = makeVM(editing: template)
        await vm.load()

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.first?.endsOn, endsOn)
    }

    func testUpdateFailureSetsActionError() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(editing: RecurringFixtures.template(), recurringClient: client)
        await vm.load()

        let ok = await vm.submit()

        XCTAssertFalse(ok)
        if case .error = vm.submitState {} else { XCTFail("expected submit error") }
    }

    func testDayOfWeekIsEditable() {
        let (vm, _) = makeVM(editing: RecurringFixtures.template())

        vm.setDayOfWeek(0)

        XCTAssertEqual(vm.formState.dayOfWeek, 0)
    }

    /// An update is a full replace, so an edit that also prefilled from an order would submit that
    /// order's rooms, services and time over the live schedule. `sourceOrderId` is dropped when a
    /// template is being edited — with a non-blank id, so both ternary branches are reachable.
    func testEditingIgnoresASourceOrderInsteadOfPrefillingOverTheTemplate() async {
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(id: "ord-7", rooms: 9, bathrooms: 9))]
        let (vm, client) = makeVM(
            sourceOrderId: "ord-7",
            editing: RecurringFixtures.template(),
            orderClient: orderClient
        )

        await vm.load()
        _ = await vm.submit()

        XCTAssertEqual(orderClient.detailCallCount, 0, "an edit prefilled from an unrelated order")
        XCTAssertEqual(client.updateInputs.first?.rooms, 2)
        XCTAssertEqual(client.updateInputs.first?.bathrooms, 1)
    }

    // MARK: - What an edit does and does not touch

    func testTheAppliesNoticeIsShownOnlyWhenEditing() {
        let (create, _) = makeVM()
        let (edit, _) = makeVM(editing: RecurringFixtures.template())

        XCTAssertNil(create.appliesNotice)
        XCTAssertNotNil(edit.appliesNotice)
    }

    func testTheAppliesNoticeIsLocalizedInEveryLocale() throws {
        let (vm, _) = makeVM(editing: RecurringFixtures.template())
        let restore = L10n.bundle
        defer { L10n.bundle = restore }

        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            let notice = try XCTUnwrap(vm.appliesNotice)
            XCTAssertNotEqual(notice, "recurring_edit_applies_notice", "unlocalized in \(language)")
            XCTAssertFalse(notice.isBlank, "empty in \(language)")
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }

    // MARK: - Property size

    /// Price-affecting: a blank create defaults to 2 rooms / 1 bathroom, so a
    /// form that never reaches these setters books the wrong flat.
    func testPropertySizeReachesTheSubmittedCommand() async {
        let (vm, client) = makeVM()
        await vm.load()
        fillValid(vm)
        vm.setRooms(4)
        vm.setBathrooms(2)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.first?.rooms, 4)
        XCTAssertEqual(client.createInputs.first?.bathrooms, 2)
    }

    func testPropertySizeNeverGoesNegative() {
        let (vm, _) = makeVM()

        vm.setRooms(-1)
        vm.setBathrooms(-3)

        XCTAssertEqual(vm.formState.rooms, 0)
        XCTAssertEqual(vm.formState.bathrooms, 0)
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

    private func twoMarkets() -> FakeRecurringSavedAddressClient {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze", isDefault: true),
            RecurringFixtures.address(id: "addr-sk", countryId: "svk")
        ])
        return addressClient
    }

    private func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }
}
