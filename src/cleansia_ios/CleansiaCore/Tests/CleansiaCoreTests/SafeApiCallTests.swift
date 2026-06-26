import XCTest
@testable import CleansiaCore

final class SafeApiCallTests: XCTestCase {
    private struct StatusError: Error {
        let status: Int
    }

    func testSuccessWrapsValue() async {
        let result = await apiResult { 42 }
        XCTAssertEqual(try? result.get(), 42)
    }

    func testCancellationMapsToCancelledCode() async {
        let result: ApiResult<Int> = await apiResult { throw CancellationError() }
        XCTAssertEqual(result.apiErrorOrNil?.code, "network.cancelled")
    }

    func testDefaultMappingForUnknownError() async {
        let result: ApiResult<Int> = await apiResult { throw StatusError(status: 500) }
        XCTAssertEqual(result.apiErrorOrNil?.code, "network.unknown")
    }

    func testCustomMapErrorIsUsed() async {
        let result: ApiResult<Int> = await apiResult(
            mapError: { error in ApiError(httpStatus: (error as? StatusError)?.status) },
            { throw StatusError(status: 503) }
        )
        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 503)
    }

    func testUrlErrorMapsToUnreachable() async {
        let result: ApiResult<Int> = await apiResult { throw URLError(.notConnectedToInternet) }
        XCTAssertEqual(result.apiErrorOrNil?.code, "network.unreachable")
    }
}
