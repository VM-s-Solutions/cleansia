import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

@MainActor
extension CreateRecurringViewModelTests {
    // MARK: - Edit mode

    /// A template is pruned against its market's catalogue on first load, the way Android's
    /// `followMarket` prunes it: a pick the catalogue no longer lists would be refused at submit. The
    /// customer moved no market, so the notice says the entries are no longer offered rather than
    /// that the address does not offer them.
    func testEditingPrunesWhatTheTemplatesMarketNoLongerOffersWithANotice() async {
        let template = RecurringFixtures.template(selectedServiceIds: ["s-1", "retired"])
        let (vm, _) = makeVM(editing: template)
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [.selectionNoLongerOffered])
        XCTAssertTrue(vm.isValid)
    }

    func testEditingATemplateHoldingARetiredPackageSaysItIsNoLongerOffered() async {
        let template = RecurringFixtures.template(selectedPackageIds: ["p-1", "p-retired"])
        let (vm, _) = makeVM(editing: template)
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-1"])
        XCTAssertEqual(events, [.selectionNoLongerOffered])
    }

    /// A retry lands the first catalogue the edited schedule is checked against: still its own market's.
    func testARetriedFirstCatalogueSaysTheEditedSchedulesEntriesAreNoLongerOffered() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let (vm, _) = makeVM(
            editing: RecurringFixtures.template(selectedServiceIds: ["s-1", "retired"]),
            catalog: catalog
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        await vm.load()
        XCTAssertEqual(events, [])

        catalog.result = .success(CatalogFixtures.populated)
        await vm.retryCatalog()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [.selectionNoLongerOffered])
    }

    /// Moving an edited schedule to an address in another market is the customer's own change, and
    /// what that market does not offer is said the way the booking says it.
    func testMovingAnEditedScheduleToAnotherMarketKeepsTheMarketNotice() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _) = makeVM(
            editing: RecurringFixtures.template(selectedServiceIds: ["s-1", "s-2"], savedAddressId: "addr-cz"),
            catalog: catalog,
            addressClient: twoMarkets()
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        await vm.load()
        XCTAssertEqual(events, [])

        catalog.result = .success(CatalogFixtures.slovak)
        vm.setSavedAddressId("addr-sk")
        await drain()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
    }

    /// The same move made while the first catalogue read had failed: the retry lands the new market's
    /// catalogue, and what it trims is the move's, not entries the schedule's own market retired.
    func testARetryAfterMovingAnEditedScheduleToAnotherMarketKeepsTheMarketNotice() async {
        let catalog = FakeCatalogClient(result: .failure(ApiError(code: "x")))
        let (vm, _) = makeVM(
            editing: RecurringFixtures.template(selectedServiceIds: ["s-1", "s-2"], savedAddressId: "addr-cz"),
            catalog: catalog,
            addressClient: twoMarkets()
        )
        var events: [CreateRecurringEvent] = []
        vm.events.sink { events.append($0) }.store(in: &cancellables)
        await vm.load()
        vm.setSavedAddressId("addr-sk")
        await drain()
        XCTAssertEqual(events, [])

        catalog.result = .success(CatalogFixtures.slovak)
        await vm.retryCatalog()

        XCTAssertEqual(catalog.requestedCountryIds.last, "svk")
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(events, [.selectionPrunedForMarket])
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

        let succeeded = await vm.submit()

        XCTAssertTrue(succeeded)
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

        let succeeded = await vm.submit()

        XCTAssertFalse(succeeded)
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

    func localeBundle(_ tag: String) throws -> Bundle {
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

    // MARK: - Dirtiness level

    /// A new schedule asks for the level the way a booking does; nothing is preselected.
    func testANewScheduleStartsWithoutALevelAndBooksTheLevelPicked() async {
        let (vm, client) = makeVM()
        XCTAssertNil(vm.formState.dirtiness)
        await vm.load()
        fillValid(vm)

        vm.setDirtiness(.increased)
        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.first?.dirtiness, .increased)
    }

    /// Repeating an order does not carry its level over: how soiled the home was then says little
    /// about a home cleaned on a schedule.
    func testAScheduleFromAnOrderStillAsksForTheLevel() async {
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "ord-7",
            dirtiness: .heavy,
            dirtinessSurchargeAmount: 600,
            services: [OrderFixtures.service(id: "s-1")]
        ))]
        let (vm, _) = makeVM(sourceOrderId: "ord-7", orderClient: orderClient)

        await vm.load()

        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertNil(vm.formState.dirtiness)
    }

    /// An edit replaces every field it sends, so the schedule's own level is seeded and sent back.
    func testAnEditKeepsTheSchedulesLevel() async {
        let (vm, client) = makeVM(editing: RecurringFixtures.template(dirtiness: .heavy))
        await vm.load()
        XCTAssertEqual(vm.formState.dirtiness, .heavy)

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.first?.dirtiness, .heavy)
    }

    func testPropertySizeNeverGoesNegative() {
        let (vm, _) = makeVM()

        vm.setRooms(-1)
        vm.setBathrooms(-3)

        XCTAssertEqual(vm.formState.rooms, 0)
        XCTAssertEqual(vm.formState.bathrooms, 0)
    }

    /// Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`.
    func testPropertySizeStopsAtTheLargestHomeTheServerAccepts() {
        let (vm, _) = makeVM()

        vm.setRooms(PropertySize.maxRooms + 1)
        vm.setBathrooms(PropertySize.maxBathrooms + 1)

        XCTAssertEqual(vm.formState.rooms, PropertySize.maxRooms)
        XCTAssertEqual(vm.formState.bathrooms, PropertySize.maxBathrooms)
    }

    func testAScheduleFromALargerPastOrderStartsAtTheLargestHomeTheServerAccepts() async {
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "ord-7",
            rooms: PropertySize.maxRooms + 3,
            bathrooms: PropertySize.maxBathrooms + 2,
            services: [OrderFixtures.service(id: "s-1")]
        ))]
        let (vm, _) = makeVM(sourceOrderId: "ord-7", orderClient: orderClient)

        await vm.load()

        XCTAssertEqual(vm.formState.rooms, PropertySize.maxRooms)
        XCTAssertEqual(vm.formState.bathrooms, PropertySize.maxBathrooms)
    }
}
