import XCTest
@testable import CleansiaCore

final class PhoneNumberFormatterTests: XCTestCase {
    func testEmptyDisplaysEmpty() {
        XCTAssertEqual(PhoneNumberFormatter.display(""), "")
    }

    func testGroupsInternationalNumber() {
        XCTAssertEqual(PhoneNumberFormatter.display("+420728089247"), "+420 728 089 247")
    }

    func testGroupsLocalNumber() {
        XCTAssertEqual(PhoneNumberFormatter.display("728089247"), "728 089 247")
    }

    func testLonePlusStaysPlus() {
        XCTAssertEqual(PhoneNumberFormatter.display("+"), "+")
    }
}
