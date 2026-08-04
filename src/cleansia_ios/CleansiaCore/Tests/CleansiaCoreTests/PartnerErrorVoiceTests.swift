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
        PartnerOnlyKey("order.take.already_cancelled", emitters: "TakeOrder"),
        PartnerOnlyKey("order.take.already_completed", emitters: "TakeOrder"),
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
        PartnerOnlyKey("employee.not_approved", emitters: "TakeOrder, StartOrder, CompleteOrder, MarkCashCollected"),
        PartnerOnlyKey(
            "payout.not_found",
            emitters: "GetMyPayoutDetails, GetEmployeePayoutDetails, RevealEmployeePayoutDetails"
        ),
        PartnerOnlyKey("validation.payout.account_number_required", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.country_not_supported", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.iban_country_mismatch", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.iban_mismatch", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.invalid_account_number", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.invalid_account_prefix", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.invalid_bank_code", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.invalid_iban", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.invalid_swift", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.looks_like_card", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.scheme_not_supported", emitters: "UpdateBankDetails"),
        PartnerOnlyKey("validation.payout.swift_required", emitters: "UpdateBankDetails")
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

    /// What a cleaner is told to type when the field rejects what they typed.
    private static let bankAccountVocabulary = [
        "en": "account number",
        "cs": "účtu",
        "sk": "účtu",
        "uk": "рахунк",
        "ru": "счёт"
    ]

    /// The escape hatch the mismatch copy has to name: an empty IBAN, derived from the parts.
    private static let emptyIbanVocabulary = [
        "en": "clear the iban",
        "cs": "prázdný",
        "sk": "prázdny",
        "uk": "порожнім",
        "ru": "пустым"
    ]

    /// The take refusals a cleaner actually meets. Pinned because every client ships these sentences and a
    /// silent re-word on one of them is the divergence this suite exists to stop.
    private static let boundTakeRefusals = [
        "order.take.already_cancelled": "This order is already cancelled.",
        "order.take.already_completed": "This order is already completed.",
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

    /// The card-number refusal exists to redirect, not just to reject: a cleaner who typed their payment
    /// card into the bank-account field retypes the same digits unless the copy names the real input.
    func testTheCardNumberRefusalNamesWhatToEnterInstead() {
        var vague: [String] = []
        for locale in Self.locales {
            let resolved = resolve("validation.payout.looks_like_card", locale: locale).lowercased()
            let expected = Self.bankAccountVocabulary[locale] ?? ""
            if !resolved.contains(expected) {
                vague.append("\(locale) · never names the account (\"\(expected)\"): \"\(resolved)\"")
            }
        }
        assertNoViolations(vague, "card-number refusals that don't name what to enter instead")
    }

    /// The IBAN/parts mismatch is the one payout refusal with a way out that is not "retype it": leaving
    /// the IBAN empty derives it from the account number and bank code. Copy that only rejects strands a
    /// cleaner who cannot tell which of the three fields the server disagreed with.
    func testTheIbanMismatchRefusalOffersTheEmptyIbanFallback() {
        var silent: [String] = []
        for locale in Self.locales {
            let resolved = resolve("validation.payout.iban_mismatch", locale: locale).lowercased()
            let expected = Self.emptyIbanVocabulary[locale] ?? ""
            if !resolved.contains(expected) {
                silent.append("\(locale) · never offers the empty IBAN (\"\(expected)\"): \"\(resolved)\"")
            }
        }
        assertNoViolations(silent, "IBAN mismatch refusals that don't name the fix")
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
