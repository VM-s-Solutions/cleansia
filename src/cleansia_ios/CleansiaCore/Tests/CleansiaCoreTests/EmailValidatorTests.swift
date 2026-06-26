import XCTest
@testable import CleansiaCore

final class EmailValidatorTests: XCTestCase {
    func testAcceptsOrdinaryAddresses() {
        XCTAssertTrue(EmailValidator.isValid("user@example.com"))
        XCTAssertTrue(EmailValidator.isValid("first.last@sub.example.co.uk"))
        XCTAssertTrue(EmailValidator.isValid("name+tag@example.io"))
        XCTAssertTrue(EmailValidator.isValid("a_b-c%d@example-domain.com"))
    }

    func testRejectsEmpty() {
        XCTAssertFalse(EmailValidator.isValid(""))
    }

    func testRejectsMissingAtSymbol() {
        XCTAssertFalse(EmailValidator.isValid("plainstring"))
        XCTAssertFalse(EmailValidator.isValid("no-at-symbol.com"))
    }

    func testRejectsMissingDomainAndTld() {
        XCTAssertFalse(EmailValidator.isValid("missing-domain@"))
        XCTAssertFalse(EmailValidator.isValid("@missing-local.com"))
        XCTAssertFalse(EmailValidator.isValid("trailing-dot@example."))
    }

    func testRejectsWhitespace() {
        XCTAssertFalse(EmailValidator.isValid("spaces in@example.com"))
        XCTAssertFalse(EmailValidator.isValid(" leading@example.com"))
        XCTAssertFalse(EmailValidator.isValid("trailing@example.com "))
    }
}
