import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateRecurringViewModelTests: XCTestCase {
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

    func testIsValidRequiresAddressServiceAndStart() {
        let (vm, _) = makeVM()
        vm.setSavedAddressId("addr-1")
        XCTAssertFalse(vm.isValid)
        vm.toggleService("s-1")
        XCTAssertFalse(vm.isValid)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        XCTAssertTrue(vm.isValid)
    }

    func testSubmitSuccessReturnsTrueAndCallsCreateOnce() async {
        let (vm, client) = makeVM()
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
        fillValid(vm)

        let ok = await vm.submit()

        XCTAssertFalse(ok)
        if case .error = vm.submitState {} else { XCTFail("expected submit error") }
    }

    func testIncompleteFormDoesNotSubmit() async {
        let (vm, client) = makeVM()

        let ok = await vm.submit()

        XCTAssertFalse(ok)
        XCTAssertTrue(client.createInputs.isEmpty)
    }

    func testPathADefaultsAddressToDefaultSaved() async {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringSavedAddress(id: "addr-9", label: "Home", street: "Main 1", city: "Praha", isDefault: true)
        ])
        let (vm, _) = makeVM(addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-9")
        XCTAssertEqual(vm.savedAddresses.count, 1)
    }

    func testPathBPrefillsFromCompletedOrder() async {
        let orderClient = FakeOrderClient()
        let order = OrderItem(
            id: "ord-7",
            rooms: 3,
            bathrooms: 2,
            paymentType: Code(type: "PaymentType", name: nil, value: 2),
            orderStatus: Code(type: "OrderStatus", name: nil, value: 5),
            selectedPackages: [],
            selectedServices: [ServiceDetails(id: "svc-prefill")]
        )
        orderClient.detailResults = [.success(order)]
        let (vm, _) = makeVM(sourceOrderId: "ord-7", orderClient: orderClient)

        await vm.load()

        XCTAssertEqual(vm.formState.rooms, 3)
        XCTAssertEqual(vm.formState.bathrooms, 2)
        XCTAssertEqual(vm.formState.paymentType, 2)
        XCTAssertTrue(vm.formState.selectedServiceIds.contains("svc-prefill"))
    }

    // MARK: - Edit mode

    func testEditingSeedsTheFormFromTheTemplate() {
        let template = RecurringFixtures.template(frequency: 2)
        let (vm, _) = makeVM(editing: template)

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
            RecurringSavedAddress(id: "addr-9", label: "Home", street: "Main 1", city: "Praha", isDefault: true),
            RecurringSavedAddress(id: "addr-1", label: "Flat", street: "Zenklova 6", city: "Praha", isDefault: false)
        ])
        let (vm, _) = makeVM(editing: RecurringFixtures.template(), addressClient: addressClient)

        await vm.load()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-1")
    }

    func testSubmitInEditModeUpdatesInsteadOfCreating() async {
        let template = RecurringFixtures.template()
        let (vm, client) = makeVM(editing: template)
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

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.first?.endsOn, endsOn)
    }

    func testUpdateFailureSetsActionError() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let (vm, _) = makeVM(editing: RecurringFixtures.template(), recurringClient: client)

        let ok = await vm.submit()

        XCTAssertFalse(ok)
        if case .error = vm.submitState {} else { XCTFail("expected submit error") }
    }

    func testDayOfWeekIsEditable() {
        let (vm, _) = makeVM(editing: RecurringFixtures.template())

        vm.setDayOfWeek(0)

        XCTAssertEqual(vm.formState.dayOfWeek, 0)
    }

    // MARK: - Property size

    /// Price-affecting: a blank create defaults to 2 rooms / 1 bathroom, so a
    /// form that never reaches these setters books the wrong flat.
    func testPropertySizeReachesTheSubmittedCommand() async {
        let (vm, client) = makeVM()
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
            RecurringSavedAddress(id: "addr-new", label: "Flat", street: "Zenklova 6", city: "Praha", isDefault: false)
        ])
        await vm.reloadAddresses()

        XCTAssertEqual(vm.savedAddresses.map(\.id), ["addr-new"])
    }

    /// The customer picked the new address in the manager; a reload that also
    /// re-ran the "default ?? first" seeding would silently move them off it.
    func testReloadAddressesLeavesAHandPickedSelectionAlone() async {
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringSavedAddress(id: "addr-default", label: "Home", street: "Main 1", city: "Praha", isDefault: true),
            RecurringSavedAddress(id: "addr-new", label: "Flat", street: "Zenklova 6", city: "Praha", isDefault: false)
        ])
        let (vm, _) = makeVM(addressClient: addressClient)
        await vm.load()
        vm.setSavedAddressId("addr-new")

        await vm.reloadAddresses()

        XCTAssertEqual(vm.formState.savedAddressId, "addr-new")
    }

    func testCreateModeStillCreates() async {
        let (vm, client) = makeVM()
        fillValid(vm)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertTrue(client.updateInputs.isEmpty)
    }
}
