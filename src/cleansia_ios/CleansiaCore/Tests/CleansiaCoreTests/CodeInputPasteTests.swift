import XCTest
@testable import CleansiaCore

/// The Paste button under the boxes replaces what was typed with the clipboard's code, so a fragment shows as
/// itself, incomplete, and is never completed from digits typed before it. The screens submit a code when it
/// changes to a whole one, which is what `submitted` reads.
final class CodeInputPasteTests: XCTestCase {
    private let length = 6

    private func paste(_ clipboard: String, onto code: String) -> (code: String, submitted: Bool) {
        let pasted = CodeInput.pasting(clipboard, onto: code, length: length)
        return (pasted, pasted != code && pasted.count == length)
    }

    func testAWholeCodeFillsTheBoxesAndIsSubmitted() {
        let result = paste("123456", onto: "")
        XCTAssertEqual(result.code, "123456")
        XCTAssertTrue(result.submitted)
    }

    /// Run together with the digits typed before it, the fragment would make a whole code and spend one of
    /// the five attempts on it.
    func testAFragmentReplacesWhatWasTypedAndIsNotSubmitted() {
        let result = paste("3456", onto: "12")
        XCTAssertEqual(result.code, "3456")
        XCTAssertFalse(result.submitted)
    }

    func testTheCodeInASentenceReplacesWhatWasTypedAndIsSubmitted() {
        let result = paste("Your code is 654321.", onto: "12")
        XCTAssertEqual(result.code, "654321")
        XCTAssertTrue(result.submitted)
    }

    func testAClipboardWithoutDigitsLeavesTheCodeUnchanged() {
        let result = paste("abc", onto: "12")
        XCTAssertEqual(result.code, "12")
        XCTAssertFalse(result.submitted)
    }

    func testARunLongerThanACodeGivesItsFirstSixDigitsAndIsSubmitted() {
        let result = paste("1234567", onto: "123")
        XCTAssertEqual(result.code, "123456")
        XCTAssertTrue(result.submitted)
    }

    func testAWholeCodeReplacesAWrongOne() {
        let result = paste("654321", onto: "123456")
        XCTAssertEqual(result.code, "654321")
        XCTAssertTrue(result.submitted)
    }
}
