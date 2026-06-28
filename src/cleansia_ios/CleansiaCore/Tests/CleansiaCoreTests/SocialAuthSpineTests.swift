import XCTest
@testable import CleansiaCore

final class SocialAuthSpineTests: XCTestCase {
    override func setUp() {
        super.setUp()
        MockURLProtocol.recorder.reset()
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
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
            registerEndpoint: .customer,
            authedSession: session,
            noAuthSession: session
        )
    }

    func testGoogleAuthPostsContractBodyToGoogleAuthPath() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8)) }

        _ = await client.googleAuth(
            token: "g-id-token",
            googleId: "g-123",
            email: "a@b.cz",
            firstName: "Jana",
            lastName: "Nováková"
        )

        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "GoogleAuth"))
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.url?.path, "/api/Auth/GoogleAuth")
        let body = try decodeBody(request)
        XCTAssertEqual(body["token"] as? String, "g-id-token")
        XCTAssertEqual(body["googleId"] as? String, "g-123")
        XCTAssertEqual(body["email"] as? String, "a@b.cz")
        XCTAssertEqual(body["firstName"] as? String, "Jana")
        XCTAssertEqual(body["lastName"] as? String, "Nováková")
    }

    func testAppleAuthPostsContractBodyToAppleAuthPath() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8)) }

        _ = await client.appleAuth(
            identityToken: "apple-identity-token",
            rawNonce: "raw-nonce-xyz",
            firstName: "Jan",
            lastName: "Novák"
        )

        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "AppleAuth"))
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertEqual(request.url?.path, "/api/Auth/AppleAuth")
        let body = try decodeBody(request)
        XCTAssertEqual(body["identityToken"] as? String, "apple-identity-token")
        XCTAssertEqual(body["rawNonce"] as? String, "raw-nonce-xyz")
        XCTAssertEqual(body["firstName"] as? String, "Jan")
        XCTAssertEqual(body["lastName"] as? String, "Novák")
    }

    func testSocialAuthPathsNeverCarryBearerEvenWithStoredToken() async throws {
        let store = MemTokenStore()
        store.save(.init(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "r1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        ))
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8)) }

        _ = await client.googleAuth(token: "t", googleId: "g", email: "a@b.cz", firstName: "A", lastName: "B")
        _ = await client.appleAuth(identityToken: "t", rawNonce: "n", firstName: nil, lastName: nil)

        for path in ["GoogleAuth", "AppleAuth"] {
            let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: path))
            XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"), "\(path) must not carry a Bearer")
            XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        }
    }

    func testSocialAuthAuthenticatedPersistsViaTheSharedGate() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"\#(access)","isEmailConfirmed":true,"refreshToken":"r1"}"#.utf8))
        }

        let result = await client.googleAuth(
            token: "t", googleId: "g", email: "a@b.cz", firstName: "A", lastName: "B"
        )

        guard case .success(.authenticated) = result else { return XCTFail("expected authenticated") }
        XCTAssertEqual(store.current()?.accessToken, access)
        XCTAssertEqual(store.current()?.refreshToken, "r1")
    }

    func testSocialAuthUnverifiedEmailRoutesThroughGateWithoutPersisting() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"","isEmailConfirmed":false,"email":"a@b.cz"}"#.utf8))
        }

        let result = await client.appleAuth(
            identityToken: "t", rawNonce: "n", firstName: nil, lastName: nil
        )

        guard case let .success(.unverifiedEmail(email, hasToken)) = result else {
            return XCTFail("expected unverifiedEmail")
        }
        XCTAssertEqual(email, "a@b.cz")
        XCTAssertFalse(hasToken)
        XCTAssertNil(store.current())
    }

    func testSocialAuthFailurePropagatesError() async throws {
        let store = MemTokenStore()
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (401, Data(#"{"errorCode":"auth.invalid_apple_user_token"}"#.utf8)) }

        let result = await client.appleAuth(
            identityToken: "t", rawNonce: "n", firstName: nil, lastName: nil
        )

        guard case let .failure(error) = result else { return XCTFail("expected failure") }
        XCTAssertEqual(error.code, "auth.invalid_apple_user_token")
    }

    private func decodeBody(_ request: URLRequest) throws -> [String: Any] {
        let data = try XCTUnwrap(MockURLProtocol.body(of: request))
        let object = try JSONSerialization.jsonObject(with: data)
        return try XCTUnwrap(object as? [String: Any])
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
