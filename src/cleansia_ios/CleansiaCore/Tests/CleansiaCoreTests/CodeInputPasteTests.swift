import XCTest
@testable import CleansiaCore

/// The paste button below the boxes answers as the field's own Paste does at the end of the code.
final class CodeInputPasteTests: XCTestCase {
    private func paste(_ text: String, onto code: String = "") -> String {
        CodeInput.pasting(text, onto: code, length: 6)
    }

    func testAPastedSentenceFillsAllSixBoxes() {
        XCTAssertEqual(paste("Your code is 123456"), "123456")
        XCTAssertEqual(paste("Your code is 123 456", onto: "9"), "123456")
    }

    func testAWholeCodeReplacesAHalfTypedOrAWrongOne() {
        XCTAssertEqual(paste("654321", onto: "12"), "654321")
        XCTAssertEqual(paste("654321", onto: "123456"), "654321")
    }

    func testTextWithoutDigitsChangesNothing() {
        XCTAssertEqual(paste("hello", onto: "12"), "12")
        XCTAssertEqual(paste("hello"), "")
    }

    func testAFragmentIsAddedAfterTheCode() {
        XCTAssertEqual(paste("34", onto: "12"), "1234")
        XCTAssertEqual(paste("3456", onto: "123"), "123345")
    }
}
