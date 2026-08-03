import XCTest
@testable import CleansiaCore

/// `ApiErrorLocalizer` resolves `error.*` from the Core catalog alone — no app-bundle probe, no per-app
/// override — so a key only a cleaner can ever receive still ships in the catalog the customer app also
/// reads. Copy for such a key therefore has to be voiced for the cleaner in the shared catalog; the
/// backend splits the key when two personas genuinely need different sentences.
final class PartnerErrorVoiceTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    /// Keys whose every backend emitter is a partner or admin command — no customer-facing command can
    /// produce them, so the shared catalog owes them a cleaner's sentence.
    private static let partnerOnly = [
        PartnerOnlyKey("order.no_available_spots", emitters: "TakeOrder, AdminReassignOrder"),
        PartnerOnlyKey("order.not_takeable", emitters: "TakeOrder"),
        PartnerOnlyKey("order.weekly_limit_reached", emitters: "TakeOrder"),
        PartnerOnlyKey("order.time_conflict", emitters: "TakeOrder"),
        PartnerOnlyKey("order.employee_already_assigned", emitters: "TakeOrder, AdminReassignOrder"),
        PartnerOnlyKey("order.employee_not_assigned", emitters: "StartOrder, CompleteOrder, notes, photos, issues"),
        PartnerOnlyKey("order.employee_already_has_order_in_progress", emitters: "StartOrder"),
        PartnerOnlyKey("order.not_confirmed", emitters: "StartOrder, NotifyOnTheWay"),
        PartnerOnlyKey("order.not_in_progress", emitters: "CompleteOrder, MarkCashCollected"),
        PartnerOnlyKey("order.payment_not_confirmed", emitters: "CompleteOrder"),
        PartnerOnlyKey("order.cash_not_collected", emitters: "CompleteOrder"),
        PartnerOnlyKey("order.cash_already_collected", emitters: "MarkCashCollected"),
        PartnerOnlyKey("order.card_payment_already_settled", emitters: "MarkCashCollected"),
        PartnerOnlyKey("order.card_payment_in_progress", emitters: "MarkCashCollected"),
        PartnerOnlyKey("order.card_payment_unverified", emitters: "MarkCashCollected"),
        PartnerOnlyKey("order.after_photos.required", emitters: "CompleteOrder"),
        PartnerOnlyKey("order.completion_notes.too_long", emitters: "CompleteOrder"),
        PartnerOnlyKey("employee.profile_incomplete", emitters: "TakeOrder, CompleteOrder, ApproveEmployee"),
        PartnerOnlyKey("employee.not_approved", emitters: "TakeOrder, StartOrder, CompleteOrder, MarkCashCollected")
    ]

    /// The words the customer catalog uses for an appointment the reader booked. A cleaner did not book
    /// anything, so any of these in a partner-only key is copy lifted from the customer voice.
    private static let reservationVocabulary = [
        "en": ["booking"],
        "cs": ["rezervac"],
        "sk": ["rezervác", "rezervac"],
        "uk": ["бронюв"],
        "ru": ["бронирован"]
    ]

    /// The take refusal a cleaner actually meets: a seat race they lost. Pinned because every client ships
    /// this sentence and a silent re-word on one of them is the divergence this suite exists to stop.
    private static let boundTakeRefusals = [
        "order.no_available_spots": "Another cleaner has already taken this job.",
        "order.not_takeable": "This job is no longer available.",
        "order.weekly_limit_reached": "You've reached your weekly order limit.",
        "order.time_conflict": "This order conflicts with another you've already taken."
    ]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryPartnerOnlyKeyResolvesToASentenceInAllFiveLocales() {
        var gaps: [String] = []
        for locale in Self.locales {
            for entry in Self.partnerOnly {
                let resolved = resolve(entry.key, locale: locale)
                if resolved == entry.key || resolved.isEmpty {
                    gaps.append("\(entry.key) · \(locale) · emitted by \(entry.emitters), shows the raw key")
                }
            }
        }
        assertNoViolations(gaps, "partner-only error keys with no Core catalog entry")
    }

    func testPartnerOnlyKeysCarryNoCustomerReservationVocabulary() {
        var lifted: [String] = []
        for locale in Self.locales {
            let banned = Self.reservationVocabulary[locale] ?? []
            for entry in Self.partnerOnly {
                let resolved = resolve(entry.key, locale: locale).lowercased()
                for word in banned where resolved.contains(word) {
                    lifted.append("\(entry.key) · \(locale) · \"\(word)\" — emitted only by \(entry.emitters)")
                }
            }
        }
        assertNoViolations(lifted, "partner-only error keys voiced for a customer")
    }

    func testTheTakeRefusalsKeepTheirBoundEnglishCopy() {
        for (key, expected) in Self.boundTakeRefusals {
            XCTAssertEqual(resolve(key, locale: "en"), expected, "\(key) drifted from the bound take-refusal copy")
        }
    }

    func testTheLocalizerReachesThatCopyThroughTheServerErrorKey() {
        CoreL10n.bundle = CoreL10n.localizedBundle(for: "en")
        let localizer = ApiErrorLocalizer()

        for (key, expected) in Self.boundTakeRefusals {
            let error = ApiError(code: key, message: "raw server text", httpStatus: 400)
            XCTAssertEqual(localizer.message(for: error), expected)
        }
    }

    private func resolve(_ key: String, locale: String) -> String {
        let sentinel = "\u{1}"
        let value = CoreL10n.localizedBundle(for: locale)
            .localizedString(forKey: "error." + key, value: sentinel, table: nil)
        return value == sentinel ? key : value
    }

    private func assertNoViolations(
        _ violations: [String],
        _ what: String,
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        guard !violations.isEmpty else { return }
        let listing = violations.sorted().map { "  • \($0)" }.joined(separator: "\n")
        XCTFail("\(violations.count) \(what):\n\(listing)", file: file, line: line)
    }
}

private struct PartnerOnlyKey {
    let key: String
    let emitters: String

    init(_ key: String, emitters: String) {
        self.key = key
        self.emitters = emitters
    }
}
