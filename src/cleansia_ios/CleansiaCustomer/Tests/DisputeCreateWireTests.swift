import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The complaint as `LiveDisputeClient` puts it on the wire through the app's REAL generated
/// `CustomerDisputeAPI`: the customer's refund choice travels by number, 1 a card refund and 2 credit.
@MainActor
final class DisputeCreateWireTests: XCTestCase {
    private static let createPath = "/api/Dispute/Create"

    override func setUp() {
        super.setUp()
        DisputeCreateRecorder.reset()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [DisputeCreateRecorder.self]
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
    }

    override func tearDown() {
        DisputeCreateRecorder.reset()
        super.tearDown()
    }

    func testCreditTravelsAsTwo() async throws {
        let created = await createDispute(settlement: .credit)

        XCTAssertEqual(try created.get(), "d-9")
        let body = try XCTUnwrap(DisputeCreateRecorder.json(ofPath: Self.createPath))
        XCTAssertEqual(body["settlementPreference"] as? Int, 2)
        XCTAssertEqual(DisputeCreateRecorder.request(ofPath: Self.createPath)?.httpMethod, "POST")
    }

    func testACardRefundTravelsAsOne() async throws {
        let created = await createDispute(settlement: .cardRefund)

        XCTAssertEqual(try created.get(), "d-9")
        let body = try XCTUnwrap(DisputeCreateRecorder.json(ofPath: Self.createPath))
        XCTAssertEqual(body["settlementPreference"] as? Int, 1)
    }

    private func createDispute(settlement: DisputeSettlement) async -> ApiResult<String> {
        await LiveDisputeClient().create(
            orderId: "o-1",
            reason: 1,
            description: "late",
            lines: [],
            settlement: settlement
        )
    }
}

private final class DisputeCreateRecorder: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: [(request: URLRequest, body: Data?)] = []

    static func reset() {
        lock.withLock { recorded.removeAll() }
    }

    static func request(ofPath path: String) -> URLRequest? {
        lock.withLock { recorded.last { $0.request.url?.path == path }?.request }
    }

    static func json(ofPath path: String) -> [String: Any]? {
        let body = lock.withLock { recorded.last { $0.request.url?.path == path }?.body }
        return body.flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] }
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
        client?.urlProtocol(self, didLoad: Data(#"{"disputeId":"d-9"}"#.utf8))
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
