import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The consent read the review step gates on, over the app's REAL generated `CustomerGdprAPI`: what
/// counts as "on record" is a row that is granted and not since withdrawn — a withdrawn row is an
/// answer, but not the one that lets the box stay hidden.
@MainActor
final class BookingConsentWireTests: XCTestCase {
    private static let consentsPath = "/api/v1/Gdpr/consents"

    override func setUp() {
        super.setUp()
        ConsentReadStub.reset()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [ConsentReadStub.self]
        let session = URLSession(configuration: config)
        let tokenStore = SignedInTokenStore()
        CustomerGeneratedAuth.install(
            bridge: GeneratedClientAuthBridge(
                headerAdapter: HeaderAdapter(deviceIdProvider: WireDeviceId(), anonymousAllowList: .customer),
                tokenStore: tokenStore,
                sessionRefresher: SessionRefresher(
                    tokenStore: tokenStore,
                    refreshClient: NeverRefreshing(),
                    sessionManager: SessionManager(),
                    sessionScopedCaches: SessionScopedCacheRegistry()
                ),
                session: session
            ),
            basePath: "https://api.test"
        )
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }

    override func tearDown() {
        ConsentReadStub.reset()
        super.tearDown()
    }

    func testGrantedRowsAreOnRecordByTheirWireType() async throws {
        ConsentReadStub.response = (200, Data(#"""
        [{"id":"c-1","consentType":0,"isGranted":true,"grantedAt":"2026-09-01T10:00:00Z"},
         {"id":"c-2","consentType":1,"isGranted":true,"grantedAt":"2026-09-01T10:00:00Z"}]
        """#.utf8))

        let read = await LiveConsentStatusClient().grantedTypes()
        let granted = try XCTUnwrap(read)

        XCTAssertEqual(granted, [.termsOfService, .privacyPolicy])
        XCTAssertEqual(ConsentReadStub.lastRequest?.url?.path, Self.consentsPath)
        XCTAssertEqual(ConsentReadStub.lastRequest?.httpMethod, "GET")
    }

    func testAWithdrawnRowIsNotOnRecord() async throws {
        ConsentReadStub.response = (200, Data(#"""
        [{"id":"c-1","consentType":0,"isGranted":true,"grantedAt":"2026-09-01T10:00:00Z"},
         {"id":"c-2","consentType":1,"isGranted":false,"grantedAt":"2026-09-01T10:00:00Z",
          "withdrawnAt":"2026-09-02T10:00:00Z"}]
        """#.utf8))

        let read = await LiveConsentStatusClient().grantedTypes()
        let granted = try XCTUnwrap(read)

        XCTAssertEqual(granted, [.termsOfService])
    }

    /// `isGranted` and `withdrawnAt` are read together: a row the server flags as granted but stamps
    /// as withdrawn is not on record either way.
    func testAGrantedRowWithAWithdrawalStampIsNotOnRecord() async throws {
        ConsentReadStub.response = (200, Data(#"""
        [{"id":"c-1","consentType":0,"isGranted":true,"withdrawnAt":"2026-09-02T10:00:00Z"}]
        """#.utf8))

        let read = await LiveConsentStatusClient().grantedTypes()
        let granted = try XCTUnwrap(read)

        XCTAssertTrue(granted.isEmpty)
    }

    func testNoRowsIsAnEmptyRecordNotAFailedRead() async throws {
        ConsentReadStub.response = (200, Data("[]".utf8))

        let read = await LiveConsentStatusClient().grantedTypes()
        let granted = try XCTUnwrap(read)

        XCTAssertTrue(granted.isEmpty)
    }

    func testAFailedReadIsNil() async {
        ConsentReadStub.response = (500, Data("{}".utf8))

        let granted = await LiveConsentStatusClient().grantedTypes()

        XCTAssertNil(granted)
    }

    /// The read is authenticated — the box decides on THIS account's record, so the call must carry
    /// the session rather than ride the anonymous allow-list.
    func testTheReadCarriesTheSessionBearer() async {
        ConsentReadStub.response = (200, Data("[]".utf8))

        _ = await LiveConsentStatusClient().grantedTypes()

        XCTAssertEqual(ConsentReadStub.lastRequest?.value(forHTTPHeaderField: "Authorization"), "Bearer access-1")
    }
}

private final class ConsentReadStub: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: URLRequest?
    nonisolated(unsafe) static var response: (Int, Data) = (200, Data("[]".utf8))

    static var lastRequest: URLRequest? {
        lock.withLock { recorded }
    }

    static func reset() {
        lock.withLock { recorded = nil }
        response = (200, Data("[]".utf8))
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.withLock { Self.recorded = request }
        let (status, data) = Self.response
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: status,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

private struct WireDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-wire"
    }
}

private struct NeverRefreshing: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

private final class SignedInTokenStore: TokenStore, @unchecked Sendable {
    func current() -> AuthTokens? {
        AuthTokens(
            accessToken: "access-1",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "refresh-1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        )
    }

    func save(_: AuthTokens) {}

    func clear() {}
}
