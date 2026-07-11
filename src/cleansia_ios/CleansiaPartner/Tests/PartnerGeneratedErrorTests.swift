import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class PartnerGeneratedErrorTests: XCTestCase {
    private struct Transport: Error {}

    func testProblemDetailsTypeMapsToACodeBearingApiError() {
        let body = Data(#"{"type":"order.already_taken","detail":"Order was taken"}"#.utf8)

        let error = ApiError.fromGenerated(ErrorResponse.error(400, body, nil, Transport()))

        XCTAssertEqual(error.code, "order.already_taken")
        XCTAssertEqual(error.message, "Order was taken")
        XCTAssertEqual(error.httpStatus, 400)
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

    func testCancelledRequestWrappedByTheGeneratedClientMapsToTheSilentMarker() {
        let error = ApiError.fromGenerated(ErrorResponse.error(-1, nil, nil, URLError(.cancelled)))

        XCTAssertEqual(error.code, ApiError.cancelledCode)
        XCTAssertTrue(error.isCancellation)
        XCTAssertNotEqual(error.message, "cancelled")
    }
}
