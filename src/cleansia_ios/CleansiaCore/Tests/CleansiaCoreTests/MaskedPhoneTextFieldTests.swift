import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

@MainActor
final class MaskedPhoneTextFieldTests: XCTestCase {
    private var value = ""
    private var focused = false

    private struct Harness {
        let representable: MaskedPhoneTextField
        let coordinator: MaskedPhoneTextField.Coordinator
        let field: UITextField
    }

    private func makeField() -> Harness {
        let representable = MaskedPhoneTextField(
            value: Binding(get: { self.value }, set: { self.value = $0 }),
            focused: Binding(get: { self.focused }, set: { self.focused = $0 }),
            engine: PhoneMaskEngine(formatter: GroupedPhoneDisplayFormatter()),
            isEnabled: true,
            font: .systemFont(ofSize: 16),
            textColor: .black,
            tintColor: .black
        )
        let field = UITextField()
        let coordinator = representable.makeCoordinator()
        field.delegate = coordinator
        return Harness(representable: representable, coordinator: coordinator, field: field)
    }

    private func typeKey(_ key: String, into field: UITextField, with coordinator: MaskedPhoneTextField.Coordinator) {
        let text = field.text ?? ""
        let caret = field.selectedTextRange
            .map { field.offset(from: field.beginningOfDocument, to: $0.end) }
            ?? text.utf16.count
        _ = coordinator.textField(
            field,
            shouldChangeCharactersIn: NSRange(location: caret, length: 0),
            replacementString: key
        )
    }

    func testTypingMasksTheTextAndPublishesTheWireValue() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        for key in "+420728089247" {
            typeKey(String(key), into: field, with: coordinator)
        }
        XCTAssertEqual(field.text, "+420 728 089 247")
        XCTAssertEqual(value, "+420728089247")
    }

    func testTypingLeavesTheCaretAtTheEnd() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        for key in "420728" {
            typeKey(String(key), into: field, with: coordinator)
        }
        XCTAssertEqual(field.text, "420 728")
        XCTAssertEqual(caretOffset(in: field), 7)
    }

    func testBackspaceOverASeparatorDeletesADigit() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        field.text = "+420 728 089 247"
        _ = coordinator.textField(
            field,
            shouldChangeCharactersIn: NSRange(location: 4, length: 1),
            replacementString: ""
        )
        XCTAssertEqual(value, "+42728089247")
        XCTAssertEqual(field.text, "+427 280 892 47")
    }

    func testPasteIsSanitisedBeforeItReachesTheBinding() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        _ = coordinator.textField(
            field,
            shouldChangeCharactersIn: NSRange(location: 0, length: 0),
            replacementString: "+420 728-089-247"
        )
        XCTAssertEqual(field.text, "+420 728 089 247")
        XCTAssertEqual(value, "+420728089247")
    }

    func testTheDelegateNeverLetsUIKitApplyTheRawEdit() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        let handled = coordinator.textField(
            field,
            shouldChangeCharactersIn: NSRange(location: 0, length: 0),
            replacementString: "4"
        )
        XCTAssertFalse(handled)
    }

    func testAutofilledTextIsNormalisedAndPublished() {
        let harness = makeField()
        let coordinator = harness.coordinator
        let field = harness.field
        field.text = "+420 728089247"
        coordinator.editingChanged(field)
        XCTAssertEqual(field.text, "+420 728 089 247")
        XCTAssertEqual(value, "+420728089247")
    }

    func testAValueLoadedFromTheServerIsSeatedFormatted() {
        let harness = makeField()
        let representable = harness.representable
        let field = harness.field
        representable.seat("+420 728089247", in: field)
        XCTAssertEqual(field.text, "+420 728 089 247")
    }

    func testSeatingLeavesAnAlreadyMatchingFieldAlone() {
        let harness = makeField()
        let representable = harness.representable
        let coordinator = harness.coordinator
        let field = harness.field
        for key in "420728" {
            typeKey(String(key), into: field, with: coordinator)
        }
        field.selectedTextRange = field.textRange(from: field.beginningOfDocument, to: field.beginningOfDocument)
        representable.seat(value, in: field)
        XCTAssertEqual(caretOffset(in: field), 0)
    }

    func testEmptyValueSeatsAnEmptyField() {
        let harness = makeField()
        let representable = harness.representable
        let field = harness.field
        field.text = "+420 728"
        representable.seat("", in: field)
        XCTAssertEqual(field.text, "")
    }

    private func caretOffset(in field: UITextField) -> Int? {
        field.selectedTextRange.map { field.offset(from: field.beginningOfDocument, to: $0.end) }
    }
}
