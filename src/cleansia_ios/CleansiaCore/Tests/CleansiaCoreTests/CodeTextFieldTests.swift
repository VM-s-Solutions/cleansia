import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The field under the code boxes: however an edit arrives, the field is left showing the code and the caller
/// holding it, so the next edit is measured against the code and never against text the field kept.
@MainActor
final class CodeTextFieldTests: XCTestCase {
    private var code = ""
    private var focused = false

    private struct Harness {
        let coordinator: CodeTextField.Coordinator
        let field: UITextField
    }

    private func makeField() -> Harness {
        let representable = CodeTextField(
            code: Binding(get: { self.code }, set: { self.code = $0 }),
            focused: Binding(get: { self.focused }, set: { self.focused = $0 }),
            length: 6
        )
        let field = UITextField()
        let coordinator = representable.makeCoordinator()
        field.delegate = coordinator
        return Harness(coordinator: coordinator, field: field)
    }

    /// What UIKit asks when a key is typed, Paste is chosen or a suggestion is inserted at the caret.
    @discardableResult
    private func insert(_ text: String, into harness: Harness) -> Bool {
        let end = (harness.field.text ?? "").utf16.count
        return harness.coordinator.textField(
            harness.field,
            shouldChangeCharactersIn: NSRange(location: end, length: 0),
            replacementString: text
        )
    }

    private func backspace(_ harness: Harness) {
        let end = (harness.field.text ?? "").utf16.count
        _ = harness.coordinator.textField(
            harness.field,
            shouldChangeCharactersIn: NSRange(location: end - 1, length: 1),
            replacementString: ""
        )
    }

    private func type(_ keys: String, into harness: Harness) {
        for key in keys {
            insert(String(key), into: harness)
        }
    }

    func testTypingFillsTheCodeAndTheFieldShowsIt() {
        let harness = makeField()
        type("123", into: harness)
        XCTAssertEqual(code, "123")
        XCTAssertEqual(harness.field.text, "123")
    }

    /// The field makes every edit itself, so UIKit never applies one on top of the code.
    func testTheFieldMakesEveryEditItself() {
        XCTAssertFalse(insert("1", into: makeField()))
    }

    /// What iOS 26's SwiftUI field got wrong: the paste came back run together with the digits already typed,
    /// and the field then held on to that text.
    func testACodePastedOntoAHalfTypedOneReplacesItAndBackspaceTakesOneDigitOfIt() {
        let harness = makeField()
        type("12", into: harness)
        insert("654321", into: harness)
        XCTAssertEqual(code, "654321")
        XCTAssertEqual(harness.field.text, "654321")

        backspace(harness)
        XCTAssertEqual(code, "65432")
        XCTAssertEqual(harness.field.text, "65432")
    }

    func testAPastedSentenceLeavesItsCode() {
        let harness = makeField()
        insert("Your code: 123 456", into: harness)
        XCTAssertEqual(code, "123456")
    }

    func testACodeAutoFilledTwiceIsTheCodeOnce() {
        let harness = makeField()
        insert("123456", into: harness)
        insert("123456", into: harness)
        XCTAssertEqual(code, "123456")
        XCTAssertEqual(harness.field.text, "123456")
    }

    func testASeventhDigitIsIgnored() {
        let harness = makeField()
        type("1234567", into: harness)
        XCTAssertEqual(code, "123456")
        XCTAssertEqual(harness.field.text, "123456")
    }

    /// AutoFill can put text in the field without asking the delegate; only `.editingChanged` reports it.
    func testTextPutInWithoutAskingIsHeldToTheSameRule() {
        let harness = makeField()
        type("12", into: harness)
        harness.field.text = "12Your code: 654 321"
        harness.coordinator.editingChanged(harness.field)
        XCTAssertEqual(code, "654321")
        XCTAssertEqual(harness.field.text, "654321")
    }

    /// A code the caller sets — a paste from outside the field, a reset — is shown, and the next edit
    /// continues from it.
    func testACodeTheCallerSetsIsWhatTheNextEditContinuesFrom() {
        let harness = makeField()
        harness.coordinator.seat("1234", in: harness.field)
        XCTAssertEqual(harness.field.text, "1234")

        insert("5", into: harness)
        XCTAssertEqual(code, "12345")
    }

    func testTheCaretIsKeptAfterTheCode() {
        let harness = makeField()
        let field = harness.field
        type("1234", into: harness)

        field.selectedTextRange = field.textRange(from: field.beginningOfDocument, to: field.beginningOfDocument)
        harness.coordinator.textFieldDidChangeSelection(field)

        let caret = field.selectedTextRange.map { field.offset(from: field.beginningOfDocument, to: $0.start) }
        XCTAssertEqual(caret, 4)
    }

    func testEditingMarksTheInputFocusedAndEndingItUnfocused() {
        let harness = makeField()
        harness.coordinator.textFieldDidBeginEditing(harness.field)
        XCTAssertTrue(focused)
        harness.coordinator.textFieldDidEndEditing(harness.field)
        XCTAssertFalse(focused)
    }
}
