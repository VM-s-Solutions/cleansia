import XCTest
@testable import CleansiaCore

final class AuthApiClientTests: XCTestCase {
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
        registerEndpoint: RegisterEndpoint = .employee
    ) throws -> AuthApiClient {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(
                deviceIdProvider: FixedDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            sessionScopedCaches: SessionScopedCacheRegistry(),
            registerEndpoint: registerEndpoint,
            authedSession: session,
            noAuthSession: session
        )
    }

    func testLoginPersistsTokensOnConfirmedSession() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = """
            {"token":"\(
                access
            )","isEmailConfirmed":true,"refreshToken":"r1","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        }

        let result = await client.login(email: "a@b.cz", password: "pw")

        guard case .success(.authenticated) = result else { return XCTFail("expected authenticated") }
        XCTAssertEqual(store.current()?.accessToken, access)
        XCTAssertEqual(store.current()?.refreshToken, "r1")
    }

    func testLoginWithUnconfirmedEmailDoesNotAuthenticate() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = """
            {"token":"\(
                access
            )","isEmailConfirmed":false,"refreshToken":"r1","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        }

        let result = await client.login(email: "a@b.cz", password: "pw")

        guard case let .success(.unverifiedEmail(_, hasToken)) = result else {
            return XCTFail("expected unverifiedEmail")
        }
        XCTAssertTrue(hasToken)
    }

    func testLoginWithEmptyTokenIsUnverifiedNoToken() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8))
        }

        let result = await client.login(email: "a@b.cz", password: "pw")

        guard case let .success(.unverifiedEmail(_, hasToken)) = result else {
            return XCTFail("expected unverifiedEmail")
        }
        XCTAssertFalse(hasToken)
        XCTAssertNil(store.current())
    }

    func testRefreshReplacesStoredRefreshToken() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = """
            {"token":"\(
                access
            )","isEmailConfirmed":true,"refreshToken":"r2","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        }

        let refreshed = await client.refresh(refreshToken: "r1")

        XCTAssertEqual(refreshed?.accessToken, access)
        XCTAssertEqual(refreshed?.refreshToken, "r2")
    }

    func testRefreshFailureReturnsNil() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (401, Data("{}".utf8)) }

        let refreshed = await client.refresh(refreshToken: "r1")

        XCTAssertNil(refreshed)
    }

    func testLogoutClearsTokensAndSessionCaches() async throws {
        let store = MemTokenStore()
        store.save(.init(
            accessToken: "a",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let registry = SessionScopedCacheRegistry()
        let cache = CountingCache()
        registry.register(cache)
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        MockURLProtocol.handler = { _ in (200, Data("{}".utf8)) }
        let client = try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(deviceIdProvider: FixedDeviceId()),
            sessionScopedCaches: registry,
            authedSession: session,
            noAuthSession: session
        )

        await client.logout()

        XCTAssertNil(store.current())
        XCTAssertEqual(cache.count, 1)
    }

    func testConfirmEmailIsSentAsPut() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"\#(access)","isEmailConfirmed":true,"refreshToken":"r1"}"#.utf8))
        }

        _ = await client.confirmEmail(email: "a@b.cz", code: "123456")

        let confirm = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "ConfirmUserEmail"))
        XCTAssertEqual(confirm.httpMethod, "PUT")
    }

    func testOtherAuthPathsStayPost() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        _ = await client.register(email: "a@b.cz", password: "pw", firstName: "A", lastName: "B", language: "en")
        _ = await client.resendConfirmation(email: "a@b.cz", language: "en")

        MockURLProtocol.handler = { _ in (204, Data()) }
        _ = await client.forgotPassword(email: "a@b.cz", language: "en")

        for path in ["RegisterEmployee", "ResendConfirmationEmail", "ForgotPassword"] {
            let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: path))
            XCTAssertEqual(request.httpMethod, "POST", "\(path) must be POST")
        }
    }

    func testConfirmEmailEmptyTokenIsUnverifiedNoTokenAndStoresNothing() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"","isEmailConfirmed":false,"email":"a@b.cz"}"#.utf8))
        }

        let result = await client.confirmEmail(email: "a@b.cz", code: "123456")

        guard case let .success(.unverifiedEmail(_, hasToken)) = result else {
            return XCTFail("expected unverifiedEmail")
        }
        XCTAssertFalse(hasToken)
        XCTAssertNil(store.current())
    }

    func testConfirmEmailWithTokenPersistsAndAuthenticates() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"\#(access)","isEmailConfirmed":true,"refreshToken":"r9"}"#.utf8))
        }

        let result = await client.confirmEmail(email: "a@b.cz", code: "123456")

        guard case .success(.authenticated) = result else { return XCTFail("expected authenticated") }
        XCTAssertEqual(store.current()?.accessToken, access)
        XCTAssertEqual(store.current()?.refreshToken, "r9")
    }

    func testConfirmEmailDoesNotAttachBearerEvenWithStoredToken() async throws {
        let store = MemTokenStore()
        store.save(.init(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8)) }

        _ = await client.confirmEmail(email: "a@b.cz", code: "123456")

        let confirm = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "ConfirmUserEmail"))
        XCTAssertNil(confirm.value(forHTTPHeaderField: "Authorization"))
        XCTAssertEqual(confirm.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        XCTAssertEqual(confirm.value(forHTTPHeaderField: "X-Time-Zone"), "Europe/Prague")
    }

    func testAnonAuthPathsNeverCarryBearer() async throws {
        let store = MemTokenStore()
        store.save(.init(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        _ = await client.register(email: "a@b.cz", password: "pw", firstName: "A", lastName: "B", language: "en")
        _ = await client.resendConfirmation(email: "a@b.cz", language: "en")
        MockURLProtocol.handler = { _ in (204, Data()) }
        _ = await client.forgotPassword(email: "a@b.cz", language: "en")

        for path in ["RegisterEmployee", "ResendConfirmationEmail", "ForgotPassword"] {
            let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: path))
            XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"), "\(path) must not carry a Bearer")
            XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        }
    }

    func testRegisterTargetsRegisterEmployeePath() async throws {
        let client = try makeClient(store: MemTokenStore())
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        let result = await client.register(
            email: "a@b.cz", password: "pw", firstName: "A", lastName: "B", language: "en"
        )

        guard case let .success(value) = result else { return XCTFail("expected success") }
        XCTAssertTrue(value)
        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "RegisterEmployee"))
        XCTAssertTrue(request.url?.path.contains("/api/Auth/RegisterEmployee") == true)
    }

    func testCustomerRegisterTargetsRegisterPathNotRegisterEmployee() async throws {
        let client = try makeClient(store: MemTokenStore(), registerEndpoint: .customer)
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        let result = await client.register(
            email: "a@b.cz", password: "pw", firstName: "A", lastName: "B", language: "en"
        )

        guard case let .success(value) = result else { return XCTFail("expected success") }
        XCTAssertTrue(value)
        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "Register"))
        XCTAssertEqual(request.url?.path, "/api/Auth/Register")
        XCTAssertFalse(request.url?.path.contains("RegisterEmployee") == true)
        XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"))
    }

    func testDefaultRegisterEndpointStaysEmployeeForPartnerByteEquivalence() async throws {
        let client = try makeClient(store: MemTokenStore())
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        _ = await client.register(
            email: "a@b.cz", password: "pw", firstName: "A", lastName: "B", language: "en"
        )

        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "Register"))
        XCTAssertEqual(request.url?.path, "/api/Auth/RegisterEmployee")
    }

    func testAuthedNonAnonPathCarriesBearerPositiveControl() async throws {
        let store = MemTokenStore()
        store.save(.init(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        await client.logout()

        let logout = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "Logout"))
        XCTAssertEqual(logout.value(forHTTPHeaderField: "Authorization"), "Bearer stored-access")
        XCTAssertEqual(logout.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
    }
}

private struct FixedDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private final class MemTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?
    func current() -> AuthTokens? {
        lock.lock()
        defer { lock.unlock() }
        return stored
    }

    func save(_ tokens: AuthTokens) {
        lock.lock()
        stored = tokens
        lock.unlock()
    }

    func clear() {
        lock.lock()
        stored = nil
        lock.unlock()
    }
}

private final class CountingCache: SessionScopedCache, @unchecked Sendable {
    private let lock = NSLock()
    private var calls = 0
    var count: Int {
        lock.lock()
        defer { lock.unlock() }
        return calls
    }

    func clear() async {
        lock.lock()
        calls += 1
        lock.unlock()
    }
}
