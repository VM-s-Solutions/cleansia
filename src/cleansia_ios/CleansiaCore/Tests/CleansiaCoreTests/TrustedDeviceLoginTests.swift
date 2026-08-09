import XCTest
@testable import CleansiaCore

/// Someone locked out after fumbling their own password on their own handset is let through by a
/// still-valid refresh token from a previous session — the bypass the browser has always had through
/// its refresh cookie. The handset presents what it stored and decides nothing: the server hashes it,
/// looks the refresh token up and requires the row to be unrevoked, unexpired and bound to the
/// account being signed into.
///
/// Every wire assertion reads the bytes the production client handed to `URLSession`, so the
/// serialization is the shipping one — a test-local encoder would prove nothing about whether the key
/// reaches the wire at all.
final class TrustedDeviceLoginTests: XCTestCase {
    private let storedRefresh = "stored-refresh"

    override func setUp() {
        super.setUp()
        MockURLProtocol.recorder.reset()
        MockURLProtocol.handler = Self.authSurfaceHandler
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
        super.tearDown()
    }

    func testMarkerIsTheStoredRefreshTokenAndNotTheAccessToken() {
        XCTAssertEqual(tokens(refreshToken: storedRefresh).trustedDeviceToken, storedRefresh)
    }

    func testMarkerIsAbsentWhenTheStoredRefreshTokenIsEmpty() {
        XCTAssertNil(tokens(refreshToken: "").trustedDeviceToken)
    }

    func testMarkerIsAbsentWhenTheStoredRefreshTokenIsWhitespace() {
        XCTAssertNil(tokens(refreshToken: "   ").trustedDeviceToken)
    }

    func testLoginSendsTheStoredRefreshTokenAsTheTrustedDeviceMarker() async throws {
        let store = MemoryTokenStore()
        store.save(tokens(refreshToken: storedRefresh))
        let client = try makeClient(store: store)

        _ = await client.login(email: "a@b.cz", password: "pw")

        XCTAssertEqual(try loginField("trustedDeviceToken"), storedRefresh)
    }

    func testLoginOmitsTheMarkerKeyEntirelyWhenNoSessionIsStored() async throws {
        let client = try makeClient(store: MemoryTokenStore())

        _ = await client.login(email: "a@b.cz", password: "pw")

        let wire = try wireBody(ofPath: "/api/Auth/Login")
        XCTAssertFalse(wire.contains("trustedDeviceToken"), wire)
    }

    /// `AuthApiClient.persist` stores `dto.refreshToken ?? ""`, so a blank value is reachable in the
    /// shared store. Blank must read as "no previous session" — an empty string is a value that
    /// matches nothing server-side and so reads as a FAILED trusted-device attempt.
    func testLoginOmitsTheMarkerKeyEntirelyWhenTheStoredRefreshTokenIsBlank() async throws {
        let store = MemoryTokenStore()
        store.save(tokens(refreshToken: ""))
        let client = try makeClient(store: store)

        _ = await client.login(email: "a@b.cz", password: "pw")

        let wire = try wireBody(ofPath: "/api/Auth/Login")
        XCTAssertFalse(wire.contains("trustedDeviceToken"), wire)
    }

    func testLoginMarkerIsNotTheAccessTokenOrTheSubmittedCredentials() async throws {
        let store = MemoryTokenStore()
        store.save(tokens(refreshToken: storedRefresh))
        let client = try makeClient(store: store)

        _ = await client.login(email: "a@b.cz", password: "submitted-password")

        let marker = try loginField("trustedDeviceToken")
        XCTAssertEqual(marker, storedRefresh)
        XCTAssertNotEqual(marker, try loginField("password"))
        XCTAssertNotEqual(marker, try loginField("email"))
        XCTAssertNotEqual(marker, "stored-access")
    }

    /// The real defect shape: the store is populated, so every request the client can send is a live
    /// candidate carrier. `RefreshToken` and `Logout` are excluded from the value check only —
    /// presenting the refresh token IS what those two endpoints are for.
    func testTheMarkerRidesTheLoginBodyAndNoOtherAuthRequest() async throws {
        let store = MemoryTokenStore()
        store.save(tokens(refreshToken: storedRefresh))
        let client = try makeClient(store: store)

        _ = await client.register(
            email: "a@b.cz", password: "pw", firstName: "Ada", lastName: "Lovelace", language: "en"
        )
        _ = await client.confirmEmail(email: "a@b.cz", code: "123456")
        _ = await client.resendConfirmation(email: "a@b.cz", language: "en")
        _ = await client.forgotPassword(email: "a@b.cz", language: "en")
        _ = await client.googleAuth(GoogleAuthRequest(
            token: "id-token", googleId: "g-1", email: "a@b.cz",
            firstName: "Ada", lastName: "Lovelace", termsAccepted: true
        ))
        _ = await client.appleAuth(AppleAuthRequest(
            identityToken: "identity", rawNonce: "nonce",
            firstName: "Ada", lastName: "Lovelace", termsAccepted: true
        ))
        _ = await client.refresh(refreshToken: storedRefresh)
        await client.logout()

        let presentsTheTokenByDesign = ["/api/Auth/RefreshToken", "/api/Auth/Logout"]
        for path in Self.nonLoginAuthPaths {
            let wire = try wireBody(ofPath: path)
            XCTAssertFalse(wire.contains("trustedDeviceToken"), "\(path) carries the marker: \(wire)")
            if !presentsTheTokenByDesign.contains(path) {
                XCTAssertFalse(wire.contains(storedRefresh), "\(path) leaks the stored token: \(wire)")
            }
        }
    }

    private static let nonLoginAuthPaths = [
        "/api/Auth/RegisterEmployee",
        "/api/Auth/ConfirmUserEmail",
        "/api/Auth/ResendConfirmationEmail",
        "/api/Auth/ForgotPassword",
        "/api/Auth/GoogleAuth",
        "/api/Auth/AppleAuth",
        "/api/Auth/RefreshToken",
        "/api/Auth/Logout"
    ]

    private func tokens(refreshToken: String) -> AuthTokens {
        AuthTokens(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: refreshToken,
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        )
    }

    private func makeClient(store: TokenStore) throws -> AuthApiClient {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(deviceIdProvider: TrustedDeviceFixedId()),
            sessionScopedCaches: SessionScopedCacheRegistry(),
            authedSession: session,
            noAuthSession: session
        )
    }

    private func wireBody(ofPath path: String) throws -> String {
        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: path), "no request to \(path)")
        let data = try XCTUnwrap(MockURLProtocol.body(of: request), "no body on \(path)")
        return try XCTUnwrap(String(data: data, encoding: .utf8))
    }

    private func loginField(_ name: String) throws -> String? {
        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "/api/Auth/Login"))
        let data = try XCTUnwrap(MockURLProtocol.body(of: request))
        let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
        return json?[name] as? String
    }

    /// Only `Login` answers with a session; everything else answers without one, so a call later in
    /// the leak sweep cannot overwrite the store the sweep depends on.
    private static let authSurfaceHandler: (URLRequest) -> (Int, Data) = { request in
        switch request.url?.path ?? "" {
        case "/api/Auth/Login":
            let access = JwtFactory.make(exp: 9_999_999_999)
            let body = """
            {"token":"\(access)","isEmailConfirmed":true,"refreshToken":"stored-refresh",\
            "refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        case "/api/Auth/ForgotPassword":
            return (204, Data())
        case "/api/Auth/RegisterEmployee", "/api/Auth/Register", "/api/Auth/ResendConfirmationEmail":
            return (200, Data("true".utf8))
        default:
            return (200, Data(#"{"token":"","isEmailConfirmed":false}"#.utf8))
        }
    }
}

private struct TrustedDeviceFixedId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private final class MemoryTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

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
