import XCTest
@testable import CleansiaCore

final class ApiErrorLocalizerTests: XCTestCase {
    private let localizer = ApiErrorLocalizer()

    func testBusinessKeyWithCatalogEntryReturnsTheLocalizedString() {
        let error = ApiError(code: "auth.invalid_confirmation_code", message: "raw server text", httpStatus: 400)

        let resolved = localizer.message(for: error)

        XCTAssertEqual(resolved, String(localized: "error.auth.invalid_confirmation_code", bundle: .module))
        XCTAssertNotEqual(resolved, "auth.invalid_confirmation_code")
        XCTAssertNotEqual(resolved, "raw server text")
    }

    func testBusinessKeyIsPreferredOverServerMessage() {
        let error = ApiError(code: "order.not_found", message: "Order is gone", httpStatus: 404)

        XCTAssertEqual(localizer.message(for: error), String(localized: "error.order.not_found", bundle: .module))
    }

    func testUncataloguedBusinessKeyFallsBackToTheRawKey() {
        let error = ApiError(code: "definitely.not_in_catalog", message: "server detail", httpStatus: 400)

        XCTAssertEqual(localizer.message(for: error), "definitely.not_in_catalog")
    }

    func testUndottedCodeFallsThroughToServerMessage() {
        let error = ApiError(code: "ValidationError", message: "Something specific", httpStatus: 400)

        XCTAssertEqual(localizer.message(for: error), "Something specific")
    }

    func testServerMessageUsedWhenNoCode() {
        let error = ApiError(code: nil, message: "Order is gone", httpStatus: 404)

        XCTAssertEqual(localizer.message(for: error), "Order is gone")
    }

    func testNetworkCodeWithoutCatalogEntryNeverSurfacesRaw() {
        let error = ApiError(code: "network.decoding_failed", message: nil, httpStatus: 500)

        let resolved = localizer.message(for: error)

        XCTAssertEqual(resolved, localizer.message(forStatus: 500))
        XCTAssertFalse(resolved.contains("network.decoding_failed"))
    }

    func testNetworkUnknownWithMessageShowsTheMessageNotTheCode() {
        let error = ApiError(code: "network.unknown", message: "The operation failed", httpStatus: nil)

        XCTAssertEqual(localizer.message(for: error), "The operation failed")
    }

    func testNetworkUnreachableUsesItsCatalogEntry() {
        let error = ApiError(code: "network.unreachable", message: "NSURLErrorDomain -1004", httpStatus: nil)

        XCTAssertEqual(localizer.message(for: error), String(localized: "error.network.unreachable", bundle: .module))
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
