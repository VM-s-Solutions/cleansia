import Foundation
import XCTest
@testable import CleansiaCustomer

/// A saved card: the consent wording it is saved under, and the days it can be used.
final class SavedCardTests: XCTestCase {
    /// The server stamps every saved card with the version of the consent wording in force, so the text
    /// the app shows is the one named for that version: bumping the version on the server fails here
    /// until the new wording is in the catalog and is the one rendered.
    func testTheConsentShownIsTheWordingOfTheVersionTheServerRecords() throws {
        let savedCard = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("src/Cleansia.Core.Domain/Users/SavedCard.cs")
        let source = try String(contentsOf: savedCard, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"ConsentTextVersionInForce\s*=\s*"([^"]+)""#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "SavedCard.ConsentTextVersionInForce not found — the parser needs updating"
        )
        let version = try String(source[XCTUnwrap(Range(match.range(at: 1), in: source))])
        let key = "consent_" + version.replacingOccurrences(of: "-", with: "_")

        XCTAssertNotEqual(L10n.localized(key), key, "\(key) is not in the catalog")
        XCTAssertEqual(L10n.Booking.cardGuaranteeConsent, L10n.localized(key))
    }

    /// The server's `SavedCard.IsUsableOn`: good through the last day of its expiry month, in UTC.
    func testACardIsUsableThroughItsExpiryMonth() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let lastDay = try XCTUnwrap(calendar.date(from: DateComponents(year: 2027, month: 8, day: 31, hour: 23)))
        let nextMonth = try XCTUnwrap(calendar.date(from: DateComponents(year: 2027, month: 9, day: 1, hour: 0)))
        let card = PaymentsFixtures.card(id: "c", currencyCode: "CZK", expMonth: 8, expYear: 2027)

        XCTAssertTrue(card.isUsable(on: lastDay))
        XCTAssertFalse(card.isUsable(on: nextMonth))
        XCTAssertEqual(SavedCard.usable(in: [card], currencyCode: "czk", on: lastDay), card)
        XCTAssertNil(SavedCard.usable(in: [card], currencyCode: "EUR", on: lastDay))
    }
}
