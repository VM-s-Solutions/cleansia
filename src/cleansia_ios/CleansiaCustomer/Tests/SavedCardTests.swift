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
        XCTAssertEqual(L10n.Booking.savedCardConsent, L10n.localized(key))
    }

    func testThePaymentsCopyExplainsThatCashRequiresASavedCardInEveryLocale() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        let cashWords = ["en": "cash", "cs": "hotov", "sk": "hotov", "uk": "готів", "ru": "налич"]
        for (language, word) in cashWords {
            L10n.bundle = try localeBundle(language)
            for key in ["payments_card_intro", "payments_card_empty", "payments_card_remove_message"] {
                XCTAssertTrue(L10n.localized(key).lowercased().contains(word), "\(language)/\(key)")
            }
        }
    }

    /// Requiring a card for cash does not claim that saving it makes card payments faster.
    /// → /product/business-rules#saved-cards
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
            copy.append(("savedCardConsent", L10n.Booking.savedCardConsent))
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

extension PaymentsViewModelTests {
    func finish(_ vm: PaymentsViewModel, _ outcome: PaymentSheetOutcome) async {
        guard let attempt = vm.cardCapture?.attempt else { return XCTFail("no current capture") }
        _ = vm.claimCardSheet(attempt)
        await vm.cardSheetFinished(outcome, attempt: attempt)
    }

    func testEveryKindTheServerSendsHasItsOwnLabelAndANewOneReadsAsAmountDue() {
        XCTAssertEqual(Receivable.Kind(wireValue: 1), .cancellationFee)
        XCTAssertEqual(Receivable.Kind(wireValue: 2), .lockout)
        XCTAssertEqual(Receivable.Kind(wireValue: 3), .unpaidCash)
        XCTAssertEqual(Receivable.Kind(wireValue: 4), .topUp)
        XCTAssertEqual(Receivable.Kind(wireValue: 9), .other)

        let labels = [Receivable.Kind.cancellationFee, .lockout, .unpaidCash, .topUp, .other].map(L10n.Payments.kind)
        XCTAssertEqual(Set(labels).count, labels.count, "two kinds read the same")
        XCTAssertFalse(labels.contains { $0.hasPrefix("receivable_kind_") }, "a kind shows its raw key")
    }
}
