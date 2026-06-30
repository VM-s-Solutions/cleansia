import XCTest
@testable import CleansiaCustomer

final class EvidenceFileValidatorTests: XCTestCase {
    func testAcceptsAllowedTypesUnderLimit() {
        for type in ["image/jpeg", "image/png", "image/webp", "application/pdf"] {
            XCTAssertNil(EvidenceFileValidator.validate(byteCount: 1024, contentType: type))
        }
    }

    func testRejectsOversizeAtExactlyOverLimit() {
        let over = DisputeFormConstants.maxEvidenceBytes + 1
        XCTAssertEqual(EvidenceFileValidator.validate(byteCount: over, contentType: "image/jpeg"), .tooLarge)
    }

    func testAcceptsExactlyAtLimit() {
        let atLimit = DisputeFormConstants.maxEvidenceBytes
        XCTAssertNil(EvidenceFileValidator.validate(byteCount: atLimit, contentType: "image/jpeg"))
    }

    func testRejectsUnsupportedType() {
        XCTAssertEqual(EvidenceFileValidator.validate(byteCount: 1024, contentType: "text/plain"), .unsupportedType)
        XCTAssertEqual(
            EvidenceFileValidator.validate(byteCount: 1024, contentType: "application/octet-stream"),
            .unsupportedType
        )
    }

    func testFailsClosedOnEmptyContentType() {
        XCTAssertEqual(EvidenceFileValidator.validate(byteCount: 1024, contentType: ""), .unsupportedType)
    }

    func testTypeMatchIsCaseInsensitive() {
        XCTAssertNil(EvidenceFileValidator.validate(byteCount: 1024, contentType: "IMAGE/JPEG"))
    }

    func testOversizeWinsOverWrongTypeOrder() {
        let over = DisputeFormConstants.maxEvidenceBytes + 1
        XCTAssertEqual(EvidenceFileValidator.validate(byteCount: over, contentType: "text/plain"), .tooLarge)
    }
}
