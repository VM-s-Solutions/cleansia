import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class GeneratedClientAuthAdapterTests: XCTestCase {
    override func tearDown() {
        GenMockURLProtocol.handler = nil
        super.tearDown()
    }

    private func makeBridge(
        accessToken: String,
        refresher: AuthRefreshing
    ) -> GeneratedClientAuthBridge {
        let store = MemoryTokenStore(accessToken: accessToken)
        let sessionRefresher = SessionRefresher(
            tokenStore: store,
            refreshClient: refresher,
            sessionManager: SessionManager(),
            sessionScopedCaches: SessionScopedCacheRegistry()
        )
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [GenMockURLProtocol.self]
        return GeneratedClientAuthBridge(
            headerAdapter: HeaderAdapter(
                deviceIdProvider: FixedDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            tokenStore: store,
            sessionRefresher: sessionRefresher,
            session: URLSession(configuration: config)
        )
    }

    private func statsBody() -> Data {
        Data(#"{"weekEarnings":100,"currencyCode":"CZK"}"#.utf8)
    }

    func testGeneratedCallCarriesBearerAndDeviceHeaders() async throws {
        let bridge = makeBridge(accessToken: "access-1", refresher: NeverRefresher())
        PartnerGeneratedAuth.install(bridge: bridge, basePath: "https://api.test")

        let recorder = RequestRecorder()
        GenMockURLProtocol.handler = { request in
            recorder.record(request)
            return (200, self.statsBody())
        }

        _ = try await PartnerDashboardAPI.dashboardGetStats()

        let request = try XCTUnwrap(recorder.last)
        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer access-1")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Id"), "device-1")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Device-Label"), "iPhone")
        XCTAssertEqual(request.value(forHTTPHeaderField: "X-Time-Zone"), "Europe/Prague")
    }

    func testUnauthorizedTriggersSingleRefreshAndOneRetryWithNewToken() async throws {
        let refresher = CountingRefresher(newAccessToken: "access-2")
        let bridge = makeBridge(accessToken: "access-1", refresher: refresher)
        PartnerGeneratedAuth.install(bridge: bridge, basePath: "https://api.test")

        let recorder = RequestRecorder()
        GenMockURLProtocol.handler = { request in
            let count = recorder.record(request)
            return count == 1 ? (401, Data("{}".utf8)) : (200, self.statsBody())
        }

        let stats = try await PartnerDashboardAPI.dashboardGetStats()

        XCTAssertEqual(stats.currencyCode, "CZK")
        XCTAssertEqual(recorder.count, 2)
        XCTAssertEqual(recorder.authorizationHeaders, ["Bearer access-1", "Bearer access-2"])
        let calls = await refresher.calls
        XCTAssertEqual(calls, 1)
    }
}

private final class RequestRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private var requests: [URLRequest] = []

    @discardableResult
    func record(_ request: URLRequest) -> Int {
        lock.lock()
        defer { lock.unlock() }
        requests.append(request)
        return requests.count
    }

    var count: Int {
        lock.lock()
        defer { lock.unlock() }
        return requests.count
    }

    var last: URLRequest? {
        lock.lock()
        defer { lock.unlock() }
        return requests.last
    }

    var authorizationHeaders: [String] {
        lock.lock()
        defer { lock.unlock() }
        return requests.compactMap { $0.value(forHTTPHeaderField: "Authorization") }
    }
}

private struct FixedDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private struct NeverRefresher: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

private actor CountingRefresher: AuthRefreshing {
    private(set) var calls = 0
    let newAccessToken: String

    init(newAccessToken: String) {
        self.newAccessToken = newAccessToken
    }

    func refresh(refreshToken _: String) async -> RefreshCallResult {
        calls += 1
        let future = Date(timeIntervalSinceNow: 9999)
        return .refreshed(RefreshedTokens(
            accessToken: newAccessToken,
            accessTokenExpiresAt: future,
            refreshToken: "r-rotated",
            refreshTokenExpiresAt: future
        ))
    }
}

private final class MemoryTokenStore: TokenStore, @unchecked Sendable {
    private let lock = NSLock()
    private var stored: AuthTokens?

    init(accessToken: String) {
        let future = Date(timeIntervalSinceNow: 9999)
        stored = AuthTokens(
            accessToken: accessToken,
            accessTokenExpiresAt: future,
            refreshToken: "r1",
            refreshTokenExpiresAt: future
        )
    }

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

final class GenMockURLProtocol: URLProtocol {
    nonisolated(unsafe) static var handler: ((URLRequest) -> (Int, Data))?

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let handler = GenMockURLProtocol.handler, let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        let (status, data) = handler(request)
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
}
