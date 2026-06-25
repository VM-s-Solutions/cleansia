import XCTest
@testable import CleansiaCore

final class UiStateTests: XCTestCase {
    func testLoadingCaseReportsLoading() {
        let state = UiState<Int>.loading
        XCTAssertTrue(state.isLoading)
        XCTAssertNil(state.loadedValue)
    }

    func testErrorCaseCarriesApiError() {
        let apiError = ApiError(code: "order.not_found", message: "Not found", httpStatus: 404)
        let state = UiState<Int>.error(apiError)
        XCTAssertFalse(state.isLoading)
        XCTAssertNil(state.loadedValue)
        guard case let .error(error) = state else {
            return XCTFail("expected error case")
        }
        XCTAssertEqual(error, apiError)
    }

    func testLoadedCaseExposesValue() {
        let state = UiState<Int>.loaded(42)
        XCTAssertFalse(state.isLoading)
        XCTAssertEqual(state.loadedValue, 42)
    }
}
