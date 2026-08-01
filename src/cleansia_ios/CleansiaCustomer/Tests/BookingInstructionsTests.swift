import XCTest
@testable import CleansiaCustomer

/// The cap exists to agree with `CreateOrder`'s `MaximumLength(2000)`, which
/// measures .NET `string.Length` — **UTF-16 code units**. Swift's `String.count`
/// counts grapheme clusters, so a `count`-based cap would be more permissive
/// than the server and let a note through that submit then rejects. Every
/// boundary case here is written in astral-plane text for that reason: an
/// ASCII-only suite passes under either measure and proves nothing.
final class BookingInstructionsTests: XCTestCase {
    private let emoji = "😀"
    private let family = "👨‍👩‍👧‍👦"

    func testShortTextIsReturnedUnchanged() {
        XCTAssertEqual(BookingInstructions.capped("Key under the mat"), "Key under the mat")
    }

    func testTextExactlyAtTheLimitIsReturnedUnchanged() {
        let atLimit = String(repeating: "a", count: BookingInstructions.maxUtf16Length)
        XCTAssertEqual(BookingInstructions.capped(atLimit), atLimit)
    }

    func testTextPastTheLimitIsTruncatedToTheLimit() {
        let overLimit = String(repeating: "a", count: BookingInstructions.maxUtf16Length + 500)
        let capped = BookingInstructions.capped(overLimit)
        XCTAssertEqual(capped.utf16.count, BookingInstructions.maxUtf16Length)
        XCTAssertEqual(capped, String(overLimit.prefix(BookingInstructions.maxUtf16Length)))
    }

    func testTheLimitMatchesTheBackendValidator() {
        XCTAssertEqual(BookingInstructions.maxUtf16Length, 2000)
    }

    /// The headline: a surrogate pair is ONE grapheme but TWO UTF-16 units, so
    /// 1500 emoji are 3000 units to the server — well past the limit — while
    /// `String.count` reads only 1500 and would wave them through.
    func testAstralTextIsMeasuredInUtf16UnitsNotGraphemes() {
        let note = String(repeating: emoji, count: 1500)
        XCTAssertEqual(note.count, 1500)
        XCTAssertEqual(note.utf16.count, 3000)

        let capped = BookingInstructions.capped(note)

        XCTAssertLessThanOrEqual(capped.utf16.count, BookingInstructions.maxUtf16Length)
        XCTAssertEqual(capped.count, 1000)
    }

    /// A cut landing between the halves of a surrogate pair leaves an orphan
    /// that decodes to U+FFFD. The limit falls one unit inside the first emoji
    /// here, so truncation must drop that emoji whole rather than halve it.
    func testTruncationNeverSplitsASurrogatePair() {
        let note = String(repeating: "a", count: BookingInstructions.maxUtf16Length - 1)
            + String(repeating: emoji, count: 10)

        let capped = BookingInstructions.capped(note)

        XCTAssertLessThanOrEqual(capped.utf16.count, BookingInstructions.maxUtf16Length)
        XCTAssertEqual(capped.last, "a")
        XCTAssertFalse(capped.unicodeScalars.contains("\u{FFFD}"))
    }

    /// A ZWJ sequence is a single character spanning 11 UTF-16 units; cutting it
    /// mid-sequence would leave a stray person glyph in the cleaner's note.
    func testTruncationNeverSplitsAZwjSequence() {
        XCTAssertEqual(family.utf16.count, 11)
        let note = String(repeating: family, count: 200)
        XCTAssertGreaterThan(note.utf16.count, BookingInstructions.maxUtf16Length)

        let capped = BookingInstructions.capped(note)

        XCTAssertLessThanOrEqual(capped.utf16.count, BookingInstructions.maxUtf16Length)
        XCTAssertEqual(capped.utf16.count % family.utf16.count, 0)
        XCTAssertTrue(capped.allSatisfy { String($0) == family })
    }

    func testCombiningMarksCountAsTheirUtf16Width() {
        let accented = "e\u{0301}"
        XCTAssertEqual(accented.count, 1)
        XCTAssertEqual(accented.utf16.count, 2)
        let note = String(repeating: accented, count: 1200)

        let capped = BookingInstructions.capped(note)

        XCTAssertLessThanOrEqual(capped.utf16.count, BookingInstructions.maxUtf16Length)
        XCTAssertEqual(capped.count, 1000)
    }

    func testTrimmedOrNilKeepsTheTextWithoutSurroundingWhitespace() {
        XCTAssertEqual(BookingInstructions.trimmedOrNil("  Code 1234  "), "Code 1234")
    }

    func testTrimmedOrNilCollapsesWhitespaceOnlyTextToNil() {
        XCTAssertNil(BookingInstructions.trimmedOrNil("   \n "))
        XCTAssertNil(BookingInstructions.trimmedOrNil(""))
    }
}
