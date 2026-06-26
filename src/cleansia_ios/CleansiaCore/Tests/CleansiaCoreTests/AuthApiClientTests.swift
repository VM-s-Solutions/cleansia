import XCTest
@testable import CleansiaCore

final class AuthApiClientTests: XCTestCase {
    override func tearDown() {
        MockURLProtocol.handler = nil
        super.tearDown()
    }

    private func makeClient(store: TokenStore) throws -> AuthApiClient {
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

enum JwtFactory {
    static func make(exp: Int) -> String {
        let header = base64URL(Data(#"{"alg":"HS256","typ":"JWT"}"#.utf8))
        let payload = base64URL(Data(#"{"exp":\#(exp)}"#.utf8))
        return "\(header).\(payload).sig"
    }

    private static func base64URL(_ data: Data) -> String {
        data.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }
}

final class MockURLProtocol: URLProtocol {
    static var handler: ((URLRequest) -> (Int, Data))?

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let handler = MockURLProtocol.handler, let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        let (status, data) = handler(request)
        guard let response = HTTPURLResponse(
            url: url,
            statusCode: status,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        ) else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
