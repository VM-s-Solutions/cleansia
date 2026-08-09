import CleansiaCore
import XCTest
@testable import CleansiaPartner

/// A cleaner locked out after fumbling their own password on their own handset is let through by a
/// still-valid refresh token from a previous session. This drives the partner app's REAL sign-in
/// screen through the auth spine it is wired to, and asserts the bytes that reach the wire — the
/// partner app has no login request of its own, so the only way to know its login carries the marker
/// is to send one.
@MainActor
final class PartnerTrustedDeviceLoginTests: XCTestCase {
    private var suiteName: String!
    private var defaults: UserDefaults!
    private var tokenStore: SeededTokenStore!
    private var spine: AuthApiClient!

    private let storedRefresh = "stored-refresh"

    override func setUpWithError() throws {
        try super.setUpWithError()
        AuthWireRecorder.reset()
        suiteName = "PartnerTrustedDeviceLoginTests.\(UUID().uuidString)"
        defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        tokenStore = SeededTokenStore()

        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [AuthWireRecorder.self]
        let session = URLSession(configuration: config)
        spine = try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: tokenStore,
            headerAdapter: HeaderAdapter(
                deviceIdProvider: PartnerTrustedDeviceId(),
                anonymousAllowList: .partner
            ),
            sessionScopedCaches: SessionScopedCacheRegistry(),
            authedSession: session,
            noAuthSession: session
        )
    }

    override func tearDown() {
        AuthWireRecorder.reset()
        defaults.removePersistentDomain(forName: suiteName)
        super.tearDown()
    }

    func testSigningInSendsTheStoredRefreshTokenAsTheTrustedDeviceMarker() async throws {
        tokenStore.save(tokens(refreshToken: storedRefresh))

        await signIn()

        let wire = try XCTUnwrap(AuthWireRecorder.body(ofPath: "/api/Auth/Login"))
        XCTAssertTrue(wire.contains("\"trustedDeviceToken\":\"\(storedRefresh)\""), wire)
    }

    func testSigningInWithNoStoredSessionLeavesTheMarkerOffTheWireEntirely() async throws {
        await signIn()

        let wire = try XCTUnwrap(AuthWireRecorder.body(ofPath: "/api/Auth/Login"))
        XCTAssertFalse(wire.contains("trustedDeviceToken"), wire)
    }

    func testSigningInWithABlankStoredRefreshTokenLeavesTheMarkerOffTheWireEntirely() async throws {
        tokenStore.save(tokens(refreshToken: ""))

        await signIn()

        let wire = try XCTUnwrap(AuthWireRecorder.body(ofPath: "/api/Auth/Login"))
        XCTAssertFalse(wire.contains("trustedDeviceToken"), wire)
    }

    /// The leak in its real shape: a populated store and a DIFFERENT request from the app's own
    /// sign-up screen, which reaches the partner-only `RegisterEmployee` path.
    func testRegisteringFromAPopulatedStoreCarriesNoMarker() async throws {
        tokenStore.save(tokens(refreshToken: storedRefresh))

        await register()

        let wire = try XCTUnwrap(AuthWireRecorder.body(ofPath: "/api/Auth/RegisterEmployee"))
        XCTAssertFalse(wire.contains("trustedDeviceToken"), wire)
        XCTAssertFalse(wire.contains(storedRefresh), wire)
    }

    private func signIn() async {
        let viewModel = LoginViewModel(loginClient: spine, snackbar: SnackbarController())
        viewModel.onEmailChange("cleaner@example.com")
        viewModel.onPasswordChange("submitted-password")
        await viewModel.login()
    }

    private func register() async {
        let viewModel = RegisterViewModel(
            client: spine,
            settings: UserDefaultsAppSettingsStore(defaults: defaults),
            snackbar: SnackbarController(),
            signupConsent: RecordingSignupConsent()
        )
        viewModel.onFirstNameChange("Ada")
        viewModel.onLastNameChange("Lovelace")
        viewModel.onEmailChange("cleaner@example.com")
        viewModel.onPasswordChange("abcdefg1")
        viewModel.onConfirmPasswordChange("abcdefg1")
        viewModel.onAcceptTermsChange(true)
        await viewModel.register()
    }

    private func tokens(refreshToken: String) -> AuthTokens {
        AuthTokens(
            accessToken: "stored-access",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: refreshToken,
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        )
    }
}

private struct PartnerTrustedDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-partner"
    }
}

private final class SeededTokenStore: TokenStore, @unchecked Sendable {
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

/// Keeps the last body sent to each auth path so a test can assert on the bytes the production
/// encoder produced, rather than on a re-encoding of the request object.
final class AuthWireRecorder: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var bodies: [String: Data] = [:]

    static func reset() {
        lock.withLock { bodies.removeAll() }
    }

    static func body(ofPath path: String) -> String? {
        guard let data = lock.withLock({ bodies[path] }) else { return nil }
        return String(data: data, encoding: .utf8)
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        if let path = request.url?.path, let body = AuthWireRecorder.readBody(from: request) {
            AuthWireRecorder.lock.withLock { AuthWireRecorder.bodies[path] = body }
        }
        guard let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        let (status, data) = AuthWireRecorder.answer(for: request)
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

    private static func answer(for request: URLRequest) -> (Int, Data) {
        switch request.url?.path ?? "" {
        case "/api/Auth/Login":
            (200, Data("""
            {"token":"header.payload.signature","isEmailConfirmed":true,"userId":"u-1",
             "email":"cleaner@example.com","refreshToken":"r-1","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """.utf8))
        default:
            (200, Data("true".utf8))
        }
    }

    private static func readBody(from request: URLRequest) -> Data? {
        if let httpBody = request.httpBody { return httpBody }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let size = 4096
        var buffer = [UInt8](repeating: 0, count: size)
        while stream.hasBytesAvailable {
            let read = stream.read(&buffer, maxLength: size)
            if read <= 0 { break }
            data.append(buffer, count: read)
        }
        return data
    }
}
