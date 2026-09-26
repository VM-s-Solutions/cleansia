import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The iOS twin of Android's `GuestOrderWireTest`, over the app's REAL generated `CustomerOrderAPI`
/// riding the Core spine: what each guest call puts on the wire, that none of them carries a Bearer
/// even with a session on the device, and what each refuses to read off the answer.
@MainActor
final class GuestOrderWireTests: XCTestCase {
    private static let lookupPath = "/api/Order/Lookup"
    private static let previewPath = "/api/Order/GuestCancellationPreview"
    private static let cancelPath = "/api/Order/CancelGuest"

    private static let token = "P8Jw-2hQ_xTokenFromTheEmail"
    private static let lookup = #"{"id":"o-1","displayOrderNumber":"CZ-123","cleaningDateTime":"2026-09-19T10:00:00Z","#
        + #""totalPrice":90.0,"orderStatus":{"value":2},"currency":{"code":"EUR"},"confirmationCode":"ref-1"}"#
    private static let preview = #"{"orderId":"o-1","tier":3,"feeRate":0.25,"feeAmount":22.5,"refundAmount":67.5,"#
        + #""totalPrice":90.0,"currencyCode":"EUR","expressWaiverForfeitedOnCancel":false,"oopsWindowMinutes":15}"#
    private static let receipt = #"{"orderId":"o-1","feeRate":0.25,"refundAmount":67.5,"actualRefundAmount":12.0,"#
        + #""totalPrice":90.0,"refundInitiated":true}"#
    private static let notFound = #"{"type":"order.not_found","detail":"Order not found"}"#

    private let client = LiveGuestOrderClient()
    private var key: GuestOrderKey!

    override func setUpWithError() throws {
        try super.setUpWithError()
        GuestWireRecorder.reset()
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [GuestWireRecorder.self]
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
        let link = "https://cleansia.cz/track-order?orderNumber=CZ-123&token=\(Self.token)"
        key = try XCTUnwrap(GuestOrderKey(pasted: link))
    }

    override func tearDown() {
        GuestWireRecorder.reset()
        key = nil
        super.tearDown()
    }

    /// The token is the whole credential, so it travels in a POST body and the device's own session
    /// stays out of the request: a Bearer beside the token would let a stale account answer for a
    /// booking it never owned.
    private func credentials(at path: String) throws -> [String: Any] {
        let request = try XCTUnwrap(GuestWireRecorder.request(ofPath: path), "no request to \(path)")
        XCTAssertEqual(request.httpMethod, "POST")
        XCTAssertNil(request.value(forHTTPHeaderField: "Authorization"), "\(path) must not carry the session")
        let body = try XCTUnwrap(GuestWireRecorder.json(ofPath: path))
        XCTAssertEqual(body["accessToken"] as? String, Self.token)
        XCTAssertNil(body["displayOrderNumber"])
        XCTAssertNil(body["confirmationCode"])
        return body
    }

    func testLookupPostsTheAccessTokenInTheBodyAndMapsTheOrderWithoutKeepingIt() async throws {
        GuestWireRecorder.responses[Self.lookupPath] = (200, Data(Self.lookup.utf8))

        let result = await client.lookup(key)

        let order = try XCTUnwrap(result.loadedValue)
        _ = try credentials(at: Self.lookupPath)
        XCTAssertEqual(order.id, "o-1")
        XCTAssertEqual(order.currencyCode, "EUR")
        XCTAssertEqual(order.totalPrice, 90)
        XCTAssertEqual(order.status, ._2)
        XCTAssertEqual(order.cleaningDateTime, ISO8601DateFormatter().date(from: "2026-09-19T10:00:00Z"))
        XCTAssertFalse(String(describing: order).contains(Self.token))
    }

    func testThePreviewPostsTheAccessTokenAndKeepsTheServersTierAndAmounts() async throws {
        GuestWireRecorder.responses[Self.previewPath] = (200, Data(Self.preview.utf8))

        let result = await client.cancellationQuote(key)

        let quote = try XCTUnwrap(result.loadedValue)
        _ = try credentials(at: Self.previewPath)
        XCTAssertEqual(quote.orderId, "o-1")
        XCTAssertEqual(quote.quote.tier, .partial)
        XCTAssertEqual(quote.quote.feeAmount, 22.5)
        XCTAssertEqual(quote.quote.refundAmount, 67.5)
        XCTAssertEqual(quote.quote.currencyCode, "EUR")
        XCTAssertEqual(quote.quote.oopsWindowMinutes, 15)
    }

    func testCancellationSendsTheReasonAndLanguageAndKeepsTheActualRefundApartFromThePolicyOne() async throws {
        GuestWireRecorder.responses[Self.cancelPath] = (200, Data(Self.receipt.utf8))

        let result = await client.cancel(key, reason: "schedule_changed", language: "sk")

        let receipt = try XCTUnwrap(result.loadedValue)
        let body = try credentials(at: Self.cancelPath)
        XCTAssertEqual(body["reason"] as? String, "schedule_changed")
        XCTAssertEqual(body["language"] as? String, "sk")
        XCTAssertEqual(receipt.refundAmount, 67.5)
        XCTAssertEqual(receipt.actualRefundAmount, 12)
        XCTAssertEqual(receipt.refunded, 12)
    }

    func testANullActualRefundStaysUnknownInsteadOfBorrowingThePolicyAmount() async throws {
        let body = Self.receipt.replacingOccurrences(of: "12.0", with: "null")
        GuestWireRecorder.responses[Self.cancelPath] = (200, Data(body.utf8))

        let result = await client.cancel(key, reason: "schedule_changed", language: "en")

        let receipt = try XCTUnwrap(result.loadedValue)
        XCTAssertNil(receipt.actualRefundAmount)
        XCTAssertNil(receipt.refunded)
        XCTAssertEqual(receipt.refundAmount, 67.5)
    }

    func testLookupRefusesAMissingPriceOrCurrencyOrStatusInsteadOfInventingThem() async {
        for body in [
            Self.lookup.replacingOccurrences(of: "90.0", with: "null"),
            Self.lookup.replacingOccurrences(of: "\"EUR\"", with: "null"),
            Self.lookup.replacingOccurrences(of: "\"value\":2", with: "\"value\":null")
        ] {
            GuestWireRecorder.responses[Self.lookupPath] = (200, Data(body.utf8))

            let result = await client.lookup(key)

            XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode, body)
        }
    }

    /// The order id is what pins a quote to the booking on screen, so it is refused with the figures.
    func testAQuoteWithoutItsOrderIdOrTierOrGraceIsRefusedRatherThanShown() async {
        GuestWireRecorder.responses[Self.previewPath] = (200, Data(Self.preview.utf8))
        let intact = await client.cancellationQuote(key)
        XCTAssertNotNil(intact.loadedValue, "the intact preview is refused, so every refusal below proves nothing")

        for body in [
            Self.preview.replacingOccurrences(of: "\"orderId\":\"o-1\",", with: ""),
            Self.preview.replacingOccurrences(of: "\"tier\":3,", with: ""),
            Self.preview.replacingOccurrences(of: ",\"oopsWindowMinutes\":15", with: "")
        ] {
            GuestWireRecorder.responses[Self.previewPath] = (200, Data(body.utf8))

            let result = await client.cancellationQuote(key)

            XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode, body)
        }
    }

    /// A wrong key on any of the three calls is the same `order.not_found`, passed through untouched.
    func testARefusalKeepsTheServersKeyOnEveryCall() async {
        for path in [Self.lookupPath, Self.previewPath, Self.cancelPath] {
            GuestWireRecorder.responses[path] = (400, Data(Self.notFound.utf8))
        }

        let lookup = await client.lookup(key)
        let preview = await client.cancellationQuote(key)
        let cancel = await client.cancel(key, reason: nil, language: "en")

        for result in [lookup.apiErrorOrNil, preview.apiErrorOrNil, cancel.apiErrorOrNil] {
            XCTAssertEqual(result?.code, "order.not_found")
            XCTAssertEqual(result?.httpStatus, 400)
        }
    }

    func testThePastedLinkIsReducedToItsTokenAndABlankOneIsRefused() {
        XCTAssertNil(GuestOrderKey(pasted: "   "))
        XCTAssertEqual(GuestOrderKey(pasted: " tok-1 ")?.accessToken, "tok-1")
        XCTAssertEqual(
            GuestOrderKey(pasted: "https://cleansia.cz/track-order?orderNumber=CZ-1&token=tok-1")?.accessToken,
            "tok-1"
        )
        XCTAssertEqual(
            GuestOrderKey(pasted: "https://cleansia.cz/track-order?token=tok-1&email=a%40b.cz")?.accessToken,
            "tok-1"
        )
        XCTAssertEqual(
            GuestOrderKey(pasted: "https://cleansia.cz/track-order?accesstoken=x")?.accessToken,
            "https://cleansia.cz/track-order?accesstoken=x"
        )
    }
}

private extension Result where Failure == ApiError {
    var loadedValue: Success? {
        if case let .success(value) = self { return value }
        return nil
    }
}

private final class GuestWireRecorder: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: [(request: URLRequest, body: Data?)] = []
    nonisolated(unsafe) static var responses: [String: (status: Int, body: Data)] = [:]

    static func reset() {
        lock.withLock {
            recorded.removeAll()
            responses.removeAll()
        }
    }

    static func request(ofPath path: String) -> URLRequest? {
        lock.withLock { recorded.last { $0.request.url?.path == path }?.request }
    }

    static func json(ofPath path: String) -> [String: Any]? {
        let data = lock.withLock { recorded.last { $0.request.url?.path == path }?.body }
        return data.flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] }
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let answer = Self.lock.withLock { () -> (status: Int, body: Data) in
            Self.recorded.append((request, Self.readBody(from: request)))
            return Self.responses[request.url?.path ?? ""] ?? (500, Data("{}".utf8))
        }
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: answer.status,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: answer.body)
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

/// A session IS on the device: the allow-list, not its absence, is what keeps the Bearer off the wire.
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
