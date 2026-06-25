import XCTest
@testable import CleansiaCore

final class PhoneNumberSanitizerTests: XCTestCase {
    func testEmptyStaysEmpty() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize(""), "")
    }

    func testKeepsLeadingPlusAndDigits() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("+420728089247"), "+420728089247")
    }

    func testStripsSpacesAndSeparators() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("+420-728 089 247"), "+420728089247")
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("(420) 728 089 247"), "420728089247")
    }

    func testDropsLetters() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("ab123cd456"), "123456")
    }

    func testOnlyLeadingPlusKept() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("4+20+7"), "4207")
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("++420"), "+420")
    }
}
