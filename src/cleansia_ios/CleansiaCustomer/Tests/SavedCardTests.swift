import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaCustomer

/// A saved card: the consent wording it is saved under, what the app says it is for, and the days it can
/// be used.
final class SavedCardTests: XCTestCase {
    /// The Payments screen's card copy, and the label of the consent tick on its *Save a card* block.
    private static let savedCardCopy = [
        "payments_card_intro",
        "payments_card_empty",
        "payments_card_add_note",
        "payments_card_remove_message",
        "booking_card_guarantee_title"
    ]

    /// Android's `PaymentsCopyTest` holds the same words against the same strings.
    private static let cashGuaranteeVocabulary = [
        "en": ["cash", "guarantee", "fee"],
        "cs": ["hotovost", "zaruč", "poplat"],
        "sk": ["hotovos", "zaruč", "poplat"],
        "uk": ["готівк", "гарант", "збор"],
        "ru": ["наличн", "гарант", "сбор"]
    ]

    private static let speedClaimVocabulary = [
        "en": ["quick", "fast", "speed"],
        "cs": ["rychl"],
        "sk": ["rýchl"],
        "uk": ["швидк", "швидш"],
        "ru": ["быстр"]
    ]

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

    /// Cash needs no saved card and nothing charges one (→ /product/business-rules#card-guarantee). The
    /// consent sentence printed with the tick is the versioned wording held above, and is not read here.
    func testTheSavedCardCopyNeitherTiesTheCardToCashNorSaysFeesMayBeChargedToIt() throws {
        let offending = try savedCardCopySaying(Self.cashGuaranteeVocabulary)
        XCTAssertTrue(offending.isEmpty, "the saved card is still described as the cash guarantee: \(offending)")
    }

    /// No card payment offers the saved card, so saving one makes no payment quicker.
    func testTheSavedCardCopyDoesNotPromiseQuickerCardPayments() throws {
        let offending = try savedCardCopySaying(Self.speedClaimVocabulary)
        XCTAssertTrue(offending.isEmpty, "the saved card still promises quicker payments: \(offending)")
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

    private func savedCardCopySaying(_ vocabulary: [String: [String]]) throws -> [String] {
        let restoreBundle = L10n.bundle
        let restoreTag = CoreL10n.languageTag
        defer {
            L10n.bundle = restoreBundle
            CoreL10n.apply(languageTag: restoreTag)
        }
        let consentRefusal = ApiError(code: "saved_card.consent_not_accepted", httpStatus: 400)
        var offending: [String] = []
        for (language, stems) in vocabulary {
            L10n.bundle = try localeBundle(language)
            CoreL10n.apply(languageTag: language)
            var copy = Self.savedCardCopy.map { ($0, L10n.localized($0)) }
            copy.append((consentRefusal.code ?? "", ApiErrorLocalizer().message(for: consentRefusal)))
            for (key, value) in copy {
                XCTAssertNotEqual(value, key, "\(key) is not in the \(language) catalog")
                if let word = stems.first(where: { value.lowercased().contains($0) }) {
                    offending.append("\(language)/\(key) says \"\(word)\"")
                }
            }
        }
        return offending
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
