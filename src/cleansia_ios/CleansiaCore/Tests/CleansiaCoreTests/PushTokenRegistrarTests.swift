import Foundation
import XCTest
@testable import CleansiaCore

final class PushTokenRegistrarTests: XCTestCase {
    private let deviceId = "device-123"
    private let token = "apns-token-abc"

    private func makeRegistrar(
        client: FakeDeviceRegistrationClient
    ) -> (PushTokenRegistrar, InMemoryLastRegisteredTokenStore) {
        let store = InMemoryLastRegisteredTokenStore()
        let registrar = PushTokenRegistrar(
            client: client,
            deviceIdProvider: StubDeviceIdProvider(deviceId: deviceId),
            tokenStore: store
        )
        return (registrar, store)
    }

    func testEnsureRegisteredSuccessCachesSoRepeatCallSkipsClient() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .success(())
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)
        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(client.registerCallCount, 1)
    }

    func testEnsureRegisteredSendsExpectedRequestWithPlatformIos() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .success(())
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(
            client.registeredRequests,
            [RegisterDeviceRequest(deviceId: deviceId, deviceToken: token, platform: "ios")]
        )
    }

    func testEnsureRegisteredFailureDoesNotCacheSoNextCallRetries() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .failure(ApiError(httpStatus: 500))
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)
        client.registerResult = .success(())
        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(client.registerCallCount, 2)
    }

    func testUnregisterAlwaysClearsCacheOnSuccessSoNextEnsureReRegisters() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .success(())
        client.unregisterResult = .success(())
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)
        await registrar.unregisterDevice()
        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(client.unregisterCallCount, 1)
        XCTAssertEqual(client.unregisteredDeviceIds, [deviceId])
        XCTAssertEqual(client.registerCallCount, 2)
    }

    func testUnregisterFailureStillClearsCacheSoNextEnsureReRegisters() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .success(())
        client.unregisterResult = .failure(ApiError(httpStatus: 500))
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)
        await registrar.unregisterDevice()
        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(client.registerCallCount, 2)
    }

    func testClearWipesCacheWithoutHittingTheNetwork() async {
        let client = FakeDeviceRegistrationClient()
        client.registerResult = .success(())
        let (registrar, _) = makeRegistrar(client: client)

        await registrar.ensureRegistered(token: token)
        await registrar.clear()
        await registrar.ensureRegistered(token: token)

        XCTAssertEqual(client.unregisterCallCount, 0)
        XCTAssertEqual(client.registerCallCount, 2)
    }

    /// A registration still in flight when the session is wiped belongs to the previous account. Writing its
    /// marker afterwards would make the next account's registration of the same token look already done.
    func testARegistrationHeldAcrossClearLeavesNoMarkerSoTheNextAccountRegisters() async {
        let entered = expectation(description: "register entered")
        let client = FakeDeviceRegistrationClient()
        client.holdRegister = true
        client.registerStarted = { entered.fulfill() }
        let (registrar, store) = makeRegistrar(client: client)
        let previous = Task { await registrar.ensureRegistered(token: token) }
        await fulfillment(of: [entered], timeout: 2)

        await registrar.clear()
        client.releaseRegister(.success(()))
        await previous.value

        XCTAssertNil(store.read())
        client.holdRegister = false
        await registrar.ensureRegistered(token: token)
        XCTAssertEqual(client.registerCallCount, 2)
        XCTAssertEqual(store.read(), token)
    }
}

private final class StubDeviceIdProvider: DeviceIdProviding {
    let deviceId: String
    init(deviceId: String) {
        self.deviceId = deviceId
    }
}

private final class InMemoryLastRegisteredTokenStore: LastRegisteredTokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var value: String?

    func read() -> String? {
        lock.withLock { value }
    }

    func write(_ token: String) {
        lock.withLock { value = token }
    }

    func clear() {
        lock.withLock { value = nil }
    }
}

private final class FakeDeviceRegistrationClient: DeviceRegistrationClient, @unchecked Sendable {
    private let lock = NSLock()

    var registerResult: ApiResult<Void> = .success(())
    var unregisterResult: ApiResult<Void> = .success(())
    var holdRegister = false
    var registerStarted: (() -> Void)?

    private(set) var registeredRequests: [RegisterDeviceRequest] = []
    private(set) var unregisteredDeviceIds: [String] = []
    private var heldRegister: CheckedContinuation<ApiResult<Void>, Never>?

    var registerCallCount: Int {
        lock.withLock { registeredRequests.count }
    }

    var unregisterCallCount: Int {
        lock.withLock { unregisteredDeviceIds.count }
    }

    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        lock.withLock { registeredRequests.append(request) }
        guard holdRegister else { return registerResult }
        return await withCheckedContinuation { continuation in
            lock.withLock { heldRegister = continuation }
            registerStarted?()
        }
    }

    func releaseRegister(_ result: ApiResult<Void>) {
        let held = lock.withLock {
            defer { heldRegister = nil }
            return heldRegister
        }
        held?.resume(returning: result)
    }

    func unregister(deviceId: String) async -> ApiResult<Void> {
        lock.withLock { unregisteredDeviceIds.append(deviceId) }
        return unregisterResult
    }
}
