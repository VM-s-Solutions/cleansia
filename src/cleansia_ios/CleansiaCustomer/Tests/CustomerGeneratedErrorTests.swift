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
}
