import XCTest
@testable import CleansiaCore

final class ChosenPasswordTests: XCTestCase {
    private let rules = "rules"
    private let mismatch = "mismatch"

    private func typed(_ password: String, _ confirmation: String) -> ChosenPassword {
        var form = ChosenPassword()
        form.setPassword(password)
        form.setConfirmation(confirmation)
        return form
    }

    func testAPasswordWithinTheRulesTypedTwiceIsTheOneToSend() {
        var form = typed("Chosen123", "Chosen123")

        XCTAssertTrue(form.isValid)
        XCTAssertEqual(form.validated(rulesError: rules, mismatchError: mismatch), "Chosen123")
        XCTAssertNil(form.passwordError)
        XCTAssertNil(form.confirmationError)
    }

    func testAPasswordOutsideTheRulesIsRefusedWithTheRulesMessage() {
        for password in ["Short12", "lettersonly", "12345678", ""] {
            var form = typed(password, password)

            XCTAssertNil(form.validated(rulesError: rules, mismatchError: mismatch), password)
            XCTAssertEqual(form.passwordError, rules, password)
        }
    }

    func testTwoDifferentPasswordsAreRefusedOnTheConfirmationOnly() {
        var form = typed("Chosen123", "Chosen124")

        XCTAssertNil(form.validated(rulesError: rules, mismatchError: mismatch))
        XCTAssertNil(form.passwordError)
        XCTAssertEqual(form.confirmationError, mismatch)
    }

    func testAnEmptyFormRefusesBothFields() {
        var form = ChosenPassword()

        XCTAssertNil(form.validated(rulesError: rules, mismatchError: mismatch))
        XCTAssertEqual(form.passwordError, rules)
        XCTAssertEqual(form.confirmationError, mismatch)
    }

    func testTypingInAFieldClearsOnlyThatFieldsMessage() {
        var form = ChosenPassword()
        _ = form.validated(rulesError: rules, mismatchError: mismatch)

        form.setPassword("Chosen123")
        XCTAssertNil(form.passwordError)
        XCTAssertEqual(form.confirmationError, mismatch)

        form.setConfirmation("Chosen123")
        XCTAssertNil(form.confirmationError)
    }

    func testTheRuleListReadsThePolicy() {
        let form = typed("abc1", "abc")

        XCTAssertFalse(form.hasMinLength)
        XCTAssertTrue(form.hasLetter)
        XCTAssertTrue(form.hasNumber)
        XCTAssertFalse(form.matches)
        XCTAssertFalse(form.isValid)
    }
}
