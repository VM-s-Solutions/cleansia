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
