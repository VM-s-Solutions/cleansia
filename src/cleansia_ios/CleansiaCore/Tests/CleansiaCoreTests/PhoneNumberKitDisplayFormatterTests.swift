import XCTest
@testable import CleansiaCore

final class PhoneNumberKitDisplayFormatterTests: XCTestCase {
    private let formatter = PhoneNumberKitDisplayFormatter(defaultRegion: "CZ")

    func testEmptyDisplaysEmpty() {
        XCTAssertEqual(formatter.display(""), "")
    }

    func testGroupsCzechNumber() {
        XCTAssertEqual(formatter.display("+420728089247"), "+420 728 089 247")
    }

    func testGroupsSlovakNumber() {
        XCTAssertEqual(formatter.display("+421905123456"), "+421 905 123 456")
    }

    func testEveryPartialOfEveryMarketKeepsItsDigits() {
        for number in Self.markets {
            for length in 1 ... number.count {
                let partial = String(number.prefix(length))
                XCTAssertEqual(
                    PhoneNumberSanitizer.sanitize(formatter.display(partial)),
                    partial,
                    "formatting changed the digits of \(partial)"
                )
            }
        }
    }

    /// A formatter the engine refuses falls back to the bare digits, which would
    /// look like the mask silently stopped working.
    func testEveryMarketIsActuallyMasked() {
        for number in Self.markets where number.count > 6 {
            XCTAssertNotEqual(formatter.display(number), number, "\(number) came back unformatted")
        }
    }

    private static let markets = [
        "+420728089247",
        "+421905123456",
        "+380501234567",
        "+79161234567",
        "+491701234567",
        "+48512345678",
        "+12025550123",
        "728089247"
    ]

    /// Only what cannot drift from the server: the international form, and a length the number's country
    /// allows. +420 609 … has a Czech number's nine digits in a range this library's metadata does not know,
    /// which is how a range allocated since its release looks here; the server, on newer metadata, judges it.
    func testANumberIsPossibleOnlyInternationalAndOfALengthItsCountryAllows() {
        for possible in ["+420777123456", "+421905123456", "+420 728 089 247", "+420609123456"] {
            XCTAssertTrue(PhoneNumberKitDisplayFormatter.isPossibleNumber(possible), possible)
        }
        for impossible in ["777123456", "420777123456", "+", "", "+1", "+4201", "+4219051234567"] {
            XCTAssertFalse(PhoneNumberKitDisplayFormatter.isPossibleNumber(impossible), impossible)
        }
    }

    /// No country has these codes, so no length can make them a number, as libphonenumber, on the server and
    /// on Android, says too. This library reads such a number as one of the handset's own region instead.
    func testANumberWithACountryCodeNoCountryHasIsNotPossible() {
        for unassigned in ["+0123456789", "+9991234567", "+999 123 456 789"] {
            XCTAssertFalse(PhoneNumberKitDisplayFormatter.isPossibleNumber(unassigned), unassigned)
        }
    }

    func testEngineWiredToTheRealFormatterKeepsTheWireFormat() {
        let engine = PhoneMaskEngine(formatter: formatter)
        XCTAssertEqual(engine.reformat("+420 728089247").wireValue, "+420728089247")
        XCTAssertEqual(engine.reformat("+420 728089247").text, "+420 728 089 247")
        XCTAssertEqual(engine.reformat("+420 728 089 247"), engine.reformat("+420728089247"))
    }

    func testEngineWiredToTheRealFormatterTypesAndBackspaces() {
        let engine = PhoneMaskEngine(formatter: formatter)
        var text = ""
        for key in "+420728089247" {
            text = engine.edit(current: text, range: text.count ..< text.count, replacement: String(key)).text
        }
        XCTAssertEqual(text, "+420 728 089 247")

        let separator = 4
        let afterBackspace = engine.edit(current: text, range: separator ..< separator + 1, replacement: "")
        XCTAssertEqual(afterBackspace.wireValue, "+42728089247")
    }
}
