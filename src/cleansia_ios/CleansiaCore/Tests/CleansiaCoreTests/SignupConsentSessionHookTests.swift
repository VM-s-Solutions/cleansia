import XCTest
@testable import CleansiaCore

/// Which auth answers hand the parked tick a session, and whose address they hand it.
///
/// The address is the one load-bearing detail: every one of these calls also knows an
/// address the CALLER supplied, and using it would look correct on a happy path while
/// letting one account's session record another account's consent.
final class SignupConsentSessionHookTests: XCTestCase {
    private var delivery: RecordingDelivery!

    override func setUp() {
        super.setUp()
        delivery = RecordingDelivery()
        MockURLProtocol.recorder.reset()
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
        super.tearDown()
    }

    func testAConfirmedLoginDeliversTheAddressTheServerNamedNotTheOneTyped() async throws {
        let client = try makeClient()
        respondWithSession(email: "server@example.com", isEmailConfirmed: true)

        _ = await client.login(email: "typed@example.com", password: "pw")

        XCTAssertEqual(delivery.sessionEmails, ["server@example.com"])
    }

    func testConfirmingAnEmailDeliversTheAddressTheServerNamed() async throws {
        let client = try makeClient()
        respondWithSession(email: "server@example.com", isEmailConfirmed: true)

        _ = await client.confirmEmail(email: "typed@example.com", code: "123456")

        XCTAssertEqual(delivery.sessionEmails, ["server@example.com"])
    }

    /// Apple hides the address behind a relay and hands the client nothing to fall back on,
    /// so the token response is the only place the account is ever named.
    func testAppleSignInDeliversTheAddressTheServerNamed() async throws {
        let client = try makeClient()
        respondWithSession(email: "relay@privaterelay.appleid.com", isEmailConfirmed: true)

        _ = await client.appleAuth(identityToken: "t", rawNonce: "n", firstName: nil, lastName: nil)

        XCTAssertEqual(delivery.sessionEmails, ["relay@privaterelay.appleid.com"])
    }

    func testGoogleSignInDeliversTheAddressTheServerNamed() async throws {
        let client = try makeClient()
        respondWithSession(email: "server@example.com", isEmailConfirmed: true)

        _ = await client.googleAuth(
            token: "t",
            googleId: "g",
            email: "typed@example.com",
            firstName: "Ada",
            lastName: "Lovelace"
        )

        XCTAssertEqual(delivery.sessionEmails, ["server@example.com"])
    }

    /// No token means no session, so there is nothing to record against and the tick stays parked.
    func testAnAnswerWithNoTokenDeliversNothing() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in
            (200, Data(#"{"token":"","isEmailConfirmed":false,"email":"server@example.com"}"#.utf8))
        }

        _ = await client.login(email: "typed@example.com", password: "pw")

        XCTAssertEqual(delivery.sessionEmails, [])
    }

    func testARejectedLoginDeliversNothing() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in (401, Data(#"{"title":"Unauthorized"}"#.utf8)) }

        _ = await client.login(email: "typed@example.com", password: "pw")

        XCTAssertEqual(delivery.sessionEmails, [])
    }

    /// Registration answers a bare boolean and mints no session: there is nothing to deliver into.
    func testRegistrationDeliversNothing() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in (200, Data("true".utf8)) }

        _ = await client.register(
            email: "typed@example.com",
            password: "pw",
            firstName: "Ada",
            lastName: "Lovelace",
            language: "en"
        )

        XCTAssertEqual(delivery.sessionEmails, [])
    }

    /// A body with no address is not an identity, and guessing one from the form is the defect.
    func testASessionWithNoAddressNeverSubstitutesTheTypedOne() async throws {
        let client = try makeClient()
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = """
            {"token":"\(access)","isEmailConfirmed":true,"refreshToken":"r1",\
            "refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        }

        _ = await client.login(email: "typed@example.com", password: "pw")

        XCTAssertEqual(delivery.sessionEmails, [nil])
    }

    private func respondWithSession(email: String, isEmailConfirmed: Bool) {
        let access = JwtFactory.make(exp: 9_999_999_999)
        MockURLProtocol.handler = { _ in
            let body = """
            {"token":"\(access)","isEmailConfirmed":\(isEmailConfirmed),"email":"\(email)",\
            "refreshToken":"r1","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """
            return (200, Data(body.utf8))
        }
    }

    private func makeClient() throws -> AuthApiClient {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: HookMemTokenStore(),
            headerAdapter: HeaderAdapter(deviceIdProvider: HookDeviceId()),
            sessionScopedCaches: SessionScopedCacheRegistry(),
            signupConsent: delivery,
            authedSession: session,
            noAuthSession: session
        )
    }
}

private final class RecordingDelivery: SignupConsentDelivering, @unchecked Sendable {
    private let lock = NSLock()
    private var received: [String?] = []

    var sessionEmails: [String?] {
        lock.withLock { received }
    }

    func deliver(sessionEmail: String?) async {
        lock.withLock { received.append(sessionEmail) }
    }
}

private struct HookDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-hook"
    }
}

private final class HookMemTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var tokens: AuthTokens?

    func current() -> AuthTokens? {
        lock.withLock { tokens }
    }

    func save(_ tokens: AuthTokens) {
        lock.withLock { self.tokens = tokens }
    }

    func clear() {
        lock.withLock { tokens = nil }
    }
}
