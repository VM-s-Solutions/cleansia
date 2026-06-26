import XCTest
@testable import CleansiaCore

final class PasswordPolicyTests: XCTestCase {
    func testHasMinLengthRequiresEightCharacters() {
        XCTAssertFalse(PasswordPolicy.hasMinLength("abc123"))
        XCTAssertFalse(PasswordPolicy.hasMinLength("abcdef1"))
        XCTAssertTrue(PasswordPolicy.hasMinLength("abcdefg1"))
        XCTAssertTrue(PasswordPolicy.hasMinLength("aaaaaaaaaaaa"))
    }

    func testHasLetterRequiresAtLeastOneLetter() {
        XCTAssertFalse(PasswordPolicy.hasLetter("12345678"))
        XCTAssertTrue(PasswordPolicy.hasLetter("1234567a"))
    }

    func testHasNumberRequiresAtLeastOneDigit() {
        XCTAssertFalse(PasswordPolicy.hasNumber("abcdefgh"))
        XCTAssertTrue(PasswordPolicy.hasNumber("abcdefg1"))
    }

    func testIsValidRequiresAllThreeRules() {
        XCTAssertFalse(PasswordPolicy.isValid("short1"))
        XCTAssertFalse(PasswordPolicy.isValid("12345678"))
        XCTAssertFalse(PasswordPolicy.isValid("abcdefgh"))
        XCTAssertTrue(PasswordPolicy.isValid("abcdefg1"))
    }

    func testPasswordsMatchRequiresNonEmptyEqualValues() {
        XCTAssertFalse(PasswordPolicy.passwordsMatch("", ""))
        XCTAssertFalse(PasswordPolicy.passwordsMatch("abcdefg1", ""))
        XCTAssertFalse(PasswordPolicy.passwordsMatch("abcdefg1", "abcdefg2"))
        XCTAssertTrue(PasswordPolicy.passwordsMatch("abcdefg1", "abcdefg1"))
    }
}
