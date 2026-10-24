import XCTest
@testable import CleansiaCore

/// The case list Android's `acceptOneTimeCode` is held to as well; keep the two in step.
final class OneTimeCodeTests: XCTestCase {
    private func accept(_ raw: String, replacing current: String = "") -> String {
        OneTimeCode.accept(raw, replacing: current, length: 6)
    }

    func testAWholeCodeIntoAnEmptyFieldIsTheCode() {
        XCTAssertEqual(accept("123456"), "123456")
    }

    /// What a mail client puts on the clipboard: the sentence around the code, the code spaced into groups.
    func testTheWordsAndSpacesAroundAPastedCodeAreDropped() {
        XCTAssertEqual(accept("Your code: 123 456"), "123456")
        XCTAssertEqual(accept("123\u{00A0}456"), "123456")
        XCTAssertEqual(accept("Code: 123456."), "123456")
    }

    /// The defect: the old field appended the paste to what was typed and kept the first six digits.
    func testAWholeCodePastedOntoAHalfTypedOneReplacesIt() {
        XCTAssertEqual(accept("12654321", replacing: "12"), "654321")
        XCTAssertEqual(accept("12Your code: 654 321", replacing: "12"), "654321")
    }

    func testAWholeCodePastedOverAFullWrongOneReplacesIt() {
        XCTAssertEqual(accept("123456654321", replacing: "123456"), "654321")
        XCTAssertEqual(accept("654321", replacing: "123456"), "654321", "select all, then paste")
    }

    /// iOS can insert an AutoFilled code twice into a `.oneTimeCode` field.
    func testACodeAutoFilledTwiceIsTheCodeOnce() {
        XCTAssertEqual(accept("123456123456", replacing: "123456"), "123456")
    }

    func testAutoFillThatSetsTheWholeTextOverAPartialCodeIsTheCode() {
        XCTAssertEqual(accept("654321", replacing: "12"), "654321")
        XCTAssertEqual(accept("123456", replacing: "12"), "123456")
    }

    func testTypingGrowsTheCodeOneDigitAtATime() {
        XCTAssertEqual(accept("1"), "1")
        XCTAssertEqual(accept("12", replacing: "1"), "12")
        XCTAssertEqual(accept("123", replacing: "12"), "123")
    }

    func testBackspaceRemovesOneDigit() {
        XCTAssertEqual(accept("12", replacing: "123"), "12")
        XCTAssertEqual(accept("", replacing: "1"), "")
    }

    /// The full-length threshold is what keeps typing off the replace path: several digits typed before a
    /// recomposition still arrive as fewer than a whole code.
    func testDigitsTypedFasterThanTheFieldReportsAreStillTyping() {
        XCTAssertEqual(accept("12345", replacing: "12"), "12345")
    }

    func testASeventhTypedDigitIsIgnored() {
        XCTAssertEqual(accept("1234567", replacing: "123456"), "123456")
    }

    /// By design: a fragment shorter than a code is typing, so it lands after what is there.
    func testAFragmentShorterThanACodeIsAppendedAndClamped() {
        XCTAssertEqual(accept("1234", replacing: "12"), "1234")
        XCTAssertEqual(accept("1234567", replacing: "123"), "123456")
    }

    func testADigitTypedMidCodeLandsWhereItWasTyped() {
        XCTAssertEqual(accept("1923", replacing: "123"), "1923")
    }

    /// The server takes ASCII digits only; a keyboard can type Arabic-Indic or full-width ones.
    func testFullWidthAndArabicIndicDigitsAreDropped() {
        XCTAssertEqual(accept("١٢٣٤٥٦"), "")
        XCTAssertEqual(accept("１２３４５６"), "")
        XCTAssertEqual(accept("12３4", replacing: "12"), "124")
    }

    func testAPasteWithNoDigitsLeavesTheCodeUnchanged() {
        XCTAssertEqual(accept("12hello", replacing: "12"), "12")
        XCTAssertEqual(accept("hello"), "")
    }

    func testTheLengthIsTheCallersCode() {
        XCTAssertEqual(OneTimeCode.accept("12345678", replacing: "", length: 4), "1234")
        XCTAssertEqual(OneTimeCode.accept("12", replacing: "1", length: 4), "12")
    }
}
