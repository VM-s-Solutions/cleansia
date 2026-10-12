import XCTest
@testable import CleansiaCore

/// Every sign-in wipes the session caches before it stores the new tokens, so nothing a previous account left
/// behind — an interrupted sign-out, a read that answered after it — reaches the next one (S11). Each cache
/// records the access token still stored when it is cleared: the previous session's, never the new one.
/// → /mobile-app/patterns#session-wipe
final class SignInWipeTests: XCTestCase {
    private let previousToken = "previous-access"
    private var store: PreviousSessionTokenStore!
    private var cache: RecordingCache!

    override func setUp() {
        super.setUp()
        MockURLProtocol.recorder.reset()
        store = PreviousSessionTokenStore(accessToken: previousToken)
        cache = RecordingCache(store: store)
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
        cache = nil
        store = nil
        super.tearDown()
    }

    func testALoginWipesTheSessionCachesBeforeItStoresTheNewTokens() async throws {
        let client = try makeClient()
        answer(isEmailConfirmed: true)

        _ = await client.login(email: "a@b.cz", password: "pw")

        await assertWipedBeforeTheNewTokens()
    }

    func testAConfirmedEmailWipesTheSessionCachesBeforeItStoresTheNewTokens() async throws {
        let client = try makeClient()
        answer(isEmailConfirmed: true)

        _ = await client.confirmEmail(email: "a@b.cz", code: "123456")

        await assertWipedBeforeTheNewTokens()
    }

    func testAGoogleSignInWipesTheSessionCachesBeforeItStoresTheNewTokens() async throws {
        let client = try makeClient()
        answer(isEmailConfirmed: true)

        _ = await client.googleAuth(GoogleAuthRequest(
            token: "t",
            googleId: "g",
            email: "a@b.cz",
            firstName: "A",
            lastName: "B",
            termsAccepted: false,
            countryId: nil
        ))

        await assertWipedBeforeTheNewTokens()
    }

    func testAnAppleSignInWipesTheSessionCachesBeforeItStoresTheNewTokens() async throws {
        let client = try makeClient()
        answer(isEmailConfirmed: true)

        _ = await client.appleAuth(AppleAuthRequest(
            identityToken: "t",
            rawNonce: "n",
            firstName: nil,
            lastName: nil,
            termsAccepted: false,
            countryId: nil
        ))

        await assertWipedBeforeTheNewTokens()
    }

    /// An unconfirmed address still stores the token it is given, so it starts a session like any other.
    func testAnUnconfirmedLoginThatStoresATokenWipesFirstToo() async throws {
        let client = try makeClient()
        answer(isEmailConfirmed: false)

        let result = await client.login(email: "a@b.cz", password: "pw")

        guard case .success(.unverifiedEmail(_, hasToken: true)) = result else {
            return XCTFail("expected an unverified login carrying a token, got \(result)")
        }
        await assertWipedBeforeTheNewTokens()
    }

    func testALoginThatStoresNoTokenWipesNothing() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8)) }

        _ = await client.login(email: "a@b.cz", password: "pw")

        let seen = await cache.tokensSeenAtClear
        XCTAssertTrue(seen.isEmpty)
        XCTAssertEqual(store.current()?.accessToken, previousToken)
    }

    private func assertWipedBeforeTheNewTokens(file: StaticString = #filePath, line: UInt = #line) async {
        let seen = await cache.tokensSeenAtClear
        XCTAssertEqual(seen, [previousToken], "not wiped once, ahead of the new tokens", file: file, line: line)
        XCTAssertNotEqual(store.current()?.accessToken, previousToken, "no new tokens stored", file: file, line: line)
    }

    private func answer(isEmailConfirmed: Bool) {
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = #"{"token":"\#(access)","isEmailConfirmed":\#(isEmailConfirmed),"refreshToken":"r2","#
                + #""refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}"#
            return (200, Data(body.utf8))
        }
    }

    private func makeClient() throws -> AuthApiClient {
        let caches = SessionScopedCacheRegistry()
        caches.register(cache)
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(
                deviceIdProvider: SignInWipeDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            sessionScopedCaches: caches,
            authedSession: session,
            noAuthSession: session
        )
    }
}

private actor RecordingCache: SessionScopedCache {
    private let store: TokenStore
    private(set) var tokensSeenAtClear: [String?] = []

    init(store: TokenStore) {
        self.store = store
    }

    func clear() async {
        tokensSeenAtClear.append(store.current()?.accessToken)
    }
}

private struct SignInWipeDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private final class PreviousSessionTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

    init(accessToken: String) {
        let future = Date(timeIntervalSinceNow: 9999)
        stored = AuthTokens(
            accessToken: accessToken,
            accessTokenExpiresAt: future,
            refreshToken: "previous-refresh",
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
