import XCTest
@testable import CleansiaCore

final class PushLogoutClearsTests: XCTestCase {
    override func setUp() {
        super.setUp()
        MockURLProtocol.recorder.reset()
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
        super.tearDown()
    }

    private func makeClient(
        store: TokenStore,
        caches: SessionScopedCacheRegistry
    ) throws -> AuthApiClient {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(
                deviceIdProvider: StubDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            sessionScopedCaches: caches,
            authedSession: session,
            noAuthSession: session
        )
    }

    func testUserLogoutRunsPreLogoutWhileBearerIsLiveThenWipesToken() async throws {
        let store = StubTokenStore(accessToken: "access-1", refreshToken: "r1")
        let client = try makeClient(store: store, caches: SessionScopedCacheRegistry())
        MockURLProtocol.handler = { _ in (200, Data("{}".utf8)) }

        var tokenAtPreLogout: String?
        client.setPreLogout {
            tokenAtPreLogout = store.current()?.accessToken
        }

        await client.logout()

        XCTAssertEqual(tokenAtPreLogout, "access-1", "the pre-logout hook must run while the Bearer is still live")
        XCTAssertNil(store.current(), "the token must be wiped after logout")
    }

    func testRegistrarUnregisterRidesThePreLogoutHookBeforeWipe() async throws {
        let store = StubTokenStore(accessToken: "access-1", refreshToken: "r1")
        let client = try makeClient(store: store, caches: SessionScopedCacheRegistry())
        MockURLProtocol.handler = { _ in (200, Data("{}".utf8)) }

        let registrationClient = FakeRegistrationClient(deviceIdProvider: StubDeviceId(), tokenStore: store)
        let registrar = makeRegistrar(registrationClient)
        client.setPreLogout { [registrar] in await registrar.unregisterDevice() }

        await registrar.ensureRegistered(token: "apns-1")
        await client.logout()

        XCTAssertEqual(
            registrationClient.unregisterTokenPresence,
            [true],
            "Device/Unregister must see a live token (the pre-logout hook runs before the wipe)"
        )
        XCTAssertNil(store.current())
    }

    private func makeRegistrar(
        _ client: FakeRegistrationClient
    ) -> PushTokenRegistrar {
        PushTokenRegistrar(
            client: client,
            deviceIdProvider: StubDeviceId(),
            tokenStore: InMemoryLastTokenStore()
        )
    }

    func testCacheClearRunsOnExplicitLogout() async throws {
        let caches = SessionScopedCacheRegistry()
        let registrationClient = FakeRegistrationClient(deviceIdProvider: StubDeviceId())
        let registrar = makeRegistrar(registrationClient)
        caches.register(registrar)
        let store = StubTokenStore(accessToken: "access-1", refreshToken: "r1")
        let client = try makeClient(store: store, caches: caches)
        MockURLProtocol.handler = { _ in (200, Data("{}".utf8)) }

        await registrar.ensureRegistered(token: "apns-1")
        await client.logout()
        await registrar.ensureRegistered(token: "apns-1")

        XCTAssertEqual(
            registrationClient.registerCallCount,
            2,
            "the cleared cache forces a fresh register after logout"
        )
    }

    func testCacheClearRunsOnForcedSignOut() async {
        let caches = SessionScopedCacheRegistry()
        let registrationClient = FakeRegistrationClient(deviceIdProvider: StubDeviceId())
        let registrar = makeRegistrar(registrationClient)
        caches.register(registrar)

        await registrar.ensureRegistered(token: "apns-1")
        await caches.clearAll()
        await registrar.ensureRegistered(token: "apns-1")

        XCTAssertEqual(registrationClient.registerCallCount, 2, "forced sign-out clearAll() also re-arms the registrar")
    }
}

private struct StubDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private final class InMemoryLastTokenStore: LastRegisteredTokenStore, @unchecked Sendable {
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

private final class StubTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

    init(accessToken: String, refreshToken: String) {
        let future = Date(timeIntervalSinceNow: 9999)
        stored = AuthTokens(
            accessToken: accessToken,
            accessTokenExpiresAt: future,
            refreshToken: refreshToken,
            refreshTokenExpiresAt: future
        )
    }

    func current() -> AuthTokens? {
        lock.withLock { stored }
    }

    func save(_ tokens: AuthTokens) {
        lock.withLock { stored = tokens }
    }

    func clear() {
        lock.withLock { stored = nil }
    }
}

private final class FakeRegistrationClient: DeviceRegistrationClient, @unchecked Sendable {
    private let lock = NSLock()
    private let tokenStore: TokenStore?
    private let deviceIdProvider: DeviceIdProviding

    private(set) var registerCallCount = 0
    private(set) var unregisterTokenPresence: [Bool] = []

    init(deviceIdProvider: DeviceIdProviding, tokenStore: TokenStore? = nil) {
        self.deviceIdProvider = deviceIdProvider
        self.tokenStore = tokenStore
    }

    func register(_: RegisterDeviceRequest) async -> ApiResult<Void> {
        lock.withLock { registerCallCount += 1 }
        return .success(())
    }

    func unregister(deviceId _: String) async -> ApiResult<Void> {
        let live = tokenStore?.current()?.accessToken != nil
        lock.withLock { unregisterTokenPresence.append(live) }
        return .success(())
    }
}
