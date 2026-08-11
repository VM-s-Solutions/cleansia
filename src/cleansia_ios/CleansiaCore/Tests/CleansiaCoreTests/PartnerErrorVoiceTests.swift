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

    /// Every `BusinessErrorMessage` a partner mobile controller's command can answer with — its own
    /// validator or handler, or a shared validator/service it calls. `CoreL10nCatalogTests` enumerates the
    /// keys the catalog already HAS, so it goes green on a key missing from all five locales; this roster
    /// comes from the backend side instead, which is the only direction that sees one.
    ///
    /// Excluded on purpose: `payment.json_payload_required` / `payment.stripe_signature_required`. They
    /// sit on `/api/Payment/webhook`, which only Stripe's server posts to, so carrying them would read as
    /// coverage of a refusal no app can provoke.
    private static let partnerReachable: [String: String] = [
        "auth.account_locked": "LoginValidator",
        "auth.apple_type_error": "AuthTypeErrorMessages",
        "auth.external_type_error": "AuthTypeErrorMessages",
        "auth.google_type_error": "AuthTypeErrorMessages",
        "auth.insufficient_privileges": "MobilePartnerLogin",
        "auth.internal_type_error": "AuthTypeErrorMessages",
        "auth.invalid_confirmation_code": "ConfirmUserEmail",
        "auth.invalid_google_token": "GoogleAuth",
        "auth.invalid_refresh_token": "RefreshToken",
        "auth.refresh_token_reused": "RefreshToken",
        "auth.social_account_not_found": "GoogleAuth",
        "auth.too_many_attempts": "ConfirmUserEmail",
        "common.max_length": "AddOrderNote, BaseAuthValidator, ReportOrderIssue +7 more",
        "common.required": "AddOrderNote, BaseAuthValidator, CompleteOrder +37 more",
        "company.not_found": "ReceiptService",
        "country.not_existing_id": "UpdateAddressInfo, UpdateEmployee, UpdateIdentificationInfo",
        "country.not_serviced": "UpdateAddressInfo, UpdateEmployee",
        "device.invalid_platform": "RegisterDevice",
        "device.not_found": "RevokeDevice",
        "dispute.max_length_exceeded": "UpdateBankDetails, UpdateEmployee, UpdateIdentificationInfo",
        "email.invalid_format": "BaseAuthValidator, UserEmailValidator",
        "employee.not_allowed_to_update": "UpdateAddressInfo, UpdateAvailability, UpdateBankDetails +4 more",
        "employee.not_approved": "CompleteOrder, MarkCashCollected, StartOrder +1 more",
        "employee.not_found": "GetAvailableJobsPreview, GetDashboardStats, GetEarningsAnalytics +14 more",
        "employee.profile_incomplete": "CompleteOrder, TakeOrder",
        "employee_document.not_found": "DownloadMyDocument",
        "employee_document.not_owned": "DeleteMyDocument",
        "employee_document.unauthorized": "DownloadMyDocument",
        "file.content_type_doesnt_match": "ImageFileValidator",
        "file.count_exceeded": "SaveMyDocuments, SaveOrderPhotos, UpdateEmployee",
        "file.invalid_file_type": "DocumentFileValidator, UploadOrderPhoto",
        "file.required": "SaveOrderPhotos, UploadOrderPhoto",
        "file.size_exceeded": "DocumentFileValidator, ImageFileValidator, SaveOrderPhotos +1 more",
        "file.type_not_allowed": "DocumentFileValidator",
        "gdpr.consent_already_granted": "GrantConsent",
        "gdpr.consent_not_found": "WithdrawConsent",
        "gdpr.deletion_already_pending": "GdprDeletionService",
        "gdpr.deletion_blocked_by_invoice": "GdprDeletionService",
        "gdpr.deletion_blocked_by_order": "GdprDeletionService",
        "general.not_found": "DeleteMyDocument, DeleteOrderIssue, DeleteOrderNote +5 more",
        "language.not_found": "ReceiptService",
        "language.not_supported": "LanguageValidator, UpdateCurrentUser",
        "order.after_photos.required": "CompleteOrder",
        "order.card_payment_already_settled": "MarkCashCollected",
        "order.card_payment_in_progress": "MarkCashCollected",
        "order.card_payment_unverified": "MarkCashCollected",
        "order.cash_already_collected": "MarkCashCollected",
        "order.cash_not_collected": "CompleteOrder",
        "order.completion_notes.too_long": "CompleteOrder",
        "order.employee_already_assigned": "TakeOrder",
        "order.employee_already_has_order_in_progress": "StartOrder",
        "order.employee_not_assigned": "AddOrderNote, CompleteOrder, DeleteOrderIssue +9 more",
        "order.issue.description_required": "UpdateOrderIssue",
        "order.no_available_spots": "TakeOrder",
        "order.not_confirmed": "NotifyOnTheWay, StartOrder",
        "order.not_found": "AddOrderNote, CompleteOrder, DeleteOrderIssue +13 more",
        "order.not_in_progress": "CompleteOrder, MarkCashCollected",
        "order.not_takeable": "TakeOrder",
        "order.note.content_required": "UpdateOrderNote",
        "order.payment_not_confirmed": "CompleteOrder",
        "order.take.already_cancelled": "TakeOrder",
        "order.take.already_completed": "TakeOrder",
        "order.time_conflict": "TakeOrder",
        "order.weekly_limit_reached": "TakeOrder",
        "payout.not_found": "GetMyPayoutDetails",
        "payroll.invoice.not_found": "DownloadInvoice, GetInvoiceById",
        "payroll.pay_period.not_found": "GetPeriodPays",
        "receipt.not_found": "DownloadOrderReceipt",
        "user.email_confirmed": "ResendConfirmationEmail",
        "user.existing_email": "Register, RegisterEmployee",
        "user.existing_phone_number": "UpdateCurrentUser",
        "user.not_allowed_to_update": "UpdateCurrentUser",
        "user.not_existing_email": "CheckCurrentEmployee, GdprDeletionService, LoginValidator +3 more",
        "user.not_found": "ExportUserData, GetMyDocuments, RegisterDevice +1 more",
        "validation.date_must_be_in_past": "UpdateCurrentUser",
        "validation.invalid_age": "UpdateCurrentUser",
        "validation.invalid_availability_format": "UpdateAvailability, UpdateEmployee",
        "validation.invalid_date": "UpdateCurrentUser",
        "validation.invalid_password": "GoogleAuth, Login, LoginValidator +1 more",
        "validation.payout.account_number_required": "PayoutDetailsValidator",
        "validation.payout.country_not_supported": "PayoutDetailsValidator, UpdateBankDetails",
        "validation.payout.iban_country_mismatch": "PayoutDetailsValidator",
        "validation.payout.iban_mismatch": "PayoutDetailsValidator",
        "validation.payout.invalid_account_number": "PayoutDetailsValidator",
        "validation.payout.invalid_account_prefix": "PayoutDetailsValidator",
        "validation.payout.invalid_bank_code": "PayoutDetailsValidator",
        "validation.payout.invalid_iban": "PayoutDetailsValidator",
        "validation.payout.invalid_swift": "PayoutDetailsValidator",
        "validation.payout.looks_like_card": "PayoutDetailsValidator",
        "validation.payout.scheme_not_supported": "PayoutDetailsValidator",
        "validation.payout.swift_required": "PayoutDetailsValidator"
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

    func testEveryPartnerReachableKeyResolvesInAllFiveLocales() {
        var gaps: [String] = []
        for locale in Self.locales {
            for (key, emitters) in Self.partnerReachable {
                let resolved = resolve(key, locale: locale)
                if resolved == key || resolved.isEmpty {
                    gaps.append("\(key) · \(locale) · emitted by \(emitters), shows the raw key")
                }
            }
        }
        assertNoViolations(gaps, "partner-reachable error keys with no Core catalog entry")
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
