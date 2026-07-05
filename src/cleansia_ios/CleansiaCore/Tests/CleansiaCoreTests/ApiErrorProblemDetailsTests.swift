import XCTest
@testable import CleansiaCore

final class ApiErrorProblemDetailsTests: XCTestCase {
    func testTypeCarriesTheBusinessErrorCode() {
        let body = Data(
            #"{"type":"membership.already_active","title":"Bad Request","detail":"Already subscribed"}"#.utf8
        )

        let error = ApiError.fromProblemDetails(httpStatus: 400, body: body)

        XCTAssertEqual(error.code, "membership.already_active")
        XCTAssertEqual(error.message, "Already subscribed")
        XCTAssertEqual(error.httpStatus, 400)
    }

    func testErrorsDictKeyWinsOverType() {
        let body = Data(
            #"{"type":"ValidationError","detail":"top","errors":{"Email":"user.not_existing_email"}}"#.utf8
        )

        let error = ApiError.fromProblemDetails(httpStatus: 400, body: body)

        XCTAssertEqual(error.code, "user.not_existing_email")
        XCTAssertEqual(error.message, "top")
    }

    func testErrorCodeAliasIsPreferredOverType() {
        let body = Data(#"{"errorCode":"auth.account_locked","type":"ValidationError"}"#.utf8)

        let error = ApiError.fromProblemDetails(httpStatus: 400, body: body)

        XCTAssertEqual(error.code, "auth.account_locked")
    }

    func testTitleBacksUpAMissingDetail() {
        let body = Data(#"{"type":"gdpr.deletion_already_pending","title":"Bad Request"}"#.utf8)

        let error = ApiError.fromProblemDetails(httpStatus: 400, body: body)

        XCTAssertEqual(error.code, "gdpr.deletion_already_pending")
        XCTAssertEqual(error.message, "Bad Request")
    }

    func testNonProblemBodyFallsBackToRawText() {
        let error = ApiError.fromProblemDetails(httpStatus: 500, body: Data("boom".utf8))

        XCTAssertNil(error.code)
        XCTAssertEqual(error.message, "boom")
        XCTAssertEqual(error.httpStatus, 500)
    }

    func testNilBodyUsesTheFallbackMessage() {
        let error = ApiError.fromProblemDetails(httpStatus: 502, body: nil, fallbackMessage: "offline")

        XCTAssertNil(error.code)
        XCTAssertEqual(error.message, "offline")
    }
}
