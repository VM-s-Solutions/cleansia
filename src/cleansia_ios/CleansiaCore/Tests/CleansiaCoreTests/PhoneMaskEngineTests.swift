import XCTest
@testable import CleansiaCore

private struct StubPhoneDisplayFormatter: PhoneDisplayFormatting {
    let transform: (String) -> String

    func display(_ wireValue: String) -> String {
        transform(wireValue)
    }
}

final class PhoneMaskEngineTests: XCTestCase {
    private let engine = PhoneMaskEngine(formatter: GroupedPhoneDisplayFormatter())

    private func type(_ keys: String, into start: String = "") -> [PhoneMaskEdit] {
        var text = start
        var edits: [PhoneMaskEdit] = []
        for key in keys {
            let edit = engine.edit(current: text, range: text.count ..< text.count, replacement: String(key))
            text = edit.text
            edits.append(edit)
        }
        return edits
    }

    func testAsYouTypeBuildsGroupsWithCaretAtEnd() {
        let edits = type("+420728089247")
        XCTAssertEqual(edits.map(\.text), [
            "+",
            "+4",
            "+42",
            "+420",
            "+420 7",
            "+420 72",
            "+420 728",
            "+420 728 0",
            "+420 728 08",
            "+420 728 089",
            "+420 728 089 2",
            "+420 728 089 24",
            "+420 728 089 247"
        ])
        XCTAssertEqual(edits.map(\.caret), edits.map(\.text.count))
        XCTAssertEqual(edits.last?.wireValue, "+420728089247")
    }

    func testAsYouTypeWithoutCountryCode() {
        let edits = type("728089247")
        XCTAssertEqual(edits.last?.text, "728 089 247")
        XCTAssertEqual(edits.last?.wireValue, "728089247")
        XCTAssertEqual(edits.last?.caret, 11)
    }

    func testEveryKeystrokeKeepsDisplayAndWireValueInSync() {
        for edit in type("+420728089247") {
            XCTAssertEqual(PhoneNumberSanitizer.sanitize(edit.text), edit.wireValue)
        }
    }

    func testTypedSeparatorsAreDroppedFromTheWireValue() {
        let edits = type("+420 728-089")
        XCTAssertEqual(edits.last?.text, "+420 728 089")
        XCTAssertEqual(edits.last?.wireValue, "+420728089")
    }

    func testBackspaceOverSeparatorDeletesThePrecedingDigit() {
        let edit = engine.edit(current: "+420 728 089 247", range: 4 ..< 5, replacement: "")
        XCTAssertEqual(edit.wireValue, "+42728089247")
        XCTAssertEqual(edit.text, "+427 280 892 47")
        XCTAssertEqual(edit.caret, 3)
    }

    func testBackspaceOverSeparatorIsNeverANoOp() {
        let current = "+420 728 089 247"
        for (offset, character) in Array(current).enumerated() where character == " " {
            let edit = engine.edit(current: current, range: offset ..< offset + 1, replacement: "")
            XCTAssertNotEqual(edit.text, current, "backspace over the separator at \(offset) did nothing")
            XCTAssertEqual(edit.wireValue.count, 12)
        }
    }

    func testBackspaceOverDigitDeletesThatDigit() {
        let edit = engine.edit(current: "+420 728", range: 7 ..< 8, replacement: "")
        XCTAssertEqual(edit.wireValue, "+42072")
        XCTAssertEqual(edit.text, "+420 72")
        XCTAssertEqual(edit.caret, 7)
    }

    func testBackspaceOverLeadingPlusKeepsTheDigits() {
        let edit = engine.edit(current: "+420 728", range: 0 ..< 1, replacement: "")
        XCTAssertEqual(edit.wireValue, "420728")
        XCTAssertEqual(edit.text, "420 728")
        XCTAssertEqual(edit.caret, 0)
    }

    func testBackspaceOnLeadingSeparatorWithNoDigitBeforeItJustDropsIt() {
        let edit = engine.edit(current: " 420", range: 0 ..< 1, replacement: "")
        XCTAssertEqual(edit.wireValue, "420")
        XCTAssertEqual(edit.text, "420")
        XCTAssertEqual(edit.caret, 0)
    }

    func testBackspaceOnEmptyFieldIsHarmless() {
        let edit = engine.edit(current: "", range: 0 ..< 0, replacement: "")
        XCTAssertEqual(edit, PhoneMaskEdit(text: "", caret: 0, wireValue: ""))
    }

    func testSelectAllAndDeleteClearsTheField() {
        let edit = engine.edit(current: "+420 728 089 247", range: 0 ..< 16, replacement: "")
        XCTAssertEqual(edit, PhoneMaskEdit(text: "", caret: 0, wireValue: ""))
    }

    func testSelectAllAndReplaceUsesOnlyTheReplacement() {
        let edit = engine.edit(current: "+420 728", range: 0 ..< 8, replacement: "+421")
        XCTAssertEqual(edit.wireValue, "+421")
        XCTAssertEqual(edit.text, "+421")
        XCTAssertEqual(edit.caret, 4)
    }

    func testPasteFormattedNumberIntoEmptyField() {
        let edit = engine.edit(current: "", range: 0 ..< 0, replacement: "+420 728 089 247")
        XCTAssertEqual(edit.wireValue, "+420728089247")
        XCTAssertEqual(edit.text, "+420 728 089 247")
        XCTAssertEqual(edit.caret, 16)
    }

    func testPasteStripsPunctuationAndLetters() {
        let edit = engine.edit(current: "", range: 0 ..< 0, replacement: "call (420) 728-089-247")
        XCTAssertEqual(edit.wireValue, "420728089247")
        XCTAssertEqual(edit.text, "420 728 089 247")
        XCTAssertEqual(edit.caret, 15)
    }

    func testPasteKeepsOnlyTheLeadingPlus() {
        let edit = engine.edit(current: "+420", range: 4 ..< 4, replacement: "+421")
        XCTAssertEqual(edit.wireValue, "+420421")
        XCTAssertEqual(edit.text, "+420 421")
    }

    func testPasteIntoTheMiddleLeavesTheCaretAfterThePastedDigits() {
        let edit = engine.edit(current: "+420 247", range: 5 ..< 5, replacement: "728089")
        XCTAssertEqual(edit.wireValue, "+420728089247")
        XCTAssertEqual(edit.text, "+420 728 089 247")
        XCTAssertEqual(edit.caret, 12)
    }

    func testInsertingADigitInTheMiddleKeepsTheCaretAfterIt() {
        let edit = engine.edit(current: "+420 728 089", range: 8 ..< 8, replacement: "5")
        XCTAssertEqual(edit.wireValue, "+4207285089")
        XCTAssertEqual(edit.text, "+420 728 508 9")
        XCTAssertEqual(edit.caret, 10)
    }

    func testEditRangeBeyondTheTextIsClamped() {
        let edit = engine.edit(current: "+420", range: 10 ..< 20, replacement: "5")
        XCTAssertEqual(edit.wireValue, "+4205")
        XCTAssertEqual(edit.text, "+420 5")
        XCTAssertEqual(edit.caret, 6)
    }

    func testReformatPutsTheCaretAtTheEnd() {
        let edit = engine.reformat("+420728089247")
        XCTAssertEqual(edit.text, "+420 728 089 247")
        XCTAssertEqual(edit.caret, 16)
    }

    func testReformatIsIdempotent() {
        let once = engine.reformat("+420 728089247")
        let twice = engine.reformat(once.text)
        XCTAssertEqual(once, twice)
        XCTAssertEqual(engine.reformat(twice.text), twice)
    }

    func testReformatNormalisesAValueWrittenByTheWeb() {
        XCTAssertEqual(engine.reformat("+420 728089247").wireValue, "+420728089247")
        XCTAssertEqual(engine.reformat("+420 728089247").text, "+420 728 089 247")
    }

    func testReformatOfEmptyValue() {
        XCTAssertEqual(engine.reformat(""), PhoneMaskEdit(text: "", caret: 0, wireValue: ""))
    }

    /// The visible text is the source of the next edit, so a formatter that adds
    /// or loses digits would silently rewrite what we submit.
    func testFormatterThatLosesDigitsIsIgnored() {
        let lying = PhoneMaskEngine(formatter: StubPhoneDisplayFormatter { String($0.dropLast()) })
        let edit = lying.reformat("+420728089247")
        XCTAssertEqual(edit.text, "+420728089247")
        XCTAssertEqual(edit.wireValue, "+420728089247")
    }

    func testFormatterThatInventsDigitsIsIgnored() {
        let lying = PhoneMaskEngine(formatter: StubPhoneDisplayFormatter { $0 + "9" })
        XCTAssertEqual(lying.edit(current: "", range: 0 ..< 0, replacement: "420").text, "420")
    }

    func testFormatterMayUseAnySeparators() {
        let edit = bracketing.edit(current: "", range: 0 ..< 0, replacement: "420728")
        XCTAssertEqual(edit.text, "(420) 728")
        XCTAssertEqual(edit.wireValue, "420728")
        XCTAssertEqual(edit.caret, 9)
    }

    func testBackspaceOverAMultiCharacterSeparator() {
        let edit = bracketing.edit(current: "(420) 728", range: 5 ..< 6, replacement: "")
        XCTAssertEqual(edit.wireValue, "42728")
        XCTAssertEqual(edit.text, "(427) 28")
        XCTAssertEqual(edit.caret, 3)
    }

    /// A non-leading `+` would be dropped the next time the visible text is
    /// re-read, so such a formatter has to be refused outright.
    func testFormatterThatMovesThePlusIsIgnored() {
        let moved = PhoneMaskEngine(formatter: StubPhoneDisplayFormatter { "(\($0))" })
        XCTAssertEqual(moved.reformat("+420728").text, "+420728")
    }

    private var bracketing: PhoneMaskEngine {
        PhoneMaskEngine(formatter: StubPhoneDisplayFormatter { value in
            guard value.count > 3 else { return value }
            return "(\(value.prefix(3))) \(value.dropFirst(3))"
        })
    }

    // MARK: - wire format

    func testWireValueMatchesTheFormatSubmittedToday() {
        let cases = [
            "+420728089247",
            "+420 728089247",
            "+420 728 089 247",
            "+420-728-089-247",
            "(420) 728 089 247",
            "728089247",
            "+",
            ""
        ]
        for input in cases {
            XCTAssertEqual(
                engine.reformat(input).wireValue,
                PhoneNumberSanitizer.sanitize(input),
                "wire format drifted for \(input)"
            )
        }
    }

    func testWireValueNeverCarriesSeparators() {
        for edit in type("+420 (728) 089-247") {
            XCTAssertNil(edit.wireValue.first { !$0.isNumber && $0 != "+" })
        }
    }

    func testWireValueKeepsAtMostOneLeadingPlus() {
        let edit = engine.edit(current: "", range: 0 ..< 0, replacement: "++420+728")
        XCTAssertEqual(edit.wireValue, "+420728")
    }
}
