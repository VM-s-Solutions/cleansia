import Foundation
import XCTest

/// A customer who owes any amount makes no new booking, by cash or by card (owner ruling 2026-10-06). The Payments
/// page, where the amount is paid, no longer promises that card bookings go on (Android's `PaymentsCopyTest`).
final class DebtCopyTests: XCTestCase {
    private static let cardStillAllowed = [
        "en": ["not affected", "pay by card for now"],
        "cs": ["netýká", "zatím zaplaťte kartou"],
        "sk": ["netýka", "zatiaľ zaplaťte kartou"],
        "uk": ["не стосується", "поки що оплатіть карткою"],
        "ru": ["не касается", "пока оплатите картой"]
    ]

    func testThePaymentsPageNeverPromisesThatCardBookingsGoOn() throws {
        let entry = try XCTUnwrap(customerStrings()["payments_due_intro"] as? [String: Any])
        let localizations = try XCTUnwrap(entry["localizations"] as? [String: Any])
        for (locale, phrases) in Self.cardStillAllowed {
            let unit = (localizations[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
            let value = try XCTUnwrap(unit?["value"] as? String, "\(locale) lost payments_due_intro").lowercased()
            for phrase in phrases {
                XCTAssertFalse(value.contains(phrase), "\(locale) still says \"\(phrase)\": \(value)")
            }
        }
    }

    private func customerStrings() throws -> [String: Any] {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Resources/Localizable.xcstrings")
        let catalog = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        return try XCTUnwrap(catalog["strings"] as? [String: Any])
    }
}
