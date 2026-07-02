import XCTest
@testable import CleansiaCore

final class ProblemDetailsDecodeTests: XCTestCase {
    private func decode(_ json: String) throws -> ProblemDetails {
        try JSONDecoder().decode(ProblemDetails.self, from: Data(json.utf8))
    }

    func testErrorsDictWithStringValuesYieldsTheBusinessKey() throws {
        let problem = try decode(
            #"{"title":"Validation Error","detail":"top","errors":{"Email":"user.not_existing_email"}}"#
        )

        XCTAssertEqual(problem.firstErrorKey, "user.not_existing_email")
        XCTAssertEqual(problem.detail, "top")
    }

    func testErrorsDictWithStringArrayValuesYieldsTheFirstElement() throws {
        let problem = try decode(#"{"errors":{"Id":["The Id field is required."]}}"#)

        XCTAssertEqual(problem.firstErrorKey, "The Id field is required.")
    }

    func testMixedValueShapesDecodeTogether() throws {
        let problem = try decode(#"{"errors":{"A":["x"],"B":"y"}}"#)

        let first = try XCTUnwrap(problem.firstErrorKey)
        XCTAssertTrue(["x", "y"].contains(first))
    }

    func testEmptyValuesYieldNoFirstErrorKey() throws {
        let problem = try decode(#"{"errors":{"A":"","B":[]}}"#)

        XCTAssertNil(problem.firstErrorKey)
    }

    func testMissingErrorsDictLeavesFirstErrorKeyNil() throws {
        let problem = try decode(#"{"title":"t","detail":"d"}"#)

        XCTAssertNil(problem.errors)
        XCTAssertNil(problem.firstErrorKey)
    }

    func testMalformedErrorsShapeDoesNotDiscardTitleAndDetail() throws {
        let problem = try decode(#"{"title":"t","detail":"d","errors":"boom"}"#)

        XCTAssertNil(problem.errors)
        XCTAssertEqual(problem.title, "t")
        XCTAssertEqual(problem.detail, "d")
    }

    func testErrorCodeAndCodeKeyAliasesStillDecode() throws {
        XCTAssertEqual(try decode(#"{"errorCode":"auth.account_locked"}"#).errorCode, "auth.account_locked")
        XCTAssertEqual(try decode(#"{"code":"auth.account_locked"}"#).errorCode, "auth.account_locked")
    }
}
