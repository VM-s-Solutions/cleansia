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

    private(set) var registeredRequests: [RegisterDeviceRequest] = []
    private(set) var unregisteredDeviceIds: [String] = []

    var registerCallCount: Int {
        lock.withLock { registeredRequests.count }
    }

    var unregisterCallCount: Int {
        lock.withLock { unregisteredDeviceIds.count }
    }

    func register(_ request: RegisterDeviceRequest) async -> ApiResult<Void> {
        lock.withLock { registeredRequests.append(request) }
        return registerResult
    }

    func unregister(deviceId: String) async -> ApiResult<Void> {
        lock.withLock { unregisteredDeviceIds.append(deviceId) }
        return unregisterResult
    }
}
