import CleansiaCore
import XCTest
@testable import CleansiaPartner

@MainActor
final class DevicesViewModelTests: XCTestCase {
    private var client: FakePartnerDevicesClient!
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
        client = FakePartnerDevicesClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> DevicesViewModel {
        DevicesViewModel(client: client, snackbar: snackbar)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeVM().state.isLoading)
    }

    func testLoadTransitionsLoadingToLoaded() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        let vm = makeVM()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue, [thisDevice, otherDevice])
    }

    func testLoadFailureTransitionsToErrorAndSnackbars() async {
        client.myDevicesResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testRevokeSuccessRemovesRowEmitsEffectReturnsToIdle() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var revokedEmitted = false
        let token = vm.revoked.sink { revokedEmitted = true }
        defer { token.cancel() }

        await vm.revoke(otherDevice)

        XCTAssertTrue(revokedEmitted)
        XCTAssertEqual(vm.revokeAction, .idle)
        XCTAssertEqual(vm.state.loadedValue, [thisDevice])
        XCTAssertEqual(client.revokedRowIds, ["row-2"])
    }

    func testRevokeFailureKeepsListAndSurfacesActionError() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()

        await vm.revoke(otherDevice)

        guard case .error = vm.revokeAction else { return XCTFail("expected action error") }
        XCTAssertEqual(vm.state.loadedValue, [thisDevice, otherDevice])
        XCTAssertNotNil(snackbar.current)
    }

    func testRevokeIsReentryGuardedWhileSubmitting() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.suspendRevoke = true
        let vm = makeVM()
        await vm.load()

        // Hold the first revoke mid-flight (suspended in the client), then
        // fire a second — the .submitting guard must drop it before it ever
        // reaches the client.
        let first = Task { await vm.revoke(otherDevice) }
        while client.revokedRowIds.isEmpty {
            await Task.yield()
        }
        XCTAssertTrue(vm.revokeAction.isSubmitting)

        await vm.revoke(otherDevice)
        XCTAssertEqual(client.revokedRowIds.count, 1)

        client.resumeRevoke()
        await first.value
        XCTAssertEqual(vm.revokeAction, .idle)
    }

    /// TC-IOS-DEVICES-SELF-REVOKE (D7b): revoking the CURRENT device must
    /// emit signedOut so the View forces logout — the server kills the
    /// refresh chain but the access token survives ~15min otherwise.
    func testSelfRevokeEmitsSignedOut() async {
        client.currentDeviceIdValue = "device-current"
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var signedOutEmitted = false
        let token = vm.signedOut.sink { signedOutEmitted = true }
        defer { token.cancel() }

        await vm.revoke(thisDevice)

        XCTAssertTrue(signedOutEmitted)
    }

    func testRevokingOtherDeviceDoesNotEmitSignedOut() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        var signedOutEmitted = false
        let token = vm.signedOut.sink { signedOutEmitted = true }
        defer { token.cancel() }

        await vm.revoke(otherDevice)

        XCTAssertFalse(signedOutEmitted)
    }

    /// D7b secondary detection: even if the row's deviceId is nil, the
    /// server-set isCurrent flag still triggers the self-revoke sign-out.
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

        var signedOutEmitted = false
        let token = vm.signedOut.sink { signedOutEmitted = true }
        defer { token.cancel() }

        await vm.revoke(currentNoDeviceId)

        XCTAssertTrue(signedOutEmitted)
    }
}
