import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

final class CustomerGeneratedErrorTests: XCTestCase {
    private struct Transport: Error {}

    func testProblemDetailsTypeMapsToACodeBearingApiError() {
        let body = Data(#"{"type":"gdpr.deletion_already_pending","detail":"Deletion pending"}"#.utf8)

        let error = ApiError.fromGenerated(ErrorResponse.error(400, body, nil, Transport()))

        XCTAssertEqual(error.code, "gdpr.deletion_already_pending")
        XCTAssertEqual(error.message, "Deletion pending")
        XCTAssertEqual(error.httpStatus, 400)
    }

    func testErrorsDictBusinessKeyMapsToTheCode() {
        let body = Data(#"{"errors":{"Email":"user.not_existing_email"},"detail":"top"}"#.utf8)

        let error = ApiError.fromGenerated(ErrorResponse.error(400, body, nil, Transport()))

        XCTAssertEqual(error.code, "user.not_existing_email")
    }

    func testRawBodyIsOnlyTheLastResortMessage() {
        let error = ApiError.fromGenerated(ErrorResponse.error(500, Data("boom".utf8), nil, Transport()))

        XCTAssertNil(error.code)
        XCTAssertEqual(error.message, "boom")
    }

    func testNonGeneratedErrorFallsBackToTransportMapping() {
        let error = ApiError.fromGenerated(URLError(.notConnectedToInternet))

        XCTAssertEqual(error.code, "network.unreachable")
    }

    /// The literal "cancelled" toast the owner hit: a superseded request the
    /// generated client wraps as `ErrorResponse.error(-1, …, URLError.cancelled)`,
    /// whose `localizedDescription` is "cancelled". It must map to the silent
    /// marker, never a shown message.
    func testCancelledRequestWrappedByTheGeneratedClientMapsToTheSilentMarker() {
        let error = ApiError.fromGenerated(ErrorResponse.error(-1, nil, nil, URLError(.cancelled)))

        XCTAssertEqual(error.code, ApiError.cancelledCode)
        XCTAssertTrue(error.isCancellation)
        XCTAssertNotEqual(error.message, "cancelled")
    }

    func testRawSwiftCancellationMapsToTheSilentMarker() {
        let error = ApiError.fromGenerated(CancellationError())

        XCTAssertTrue(error.isCancellation)
    }
}
