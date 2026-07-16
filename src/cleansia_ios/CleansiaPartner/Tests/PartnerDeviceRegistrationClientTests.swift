import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class PartnerDeviceRegistrationClientTests: XCTestCase {
    override func setUp() {
        super.setUp()
        installBridge()
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        super.tearDown()
    }

    func testRegisterSendsPlatformIosBodyAndMapsSuccess() async throws {
        var capturedBody: Data?
        GenMockURLProtocol.handler = { request in
            capturedBody = request.bodyData
            return (200, Data(#"{"deviceId":"row-1"}"#.utf8))
        }

        let result = await PartnerDeviceRegistrationClient().register(
            RegisterDeviceRequest(deviceId: "device-1", deviceToken: "apns-abc", platform: "ios")
        )

        XCTAssertNil(result.apiErrorOrNil)
        let body = try XCTUnwrap(capturedBody)
        let command = try JSONDecoder().decode(RegisterDeviceCommand.self, from: body)
        XCTAssertEqual(command.deviceId, "device-1")
        XCTAssertEqual(command.deviceToken, "apns-abc")
        XCTAssertEqual(command.platform, "ios")
    }

    func testRegisterMapsServerErrorToApiResultFailure() async {
        GenMockURLProtocol.handler = { _ in (500, Data("{}".utf8)) }

        let result = await PartnerDeviceRegistrationClient().register(
            RegisterDeviceRequest(deviceId: "device-1", deviceToken: "apns-abc", platform: "ios")
        )

        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 500)
    }

    func testUnregisterHitsDeviceIdQueryAndMapsSuccess() async throws {
        var capturedURL: URL?
        GenMockURLProtocol.handler = { request in
            capturedURL = request.url
            return (200, Data(#"{"success":true}"#.utf8))
        }

        let result = await PartnerDeviceRegistrationClient().unregister(deviceId: "device-1")

        XCTAssertNil(result.apiErrorOrNil)
        let query = try XCTUnwrap(capturedURL?.query)
        XCTAssertTrue(query.contains("DeviceId=device-1"), query)
    }

    private func installBridge() {
        let store = StubTokenStore(accessToken: "access-1")
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [GenMockURLProtocol.self]
        let bridge = GeneratedClientAuthBridge(
            headerAdapter: HeaderAdapter(
                deviceIdProvider: StubDeviceId(),
                deviceLabel: "iPhone",
                timeZoneIdentifier: { "Europe/Prague" }
            ),
            tokenStore: store,
            sessionRefresher: SessionRefresher(
                tokenStore: store,
                refreshClient: NoRefresh(),
                sessionManager: SessionManager(),
                sessionScopedCaches: SessionScopedCacheRegistry()
            ),
            session: URLSession(configuration: config)
        )
        PartnerGeneratedAuth.install(bridge: bridge, basePath: "https://api.test")
    }
}

private extension URLRequest {
    var bodyData: Data? {
        if let httpBody { return httpBody }
        guard let stream = httpBodyStream else { return nil }
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

private struct StubDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-1"
    }
}

private struct NoRefresh: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

private final class StubTokenStore: TokenStore, @unchecked Sendable {
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
        lock.withLock { stored }
    }

    func save(_ tokens: AuthTokens) {
        lock.withLock { stored = tokens }
    }

    func clear() {
        lock.withLock { stored = nil }
    }
}
