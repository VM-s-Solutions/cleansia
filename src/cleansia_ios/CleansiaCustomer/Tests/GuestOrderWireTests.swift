import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The iOS twin of Android's `GuestOrderWireTest`: what each guest call puts on the wire and what it
/// refuses to read off it. The transport is faked at the Core seam; the spine's own wire behaviour is
/// pinned in `AnonymousPostingTests`.
final class GuestOrderWireTests: XCTestCase {
    private static let lookup = #"{"id":"o-1","displayOrderNumber":"CZ-123","cleaningDateTime":"2026-09-19T10:00:00Z","#
        + #""totalPrice":90.0,"orderStatus":{"value":2},"currency":{"code":"EUR"},"confirmationCode":"secret-code"}"#
    private static let preview = #"{"orderId":"o-1","tier":3,"feeRate":0.25,"feeAmount":22.5,"refundAmount":67.5,"#
        + #""totalPrice":90.0,"currencyCode":"EUR","expressWaiverForfeitedOnCancel":false}"#
    private static let receipt = #"{"orderId":"o-1","feeRate":0.25,"refundAmount":67.5,"actualRefundAmount":12.0,"#
        + #""totalPrice":90.0,"refundInitiated":true}"#

    private var transport: FakeAnonymousPosting!
    private var client: LiveGuestOrderClient!
    private var key: GuestOrderKey!

    override func setUpWithError() throws {
        try super.setUpWithError()
        transport = FakeAnonymousPosting()
        client = LiveGuestOrderClient(transport: transport)
        key = try XCTUnwrap(GuestOrderKey(number: "CZ-123", email: "guest@example.test", code: "secret-code"))
    }

    override func tearDown() {
        transport = nil
        client = nil
        key = nil
        super.tearDown()
    }

    private func credentials(at path: String) throws -> [String: Any] {
        let request = try XCTUnwrap(transport.requests.first { $0.path == path }, "no request to \(path)")
        let body = try XCTUnwrap(JSONSerialization.jsonObject(with: request.body) as? [String: Any])
        XCTAssertEqual(body["displayOrderNumber"] as? String, "CZ-123")
        XCTAssertEqual(body["email"] as? String, "guest@example.test")
        XCTAssertEqual(body["confirmationCode"] as? String, "secret-code")
        return body
    }

    func testLookupPostsTheCredentialsInTheBodyAndMapsTheOrderWithoutKeepingItsSecret() async throws {
        transport.responses["api/Order/Lookup"] = .success(Data(Self.lookup.utf8))

        let result = await client.lookup(key)

        let order = try XCTUnwrap(result.loadedValue)
        _ = try credentials(at: "api/Order/Lookup")
        XCTAssertEqual(order.id, "o-1")
        XCTAssertEqual(order.currencyCode, "EUR")
        XCTAssertEqual(order.totalPrice, 90)
        XCTAssertEqual(order.status, ._2)
        XCTAssertEqual(order.cleaningDateTime, ISO8601DateFormatter().date(from: "2026-09-19T10:00:00Z"))
        XCTAssertFalse(String(describing: order).contains("secret-code"))
    }

    func testThePreviewPostsTheCredentialsAndKeepsTheServersTierAndAmounts() async throws {
        transport.responses["api/Order/GuestCancellationPreview"] = .success(Data(Self.preview.utf8))

        let result = await client.cancellationQuote(key)

        let quote = try XCTUnwrap(result.loadedValue)
        _ = try credentials(at: "api/Order/GuestCancellationPreview")
        XCTAssertEqual(quote.orderId, "o-1")
        XCTAssertEqual(quote.quote.tier, .partial)
        XCTAssertEqual(quote.quote.feeAmount, 22.5)
        XCTAssertEqual(quote.quote.refundAmount, 67.5)
        XCTAssertEqual(quote.quote.currencyCode, "EUR")
    }

    func testCancellationSendsTheReasonAndLanguageAndKeepsTheActualRefundApartFromThePolicyOne() async throws {
        transport.responses["api/Order/CancelGuest"] = .success(Data(Self.receipt.utf8))

        let result = await client.cancel(key, reason: "schedule_changed", language: "sk")

        let receipt = try XCTUnwrap(result.loadedValue)
        let body = try credentials(at: "api/Order/CancelGuest")
        XCTAssertEqual(body["reason"] as? String, "schedule_changed")
        XCTAssertEqual(body["language"] as? String, "sk")
        XCTAssertEqual(receipt.refundAmount, 67.5)
        XCTAssertEqual(receipt.actualRefundAmount, 12)
        XCTAssertEqual(receipt.refunded, 12)
    }

    func testANullActualRefundStaysUnknownInsteadOfBorrowingThePolicyAmount() async throws {
        let body = Self.receipt.replacingOccurrences(of: "12.0", with: "null")
        transport.responses["api/Order/CancelGuest"] = .success(Data(body.utf8))

        let result = await client.cancel(key, reason: "schedule_changed", language: "en")

        let receipt = try XCTUnwrap(result.loadedValue)
        XCTAssertNil(receipt.actualRefundAmount)
        XCTAssertNil(receipt.refunded)
        XCTAssertEqual(receipt.refundAmount, 67.5)
    }

    func testLookupRefusesAMissingPriceOrCurrencyInsteadOfInventingMoney() async {
        for body in [
            Self.lookup.replacingOccurrences(of: "90.0", with: "null"),
            Self.lookup.replacingOccurrences(of: "\"EUR\"", with: "null"),
            Self.lookup.replacingOccurrences(of: "\"value\":2", with: "\"value\":null")
        ] {
            transport.responses["api/Order/Lookup"] = .success(Data(body.utf8))

            let result = await client.lookup(key)

            XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode, body)
        }
    }

    func testAnUnknownTierIsRefusedRatherThanQuotedAsFree() async {
        transport.responses["api/Order/GuestCancellationPreview"] =
            .success(Data(Self.preview.replacingOccurrences(of: "\"tier\":3", with: "\"tier\":99").utf8))

        let result = await client.cancellationQuote(key)

        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode)
    }

    /// A wrong key on any of the three calls is the same `order.not_found`, passed through untouched.
    func testARefusalKeepsTheServersKeyOnEveryCall() async {
        let refusal = ApiError(code: "order.not_found", httpStatus: 400)
        transport.responses["api/Order/Lookup"] = .failure(refusal)
        transport.responses["api/Order/GuestCancellationPreview"] = .failure(refusal)
        transport.responses["api/Order/CancelGuest"] = .failure(refusal)

        let lookup = await client.lookup(key)
        let preview = await client.cancellationQuote(key)
        let cancel = await client.cancel(key, reason: nil, language: "en")

        XCTAssertEqual(lookup.apiErrorOrNil, refusal)
        XCTAssertEqual(preview.apiErrorOrNil, refusal)
        XCTAssertEqual(cancel.apiErrorOrNil, refusal)
    }

    func testTheKeyIsTrimmedAndRefusedBlank() {
        XCTAssertNil(GuestOrderKey(number: " ", email: "a@b.cz", code: "x"))
        XCTAssertNil(GuestOrderKey(number: "CZ-1", email: "", code: "x"))
        XCTAssertNil(GuestOrderKey(number: "CZ-1", email: "a@b.cz", code: "\n"))
        let key = GuestOrderKey(number: " CZ-1 ", email: " a@b.cz ", code: " x ")
        XCTAssertEqual(key?.number, "CZ-1")
        XCTAssertEqual(key?.email, "a@b.cz")
        XCTAssertEqual(key?.code, "x")
    }
}

private extension Result where Failure == ApiError {
    var loadedValue: Success? {
        if case let .success(value) = self { return value }
        return nil
    }
}

private final class FakeAnonymousPosting: AnonymousPosting, @unchecked Sendable {
    var responses: [String: Result<Data, ApiError>] = [:]
    private(set) var requests: [(path: String, body: Data)] = []

    func postAnonymous<Response: Decodable>(path: String, body: some Encodable) async -> ApiResult<Response> {
        let encoded = (try? JSONEncoder().encode(body)) ?? Data()
        requests.append((path, encoded))
        switch responses[path] ?? .failure(ApiError(httpStatus: 500)) {
        case let .failure(error):
            return .failure(error)
        case let .success(data):
            let decoder = ApiDateDecoding.decoder(primary: { ISO8601DateFormatter().date(from: $0) })
            do {
                return try .success(decoder.decode(Response.self, from: data))
            } catch {
                return .failure(ApiError(code: "network.decoding_failed", httpStatus: 200))
            }
        }
    }
}
