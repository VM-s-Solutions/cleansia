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

    /// A copied number often carries something in front of its `+`: a space, a label, a bracket, or the
    /// direction marks Contacts wraps a number in. None of it may cost the number its country code.
    func testAPlusBeforeTheFirstDigitIsKeptWhateverPrecedesIt() {
        for pasted in [
            " +420 728 089 247",
            "\u{202D}+420 728 089 247\u{202C}",
            "Tel: +420 728 089 247",
            "(+420) 728 089 247"
        ] {
            XCTAssertEqual(PhoneNumberSanitizer.sanitize(pasted), "+420728089247", pasted)
        }
    }

    func testAPlusAfterADigitIsStillDropped() {
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("(420) 728 089 247"), "420728089247")
        XCTAssertEqual(PhoneNumberSanitizer.sanitize("420 +728"), "420728")
    }
}
