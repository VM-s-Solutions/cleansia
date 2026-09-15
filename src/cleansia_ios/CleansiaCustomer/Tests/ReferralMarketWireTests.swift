import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The bytes the production referral client puts on the wire: the regenerated `ValidateReferralQuery`
/// carries the market as `countryId`, and a nil market OMITS the member rather than sending null, which
/// is how the server is told to look the code up in the default market's operating company.
@MainActor
final class ReferralMarketWireTests: XCTestCase {
    private static let validatePath = "/api/Referral/Validate"

    override func setUp() {
        super.setUp()
        ReferralWireRecorder.reset()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [ReferralWireRecorder.self]
        let session = URLSession(configuration: config)
        let tokenStore = AnonymousTokenStore()
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
    }

    override func tearDown() {
        ReferralWireRecorder.reset()
        super.tearDown()
    }

    func testTheChosenMarketTravelsAsCountryIdBesideTheCode() async throws {
        _ = await LiveReferralClient().validate(code: "ANNA7", countryId: "svk")

        let body = try XCTUnwrap(ReferralWireRecorder.json(ofPath: Self.validatePath))
        XCTAssertEqual(body["code"] as? String, "ANNA7")
        XCTAssertEqual(body["countryId"] as? String, "svk")
        XCTAssertEqual(ReferralWireRecorder.method(ofPath: Self.validatePath), "POST")
    }

    func testNoMarketOmitsTheMemberRatherThanSendingNull() async throws {
        _ = await LiveReferralClient().validate(code: "ANNA7", countryId: nil)

        let wire = try XCTUnwrap(ReferralWireRecorder.text(ofPath: Self.validatePath))
        let body = try XCTUnwrap(ReferralWireRecorder.json(ofPath: Self.validatePath))
        XCTAssertNil(body["countryId"], wire)
        XCTAssertFalse(wire.contains("countryId"), wire)
        XCTAssertEqual(body["code"] as? String, "ANNA7")
    }

    /// The endpoint is on the anonymous allow-list: a signup screen has no session to attach.
    func testTheCallCarriesNoBearer() async throws {
        _ = await LiveReferralClient().validate(code: "ANNA7", countryId: "svk")

        let request = try XCTUnwrap(ReferralWireRecorder.request(ofPath: Self.validatePath))
        XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"))
    }
}

private final class ReferralWireRecorder: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: [(request: URLRequest, body: Data?)] = []

    static func reset() {
        lock.withLock { recorded.removeAll() }
    }

    static func request(ofPath path: String) -> URLRequest? {
        lock.withLock { recorded.last { $0.request.url?.path == path }?.request }
    }

    static func method(ofPath path: String) -> String? {
        request(ofPath: path)?.httpMethod
    }

    static func text(ofPath path: String) -> String? {
        data(ofPath: path).flatMap { String(data: $0, encoding: .utf8) }
    }

    static func json(ofPath path: String) -> [String: Any]? {
        data(ofPath: path).flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] }
    }

    private static func data(ofPath path: String) -> Data? {
        lock.withLock { recorded.last { $0.request.url?.path == path }?.body }
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.withLock { Self.recorded.append((request, Self.readBody(from: request))) }
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: 200,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(#"{"isValid":true,"referrerFirstName":"Eva"}"#.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    /// `URLSession` moves an uploaded body onto `httpBodyStream`, so reading `httpBody` alone reports
    /// every request as empty.
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

private final class AnonymousTokenStore: TokenStore, @unchecked Sendable {
    func current() -> AuthTokens? {
        nil
    }

    func save(_: AuthTokens) {}

    func clear() {}
}
