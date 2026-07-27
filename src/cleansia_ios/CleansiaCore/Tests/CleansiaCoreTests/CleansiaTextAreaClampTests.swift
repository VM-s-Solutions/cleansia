import XCTest
@testable import CleansiaCore

final class CleansiaTextAreaClampTests: XCTestCase {
    func testNilLimitLeavesTextUnchanged() {
        XCTAssertEqual(CleansiaTextArea.clamped("anything at all", to: nil), "anything at all")
        XCTAssertEqual(CleansiaTextArea.clamped("", to: nil), "")
    }

    func testUnderAndAtTheLimitIsUnchanged() {
        XCTAssertEqual(CleansiaTextArea.clamped("abc", to: 5), "abc")
        XCTAssertEqual(CleansiaTextArea.clamped("abcde", to: 5), "abcde")
    }

    func testOverTheLimitIsTruncatedToThePrefix() {
        XCTAssertEqual(CleansiaTextArea.clamped("abcdefgh", to: 5), "abcde")
    }

    func testZeroLimitClampsToEmpty() {
        XCTAssertEqual(CleansiaTextArea.clamped("abc", to: 0), "")
    }

    /// The clamp counts grapheme clusters, not UTF-8 bytes or UTF-16 units — so
    /// a flag emoji (two regional-indicator scalars, four UTF-16 units) is one
    /// character and is never split into an orphaned half. This is the exact
    /// `String.prefix` behaviour the hand-rolled sites had; keeping it means the
    /// visible counter and the clamp can never disagree.
    func testMultiByteGraphemesAreNotSplitAtTheBoundary() {
        XCTAssertEqual(CleansiaTextArea.clamped("🇨🇿🇸🇰🇺🇦", to: 2), "🇨🇿🇸🇰")
        XCTAssertEqual(CleansiaTextArea.clamped("Příliš žluťoučký", to: 6), "Příliš")
        // A combining sequence stays whole: "é" as e + U+0301 is one grapheme.
        XCTAssertEqual(CleansiaTextArea.clamped("e\u{0301}xy", to: 1), "e\u{0301}")
    }
}
