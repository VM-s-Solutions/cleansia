import XCTest
@testable import CleansiaCore

final class ApiErrorCancellationTests: XCTestCase {
    func testSwiftCancellationMapsToTheSilentMarker() {
        XCTAssertTrue(ApiError.isCancellation(CancellationError()))
        XCTAssertEqual(ApiError.from(CancellationError()).code, ApiError.cancelledCode)
    }

    func testURLErrorCancelledMapsToTheSilentMarkerNotUnreachable() {
        XCTAssertTrue(ApiError.isCancellation(URLError(.cancelled)))
        XCTAssertEqual(ApiError.from(URLError(.cancelled)).code, ApiError.cancelledCode)
    }

    func testBridgedNSURLErrorCancelledMapsToTheSilentMarker() {
        let nsError = NSError(domain: NSURLErrorDomain, code: NSURLErrorCancelled)
        XCTAssertTrue(ApiError.isCancellation(nsError))
        XCTAssertEqual(ApiError.from(nsError).code, ApiError.cancelledCode)
    }

    func testAGenuineTransportErrorIsNotCancellation() {
        XCTAssertFalse(ApiError.isCancellation(URLError(.notConnectedToInternet)))
        XCTAssertEqual(ApiError.from(URLError(.notConnectedToInternet)).code, "network.unreachable")
    }

    func testIsCancellationPredicateOnTheMarkerCode() {
        XCTAssertTrue(ApiError(code: ApiError.cancelledCode).isCancellation)
        XCTAssertFalse(ApiError(code: "network.unreachable").isCancellation)
        XCTAssertFalse(ApiError(httpStatus: 500).isCancellation)
    }

    func testApiResultWrapsThrownCancellationAsTheMarker() async {
        let result: ApiResult<Int> = await apiResult { throw CancellationError() }
        guard case let .failure(error) = result else { return XCTFail("expected failure") }
        XCTAssertTrue(error.isCancellation)
    }
}
