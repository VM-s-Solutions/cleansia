import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

/// `CreateDispute` answers with an OBJECT, not a bare id.
///
/// `CreateDispute.Response` is `record Response(string DisputeId)`, so the endpoint sends
/// `{"disputeId":"..."}`. Both mobile clients once declared the call as returning a `String`, which
/// on iOS meant the customer target did not compile at all — and nothing pointed at the cause,
/// because every dispute test stubs `DisputeClient` and hands back a `String`. The wire was never
/// exercised on either platform.
///
/// These tests decode the real generated model with the real decoder, which is the only place that
/// mistake can be caught before a compiler or a customer finds it. Android's equivalent lives in
/// `DisputeWireTest.theCreatedDisputeIdIsReadOutOfTheObjectTheServerSends`.
final class DisputeWireFormatTests: XCTestCase {
    func testCreateDisputeResponseDecodesTheIdOutOfTheObject() throws {
        let json = Data(#"{"disputeId":"d-77"}"#.utf8)

        let dto = try CodableHelper.jsonDecoder.decode(CreateDisputeResponse.self, from: json)

        XCTAssertEqual(dto.disputeId, "d-77")
    }

    /// The shape that would have failed loudly if the response were ever a bare string: decoding a
    /// JSON string into the object model must not succeed. This is the assertion that pins the
    /// contract rather than just exercising it.
    func testABareStringIsNotACreateDisputeResponse() {
        let json = Data(#""d-77""#.utf8)

        XCTAssertThrowsError(try CodableHelper.jsonDecoder.decode(CreateDisputeResponse.self, from: json))
    }

    /// The generator types the property optional, because the schema declares no `required` array.
    /// The client must therefore treat a missing id as a refusal rather than carry a nil forward:
    /// the evidence upload that follows a dispute is addressed to this id, so an empty stand-in
    /// would strand the customer's photo instead of failing where someone can see it.
    func testAResponseWithNoIdDecodesToNilSoTheClientCanRefuseIt() throws {
        let json = Data(#"{}"#.utf8)

        let dto = try CodableHelper.jsonDecoder.decode(CreateDisputeResponse.self, from: json)

        XCTAssertNil(dto.disputeId)
    }

    /// The command half of the same call. `lines` is omitted rather than sent as `[]` when the
    /// customer ticked nothing, so the wire shape matches what the web client sends.
    func testCreateDisputeCommandOmitsLinesWhenNothingWasTicked() throws {
        let command = CreateDisputeCommand(
            orderId: "o-1",
            reason: DisputeReason(rawValue: 3),
            description: "late",
            lines: nil
        )

        let body = try CodableHelper.jsonEncoder.encode(command)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])

        XCTAssertEqual(object["orderId"] as? String, "o-1")
        XCTAssertNil(object["lines"])
    }
}
