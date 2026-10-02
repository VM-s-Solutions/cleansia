import XCTest
@testable import CleansiaCore

/// The caret of a field whose binding regroups the text (the partner's IBAN, in fours). Assigning the
/// regrouped text to the UITextField puts the caret at the end, so the field puts it back with
/// `regroupedCaret`; these pin that mapping, the same one Android's `IbanGroupsOfFour` draws.
final class CleansiaTextFieldCaretTests: XCTestCase {
    private let raw = "DE89370400440532013000"
    private let grouped = "DE89 3704 0044 0532 0130 00"

    /// The invalid-IBAN copy says it is usually one wrong character: deleting it inside the third group
    /// and typing the right one must land where it was deleted, not at the end.
    func testFixingOneCharacterInsideAGroupKeepsTheCaretOnIt() {
        // "0044" lost its last 4: the caret sits after "DE89 3704 004".
        let afterDelete = "DE89 3704 004 0532 0130 00"
        let regrouped = "DE89 3704 0040 5320 1300 0"
        let caret = CleansiaTextField.regroupedCaret(13, from: afterDelete, to: regrouped)
        XCTAssertEqual(caret, 13)

        // The right digit goes in there, and the IBAN is whole again with the caret after it.
        let afterInsert = "DE89 3704 00440 5320 1300 0"
        XCTAssertEqual(CleansiaTextField.regroupedCaret(14, from: afterInsert, to: grouped), 14)
    }

    func testInsertingIntoTheFirstGroupKeepsTheCaretAfterTheInsertedCharacter() {
        XCTAssertEqual(CleansiaTextField.regroupedCaret(4, from: "DE8X9 3704", to: "DE8X 9370 4"), 4)
    }

    /// Backspacing over a space deletes nothing the form keeps, so the regroup puts the space back; the
    /// caret stays before it instead of jumping to the end.
    func testBackspacingOverASpaceLeavesTheCaretBeforeIt() {
        XCTAssertEqual(CleansiaTextField.regroupedCaret(4, from: "DE893704 0044", to: "DE89 3704 0044"), 4)
    }

    /// A lower-case paste with its own spacing is upper-cased and regrouped; the caret stays at the end.
    func testAPastedIbanKeepsTheCaretAtTheEnd() {
        let pasted = "de89 3704 0044 0532 0130 00"
        XCTAssertEqual(CleansiaTextField.regroupedCaret(27, from: pasted, to: grouped), 27)
    }

    /// Characters the cap drops leave the caret at the end of what is kept.
    func testACaretPastWhatTheCapKeepsGoesToTheEnd() {
        XCTAssertEqual(CleansiaTextField.regroupedCaret(9, from: "DE89 3704", to: "DE89 370"), 8)
        XCTAssertEqual(CleansiaTextField.regroupedCaret(0, from: "DE89 3704", to: "DE89 3704"), 0)
    }

    /// Android's offset mapping: raw offset o is drawn at o + (o - 1) / 4, drawn offset t reads back as
    /// t - t / 5. The iOS caret lands on the same offsets both ways.
    func testTheMappingMatchesAndroidsOffsetMappingBothWays() {
        for original in 0 ... raw.count {
            let drawn = original + max(original - 1, 0) / 4
            XCTAssertEqual(CleansiaTextField.regroupedCaret(original, from: raw, to: grouped), drawn, "raw \(original)")
        }
        for drawn in 0 ... grouped.count {
            XCTAssertEqual(
                CleansiaTextField.regroupedCaret(drawn, from: grouped, to: raw),
                drawn - drawn / 5,
                "drawn \(drawn)"
            )
        }
    }
}
