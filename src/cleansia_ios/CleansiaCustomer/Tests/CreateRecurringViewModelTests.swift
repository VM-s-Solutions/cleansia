import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CreateRecurringViewModelTests: XCTestCase {
    private func makeVM(
        sourceOrderId: String? = nil,
        recurringClient: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        catalog: FakeCatalogClient = FakeCatalogClient(result: .success(CatalogFixtures.populated)),
        addressClient: FakeRecurringSavedAddressClient = FakeRecurringSavedAddressClient(),
        orderClient: FakeOrderClient = FakeOrderClient()
    ) -> (CreateRecurringViewModel, FakeRecurringBookingClient) {
        let repo = RecurringBookingRepository(client: recurringClient)
        let vm = CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
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
}
