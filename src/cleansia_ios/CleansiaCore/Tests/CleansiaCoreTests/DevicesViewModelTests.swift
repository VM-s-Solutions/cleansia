import XCTest
@testable import CleansiaCore

@MainActor
final class DevicesViewModelTests: XCTestCase {
    private var client: FakeDevicesClient!
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
        client = FakeDevicesClient()
        snackbar = SnackbarController()
    }

    private func makeVM() -> DevicesViewModel {
        DevicesViewModel(client: client, snackbar: snackbar, revokedMessage: "Device removed")
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

    func testLoadTransitionsLoadingToLoaded() async {
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
        client.myDevicesResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testRevokeSuccessRemovesRowReturnsToIdle() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .success(())
        let vm = makeVM()
        await vm.load()

        await vm.revoke(otherDevice)

        XCTAssertEqual(vm.revokeAction, .idle)
        XCTAssertEqual(vm.state.loadedValue, [thisDevice])
        XCTAssertEqual(client.revokedRowIds, ["row-2"])
        XCTAssertEqual(snackbar.current?.text, "Device removed")
    }

    /// The system confirm has already closed, so the refusal is the snackbar's and the row is free again.
    func testRevokeFailureKeepsListAndSurfacesError() async {
        client.myDevicesResult = .success([thisDevice, otherDevice])
        client.revokeResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()
        await vm.load()

        await vm.revoke(otherDevice)

        XCTAssertEqual(vm.revokeAction, .idle)
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

    /// Revoking the CURRENT device must emit signedOut so the View forces
    /// logout — the server kills the refresh chain but the access token
    /// survives ~15min otherwise.
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

    /// Even if the row's deviceId is nil, the server-set isCurrent flag
    /// still triggers the self-revoke sign-out.
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

@MainActor
final class FakeDevicesClient: DevicesClient {
    var currentDeviceIdValue = "device-current"
    var myDevicesResult: ApiResult<[UserDevice]> = .success([])
    var revokeResult: ApiResult<Void> = .success(())

    private(set) var sentCurrentDeviceIds: [String] = []
    private(set) var revokedRowIds: [String] = []

    /// When set, `revoke` suspends on this until `resumeRevoke()` is called,
    /// so a test can hold one revoke mid-flight and fire a second to exercise
    /// the re-entry guard against real concurrency.
    private var revokeGate: CheckedContinuation<Void, Never>?
    var suspendRevoke = false

    var currentDeviceId: String {
        currentDeviceIdValue
    }

    func myDevices() async -> ApiResult<[UserDevice]> {
        sentCurrentDeviceIds.append(currentDeviceIdValue)
        return myDevicesResult
    }

    func revoke(rowId: String) async -> ApiResult<Void> {
        revokedRowIds.append(rowId)
        if suspendRevoke {
            await withCheckedContinuation { continuation in
                revokeGate = continuation
            }
        }
        return revokeResult
    }

    func resumeRevoke() {
        revokeGate?.resume()
        revokeGate = nil
    }
}
