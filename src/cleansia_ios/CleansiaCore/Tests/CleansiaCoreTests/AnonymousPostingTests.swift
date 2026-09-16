import XCTest
@testable import CleansiaCore

/// The guest order calls are the first business endpoints to ride the hand-written spine rather than the
/// generated client, so the spine's anonymous post is pinned here at the wire: the body it sends, the
/// headers it does and does not attach, and what it makes of the server's answer.
final class AnonymousPostingTests: XCTestCase {
    private struct Credentials: Encodable {
        let displayOrderNumber: String
        let email: String
        let confirmationCode: String
    }

    private struct Receipt: Decodable, Equatable {
        let orderId: String?
        let refundInitiated: Bool?
        let actualRefundAmount: Double?
        let cleaningDateTime: Date?
    }

    private let credentials = Credentials(
        displayOrderNumber: "CZ-123",
        email: "guest@example.test",
        confirmationCode: "secret-code"
    )

    override func setUp() {
        super.setUp()
        MockURLProtocol.recorder.reset()
    }

    override func tearDown() {
        MockURLProtocol.handler = nil
        MockURLProtocol.recorder.reset()
        super.tearDown()
    }

    private func makeClient(store: TokenStore = PostingMemTokenStore()) throws -> AuthApiClient {
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [MockURLProtocol.self]
        let session = URLSession(configuration: config)
        return try AuthApiClient(
            apiBaseURL: XCTUnwrap(URL(string: "https://api.test/")),
            tokenStore: store,
            headerAdapter: HeaderAdapter(
                deviceIdProvider: PostingDeviceId(),
                anonymousAllowList: .customer,
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            sessionScopedCaches: SessionScopedCacheRegistry(),
            registerEndpoint: .customer,
            authedSession: session,
            noAuthSession: session
        )
    }

    func testTheCredentialsTravelInAJsonBodyAndNeverInTheQuery() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in (200, Data(#"{"orderId":"o-1","refundInitiated":false}"#.utf8)) }

        let result: ApiResult<Receipt> = await client.postAnonymous(
            path: "api/Order/CancelGuest",
            body: credentials
        )

        guard case .success = result else { return XCTFail("expected success, got \(result)") }
        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "/api/Order/CancelGuest"))
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertNil(request.url?.query)
        XCTAssertEqual(request.value(forHTTPHeaderField: "Content-Type"), "application/json")
        let body = try XCTUnwrap(MockURLProtocol.body(of: request))
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
        XCTAssertEqual(json["displayOrderNumber"] as? String, "CZ-123")
        XCTAssertEqual(json["email"] as? String, "guest@example.test")
        XCTAssertEqual(json["confirmationCode"] as? String, "secret-code")
    }

    /// A stored account session is irrelevant to a guest booking and must not ride beside its secret.
    func testAStoredSessionNeverAttachesABearerToAnAnonymousPost() async throws {
        let store = PostingMemTokenStore()
        store.save(AuthTokens(
            accessToken: JwtFactory.make(exp: 9_999_999_999),
            accessTokenExpiresAt: Date.distantFuture,
            refreshToken: "r1",
            refreshTokenExpiresAt: Date.distantFuture
        ))
        let client = try makeClient(store: store)
        MockURLProtocol.handler = { _ in (200, Data(#"{"orderId":"o-1"}"#.utf8)) }

        let _: ApiResult<Receipt> = await client.postAnonymous(
            path: "api/Order/GuestCancellationPreview",
            body: credentials
        )

        let request = try XCTUnwrap(MockURLProtocol.recorder.last(matching: "/api/Order/GuestCancellationPreview"))
        XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"))
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Time-Zone"), "Europe/Prague")
    }

    func testTheResponseDecodesIncludingAnOffsetlessServerDateTime() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in
            let body = #"{"orderId":"o-1","refundInitiated":true,"actualRefundAmount":12.5,"#
                + #""cleaningDateTime":"2026-09-19T10:00:00"}"#
            return (200, Data(body.utf8))
        }

        let result: ApiResult<Receipt> = await client.postAnonymous(path: "api/Order/CancelGuest", body: credentials)

        guard case let .success(receipt) = result else { return XCTFail("expected success, got \(result)") }
        XCTAssertEqual(receipt.orderId, "o-1")
        XCTAssertEqual(receipt.refundInitiated, true)
        XCTAssertEqual(receipt.actualRefundAmount, 12.5)
        XCTAssertEqual(receipt.cleaningDateTime, ApiDateDecoding.offsetlessUtcDate(from: "2026-09-19T10:00:00"))
    }

    /// A wrong key, a wrong e-mail and a stranger's order number are all `order.not_found`, and the key is
    /// what reaches the catalog — not the HTTP status.
    func testARefusalCarriesTheBusinessKeyOffTheProblemDetails() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in
            let body = #"{"title":"One or more validation errors occurred.","status":400,"#
                + #""errors":{"DisplayOrderNumber":["order.not_found"]}}"#
            return (400, Data(body.utf8))
        }

        let result: ApiResult<Receipt> = await client.postAnonymous(path: "api/Order/CancelGuest", body: credentials)

        XCTAssertEqual(result.apiErrorOrNil?.code, "order.not_found")
        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 400)
    }

    func testAMalformedSuccessBodyIsADecodingFailureNotASilentReceipt() async throws {
        let client = try makeClient()
        MockURLProtocol.handler = { _ in (200, Data("not json".utf8)) }

        let result: ApiResult<Receipt> = await client.postAnonymous(path: "api/Order/CancelGuest", body: credentials)

        XCTAssertEqual(result.apiErrorOrNil?.code, "network.decoding_failed")
    }
}

private struct PostingDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private final class PostingMemTokenStore: TokenStore, @unchecked Sendable {
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
