import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerDevicesViewModelTests: XCTestCase {
    private var client: FakeCustomerDevicesClient!
    private var snackbar: SnackbarController!

    private let thisDevice = UserDevice(
        id: "row-1",
        platform: "ios",
        deviceId: "device-current",
        lastActiveAt: nil,
        isCurrent: true
    )
    private let otherDevice = UserDevice(
        id: "row-2",
        platform: "android",
        deviceId: "device-other",
        lastActiveAt: nil,
        isCurrent: false
    )

    override func setUp() {
        super.setUp()
        client = FakeCustomerDevicesClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> CustomerDevicesViewModel {
        CustomerDevicesViewModel(client: client, snackbar: snackbar)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeVM().state.isLoading)
    }

    func testLoadSendsTheOneDeviceIdAsCurrentDeviceId() async {
        client.currentDeviceIdValue = "device-from-provider"
        client.myDevicesResult = .success([thisDevice])
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(client.sentCurrentDeviceIds, ["device-from-provider"])
    }

    func testLoadTransitionsToLoaded() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue, [thisDevice, otherDevice])
    }

    func testLoadEmptySuccessTransitionsToLoadedEmptyNotError() async {
        client.myDevicesResult = .success([])
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue, [])
        if case .error = vm.state { XCTFail("empty backend must render the empty state, not error") }
        XCTAssertNil(snackbar.current)
    }

    func testLoadFailureTransitionsToErrorAndSnackbars() async {
        client.myDevicesResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testRevokeOtherRemovesRowAndStaysSignedIn() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var signedOut = false
        let token = vm.signedOut.sink { signedOut = true }
        defer { token.cancel() }

        await vm.revoke(otherDevice)

        XCTAssertEqual(vm.state.loadedValue, [thisDevice])
        XCTAssertEqual(client.revokedRowIds, ["row-2"])
        XCTAssertFalse(signedOut)
    }

    func testSelfRevokeEmitsSignedOut() async {
        client.currentDeviceIdValue = "device-current"
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var signedOut = false
        let token = vm.signedOut.sink { signedOut = true }
        defer { token.cancel() }

        await vm.revoke(thisDevice)

        XCTAssertTrue(signedOut)
    }

    func testSelfRevokeByIsCurrentFlagWhenDeviceIdMissing() async {
        let currentNoDeviceId = UserDevice(
            id: "row-1",
            platform: "ios",
            deviceId: nil,
            lastActiveAt: nil,
            isCurrent: true
        )
        client.myDevicesResult = .success([currentNoDeviceId])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var signedOut = false
        let token = vm.signedOut.sink { signedOut = true }
        defer { token.cancel() }

        await vm.revoke(currentNoDeviceId)

        XCTAssertTrue(signedOut)
    }

    func testRevokeFailureKeepsListAndSurfacesError() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .failure(ApiError(httpStatus: 404))
        let vm = makeVM()
        await vm.load()

        await vm.revoke(otherDevice)

        guard case .error = vm.revokeAction else { return XCTFail("expected action error") }
        XCTAssertEqual(vm.state.loadedValue, [thisDevice, otherDevice])
        XCTAssertNotNil(snackbar.current)
    }
}
