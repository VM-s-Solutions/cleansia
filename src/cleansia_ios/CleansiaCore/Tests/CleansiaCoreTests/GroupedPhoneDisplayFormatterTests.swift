import XCTest
@testable import CleansiaCore

final class GroupedPhoneDisplayFormatterTests: XCTestCase {
    private let formatter = GroupedPhoneDisplayFormatter()

    func testEmptyDisplaysEmpty() {
        XCTAssertEqual(formatter.display(""), "")
    }

    func testGroupsInternationalNumber() {
        XCTAssertEqual(formatter.display("+420728089247"), "+420 728 089 247")
    }

    func testGroupsLocalNumber() {
        XCTAssertEqual(formatter.display("728089247"), "728 089 247")
    }

    func testLonePlusStaysPlus() {
        XCTAssertEqual(formatter.display("+"), "+")
    }

    func testShortCountryCodeIsNotPadded() {
        XCTAssertEqual(formatter.display("+1"), "+1")
        XCTAssertEqual(formatter.display("+12"), "+12")
    }

    func testKeepsEveryDigit() {
        for length in 1 ... 15 {
            let digits = String(repeating: "7", count: length)
            XCTAssertEqual(PhoneNumberSanitizer.sanitize(formatter.display("+" + digits)), "+" + digits)
            XCTAssertEqual(PhoneNumberSanitizer.sanitize(formatter.display(digits)), digits)
        }
    }
}
