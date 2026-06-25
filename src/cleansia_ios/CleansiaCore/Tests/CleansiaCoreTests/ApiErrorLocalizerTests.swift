import XCTest
@testable import CleansiaCore

final class ApiErrorLocalizerTests: XCTestCase {
    private let localizer = ApiErrorLocalizer()

    func testServerMessageIsPreferredWhenPresent() {
        let error = ApiError(code: "order.not_found", message: "Order is gone", httpStatus: 404)
        XCTAssertEqual(localizer.message(for: error), "Order is gone")
    }

    func testBlankServerMessageFallsBackToStatus() {
        let error = ApiError(code: nil, message: "   ", httpStatus: 500)
        XCTAssertEqual(localizer.message(for: error), localizer.message(forStatus: 500))
    }

    func testKnownStatusesHaveDistinctMessages() {
        let unauthorized = localizer.message(forStatus: 401)
        let server = localizer.message(forStatus: 500)
        let unreachable = localizer.message(forStatus: nil)

        XCTAssertFalse(unauthorized.isEmpty)
        XCTAssertFalse(server.isEmpty)
        XCTAssertFalse(unreachable.isEmpty)
        XCTAssertNotEqual(unauthorized, server)
    }

    func testNilStatusYieldsTheConnectivityFallback() {
        let error = ApiError(code: nil, message: nil, httpStatus: nil)
        XCTAssertEqual(localizer.message(for: error), localizer.message(forStatus: nil))
    }
}
