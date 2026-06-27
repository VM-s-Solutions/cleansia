import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class OrderDetailViewModelTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePartnerOrderClient()
        snackbar = SnackbarController()
    }

    private func makeVM(orderId: String = "order-1") -> OrderDetailViewModel {
        OrderDetailViewModel(orderId: orderId, client: client, snackbar: snackbar)
    }

    private func loadedItem(id: String = "order-1", status: Int = 4) -> OrderItem {
        var item = OrderItem()
        item.id = id
        item.displayOrderNumber = "ORD-1"
        item.orderStatus = Code(value: status)
        item.address = OrderAddress(latitude: 50.0, longitude: 14.0)
        return item
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeVM().state.isLoading)
    }

    func testActionStateAndInFlightStartIdle() {
        let vm = makeVM()
        XCTAssertEqual(vm.actionState, .idle)
        XCTAssertNil(vm.inFlightAction)
    }

    func testLoadTransitionsLoadingToLoaded() async {
        client.byIdResult = .success(loadedItem())
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "order-1")
        XCTAssertEqual(vm.state.loadedValue?.status, ._4)
    }

    func testLoadFailureWithNoCacheTransitionsToErrorAndSnackbars() async {
        client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testBackgroundRefetchErrorStaysLoaded() async {
        client.byIdResult = .success(loadedItem())
        let vm = makeVM()
        await vm.load()
        XCTAssertNotNil(vm.state.loadedValue)

        // A subsequent failed refetch must NOT flip a loaded order to .error
        // (OrderDetailViewModel.kt:74-79 parity) — it only snackbars.
        client.byIdResult = .failure(ApiError(httpStatus: 500))
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "order-1")
        XCTAssertNotNil(snackbar.current)
    }

    func testCanShowMapHidesOnlyOnCancelled() async {
        client.byIdResult = .success(loadedItem(status: 5)) // Completed → still shows
        let completedVM = makeVM()
        await completedVM.load()
        XCTAssertEqual(completedVM.state.loadedValue?.canShowMap, true)

        client.byIdResult = .success(loadedItem(status: 6)) // Cancelled → hides
        let cancelledVM = makeVM()
        await cancelledVM.load()
        XCTAssertEqual(cancelledVM.state.loadedValue?.canShowMap, false)
    }

    func testCanShowMapFalseWhenNoCoordinate() async {
        var item = loadedItem()
        item.address = OrderAddress(street: "X")
        client.byIdResult = .success(item)
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.canShowMap, false)
    }
}
