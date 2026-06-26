import XCTest
@testable import CleansiaCore

final class AuthTokensTests: XCTestCase {
    private func tokens(
        accessExpiry: Date,
        refreshExpiry: Date
    ) -> AuthTokens {
        AuthTokens(
            accessToken: "access",
            accessTokenExpiresAt: accessExpiry,
            refreshToken: "refresh",
            refreshTokenExpiresAt: refreshExpiry
        )
    }

    func testAccessExpiredWhenNowAtOrPastExpiry() {
        let now = Date(timeIntervalSince1970: 1000)
        let expired = tokens(
            accessExpiry: Date(timeIntervalSince1970: 1000),
            refreshExpiry: Date(timeIntervalSince1970: 5000)
        )
        XCTAssertTrue(expired.isAccessExpired(now: now))
    }

    func testAccessNotExpiredBeforeExpiry() {
        let now = Date(timeIntervalSince1970: 999)
        let live = tokens(
            accessExpiry: Date(timeIntervalSince1970: 1000),
            refreshExpiry: Date(timeIntervalSince1970: 5000)
        )
        XCTAssertFalse(live.isAccessExpired(now: now))
    }

    func testRefreshExpiredWhenNowPastRefreshExpiry() {
        let now = Date(timeIntervalSince1970: 6000)
        let dead = tokens(
            accessExpiry: Date(timeIntervalSince1970: 1000),
            refreshExpiry: Date(timeIntervalSince1970: 5000)
        )
        XCTAssertTrue(dead.isRefreshExpired(now: now))
    }
}

final class JwtDecoderTests: XCTestCase {
    private func makeJwt(payload: [String: Any]) throws -> String {
        let header = base64URL(Data(#"{"alg":"HS256","typ":"JWT"}"#.utf8))
        let body = try base64URL(JSONSerialization.data(withJSONObject: payload))
        return "\(header).\(body).signature"
    }

    private func base64URL(_ data: Data) -> String {
        data.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }

    func testExtractsExpiryFromExpClaim() throws {
        let jwt = try makeJwt(payload: ["exp": 1_700_000_000])
        let date = JwtDecoder.expiry(of: jwt)
        XCTAssertEqual(date, Date(timeIntervalSince1970: 1_700_000_000))
    }

    func testReturnsNilForMalformedJwt() {
        XCTAssertNil(JwtDecoder.expiry(of: "not-a-jwt"))
        XCTAssertNil(JwtDecoder.expiry(of: "only.two"))
    }

    func testReturnsNilWhenExpMissing() throws {
        let jwt = try makeJwt(payload: ["sub": "user-1"])
        XCTAssertNil(JwtDecoder.expiry(of: jwt))
    }
}

final class HeaderAdapterTests: XCTestCase {
    private func adapter(deviceId: String = "device-123") -> HeaderAdapter {
        HeaderAdapter(
            deviceIdProvider: StubDeviceIdProvider(deviceId: deviceId),
            deviceLabel: "iPhone15,2 - iOS 17.0",
            timeZoneIdentifier: { "Europe/Prague" }
        )
    }

    func testStampsDeviceMetadataHeaders() throws {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test/api/Order/Get")))
        adapter().apply(to: &request, accessToken: nil)

        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-123")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Label"), "iPhone15,2 - iOS 17.0")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Time-Zone"), "Europe/Prague")
    }

    func testStampsBearerOnAuthedEndpoint() throws {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test/api/Order/Get")))
        adapter().apply(to: &request, accessToken: "tok")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer tok")
    }

    func testOmitsBearerOnAnonymousEndpoints() throws {
        let paths = [
            "/api/Auth/Login",
            "/api/Auth/Register",
            "/api/Auth/RefreshToken",
            "/api/Auth/GoogleAuth",
            "/api/Auth/ConfirmUserEmail",
            "/api/Auth/ResendConfirmationEmail"
        ]
        for path in paths {
            var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test\(path)")))
            adapter().apply(to: &request, accessToken: "tok")
            XCTAssertNil(
                request.value(forHTTPHeaderField: "Authorization"),
                "expected no Bearer on \(path)"
            )
        }
    }

    func testAnonMatchIsCaseInsensitive() throws {
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test/api/auth/login")))
        adapter().apply(to: &request, accessToken: "tok")
        XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"))
    }

    func testDeviceIdIsAsciiSanitizedAndTruncated() throws {
        let dirty = "abc\u{00A0}déf" + String(repeating: "x", count: 80)
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://api.test/api/Order")))
        adapter(deviceId: dirty).apply(to: &request, accessToken: nil)

        let stamped = request.value(forHTTPHeaderField: "X-Device-Id") ?? ""
        XCTAssertLessThanOrEqual(stamped.count, 64)
        XCTAssertTrue(stamped.allSatisfy(\.isASCII))
        XCTAssertFalse(stamped.contains("\u{00A0}"))
    }
}

final class SessionRefresherTests: XCTestCase {
    func testSingleFlightCoalescesConcurrentRefreshes() async {
        let store = InMemoryTokenStore()
        store.save(.init(
            accessToken: "old",
            accessTokenExpiresAt: Date(timeIntervalSince1970: 0),
            refreshToken: "r0",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = CountingRefreshClient(
            result: RefreshedTokens(
                accessToken: "new",
                accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
                refreshToken: "r1",
                refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
            )
        )
        let sessionManager = await SessionManager()
        let refresher = SessionRefresher(
            tokenStore: store,
            refreshClient: client,
            sessionManager: sessionManager,
            sessionScopedCaches: SessionScopedCacheRegistry()
        )

        async let first = refresher.refresh(triggeredBy: "old")
        async let second = refresher.refresh(triggeredBy: "old")
        async let third = refresher.refresh(triggeredBy: "old")
        let results = await [first, second, third]

        XCTAssertEqual(client.callCount, 1)
        XCTAssertTrue(results.allSatisfy { if case .refreshed = $0 { return true }
            return false
        })
        XCTAssertEqual(store.current()?.accessToken, "new")
        XCTAssertEqual(store.current()?.refreshToken, "r1")
    }

    func testStaleTriggerReusesAlreadyRefreshedToken() async {
        let store = InMemoryTokenStore()
        store.save(.init(
            accessToken: "current",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = CountingRefreshClient(result: nil)
        let sessionManager = await SessionManager()
        let refresher = SessionRefresher(
            tokenStore: store,
            refreshClient: client,
            sessionManager: sessionManager,
            sessionScopedCaches: SessionScopedCacheRegistry()
        )

        let outcome = await refresher.refresh(triggeredBy: "stale-access")

        XCTAssertEqual(client.callCount, 0)
        guard case let .refreshed(tokens) = outcome else {
            return XCTFail("expected reuse of current token")
        }
        XCTAssertEqual(tokens.accessToken, "current")
    }

    func testRefreshFailureSignsOutAndClearsCaches() async {
        let store = InMemoryTokenStore()
        store.save(.init(
            accessToken: "old",
            accessTokenExpiresAt: Date(timeIntervalSince1970: 0),
            refreshToken: "r0",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = CountingRefreshClient(result: nil)
        let sessionManager = await SessionManager()
        let registry = SessionScopedCacheRegistry()
        let cache = SpyCache()
        registry.register(cache)
        let refresher = SessionRefresher(
            tokenStore: store,
            refreshClient: client,
            sessionManager: sessionManager,
            sessionScopedCaches: registry
        )

        let outcome = await refresher.refresh(triggeredBy: "old")

        guard case .signedOut = outcome else { return XCTFail("expected sign-out") }
        XCTAssertNil(store.current())
        XCTAssertEqual(cache.clearCount, 1)
        let reason = await sessionManager.lastReasonForTest
        XCTAssertEqual(reason, .sessionExpired)
    }

    func testExpiredRefreshTokenSignsOutWithoutCallingServer() async {
        let store = InMemoryTokenStore()
        store.save(.init(
            accessToken: "old",
            accessTokenExpiresAt: Date(timeIntervalSince1970: 0),
            refreshToken: "r0",
            refreshTokenExpiresAt: Date(timeIntervalSince1970: 0)
        ))
        let client = CountingRefreshClient(result: nil)
        let sessionManager = await SessionManager()
        let refresher = SessionRefresher(
            tokenStore: store,
            refreshClient: client,
            sessionManager: sessionManager,
            sessionScopedCaches: SessionScopedCacheRegistry()
        )

        let outcome = await refresher.refresh(triggeredBy: "old")

        XCTAssertEqual(client.callCount, 0)
        guard case .signedOut = outcome else { return XCTFail("expected sign-out") }
        XCTAssertNil(store.current())
    }
}

@MainActor
final class SessionManagerForcedSignOutTests: XCTestCase {
    func testForcedSignOutPublishesSignal() async {
        let manager = SessionManager()
        var received: ForcedSignOutReason?
        let task = Task {
            for await reason in manager.forcedSignOutStream {
                received = reason
                break
            }
        }
        try? await Task.sleep(nanoseconds: 10_000_000)
        manager.emitForcedSignOut(.compromised)
        _ = await task.value
        XCTAssertEqual(received, .compromised)
    }
}

private struct StubDeviceIdProvider: DeviceIdProviding {
    let deviceId: String
}

private final class InMemoryTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

    func current() -> AuthTokens? {
        lock.lock()
        defer { lock.unlock() }
        return stored
    }

    func save(_ tokens: AuthTokens) {
        lock.lock()
        defer { lock.unlock() }
        stored = tokens
    }

    func clear() {
        lock.lock()
        defer { lock.unlock() }
        stored = nil
    }
}

private final class CountingRefreshClient: AuthRefreshing, @unchecked Sendable {
    private let lock = NSLock()
    private let result: RefreshedTokens?
    private var calls = 0

    init(result: RefreshedTokens?) {
        self.result = result
    }

    var callCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return calls
    }

    func refresh(refreshToken _: String) async -> RefreshedTokens? {
        lock.lock()
        calls += 1
        lock.unlock()
        try? await Task.sleep(nanoseconds: 5_000_000)
        return result
    }
}

private final class SpyCache: SessionScopedCache, @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0
    var clearCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return count
    }

    func clear() async {
        lock.lock()
        count += 1
        lock.unlock()
    }
}
