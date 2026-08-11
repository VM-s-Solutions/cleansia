import XCTest
@testable import CleansiaCore

/// The mirror of `PartnerErrorVoiceTests` for the booking refusals only a customer can receive. The
/// express-waiver code is why it exists: the backend gave that refusal its own key precisely because
/// `order.total_price.not_match` — the generic "the price changed" every client renders — is the one
/// sentence that cannot explain a Plus member's free express upgrade running out between the quote and
/// the submit.
final class CustomerErrorVoiceTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    /// Keys whose every backend emitter is a customer booking command, or an admin one no mobile client
    /// renders — either way the booking voice is the only voice they are read in.
    private static let customerOnly = [
        CustomerOnlyKey("membership.express_waiver.no_longer_available", emitters: "CreateOrder"),
        CustomerOnlyKey(
            "order.already_cancelled",
            emitters: "CancelOrder, AdminCancelOrder, AdminOverrideOrderStatus"
        ),
        CustomerOnlyKey(
            "order.already_completed",
            emitters: "CancelOrder, AdminCancelOrder, AdminOverrideOrderStatus"
        ),
        CustomerOnlyKey("order.span_exceeds_maximum", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.empty", emitters: "CreateOrder"),
        CustomerOnlyKey("order.address_exactly_one_required", emitters: "CreateOrder"),
        CustomerOnlyKey("order.cleaning_date.future", emitters: "CreateOrder"),
        CustomerOnlyKey("order.cleaning_date.below_lead_time", emitters: "CreateOrder"),
        CustomerOnlyKey("order.selected_services.invalid", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.selected_package.invalid", emitters: "CreateOrder, QuoteOrder"),
        CustomerOnlyKey("order.preferred_employee.not_eligible", emitters: "CreateOrder"),
        CustomerOnlyKey("order.total_price.not_match", emitters: "CreateOrder"),
        CustomerOnlyKey(
            "recurring_booking.not_found",
            emitters: "UpdateRecurringBooking, SetRecurringBookingActive, DeleteRecurringBooking"
        ),
        CustomerOnlyKey(
            "recurring_booking.not_owned_by_user",
            emitters: "UpdateRecurringBooking, SetRecurringBookingActive, DeleteRecurringBooking"
        ),
        CustomerOnlyKey(
            "recurring_booking.saved_address_not_found",
            emitters: "CreateRecurringBooking, UpdateRecurringBooking"
        ),
        CustomerOnlyKey(
            "recurring_booking.no_services_or_packages",
            emitters: "CreateRecurringBooking, UpdateRecurringBooking"
        ),
        CustomerOnlyKey("recurring_booking.starts_on_in_past", emitters: "CreateRecurringBooking"),
        CustomerOnlyKey(
            "recurring_booking.ends_on_before_start",
            emitters: "CreateRecurringBooking, UpdateRecurringBooking"
        ),
        CustomerOnlyKey(
            "recurring_booking.membership_required",
            emitters: "CreateRecurringBooking, UpdateRecurringBooking"
        )
    ]

    /// Every `BusinessErrorMessage` a customer mobile controller's command can answer with — its own
    /// validator or handler, or a shared validator/service it calls. `CoreL10nCatalogTests` enumerates the
    /// keys the catalog already HAS, so it goes green on a key missing from all five locales; this roster
    /// comes from the backend side instead, which is the only direction that sees one.
    ///
    /// Excluded on purpose: `payment.json_payload_required` / `payment.stripe_signature_required`. They
    /// sit on `/api/Payment/webhook`, which only Stripe's server posts to, so carrying them would read as
    /// coverage of a refusal no app can provoke.
    private static let customerReachable: [String: String] = [
        "address.already_exists": "AddSavedAddress",
        "address.label_required": "AddSavedAddress, UpdateSavedAddress",
        "address.mapbox_coords_required": "AddSavedAddress, UpdateSavedAddress",
        "address.not_owned_by_user": "SetDefaultSavedAddress, UpdateSavedAddress",
        "auth.account_locked": "LoginValidator",
        "auth.apple_type_error": "AuthTypeErrorMessages",
        "auth.external_type_error": "AuthTypeErrorMessages",
        "auth.google_type_error": "AuthTypeErrorMessages",
        "auth.internal_type_error": "AuthTypeErrorMessages",
        "auth.invalid_apple_token": "AppleAuth",
        "auth.invalid_confirmation_code": "ConfirmUserEmail",
        "auth.invalid_google_token": "GoogleAuth",
        "auth.invalid_refresh_token": "RefreshToken",
        "auth.invalid_reset_token": "ChangePassword",
        "auth.refresh_token_reused": "RefreshToken",
        "auth.same_reset_password": "ChangePassword",
        "auth.social_account_not_found": "AppleAuth, GoogleAuth",
        "auth.too_many_attempts": "ChangePassword, ConfirmUserEmail",
        "city.not_serviced": "OrderAddressResolver",
        "common.invalid_enum_value": "ConfirmRecurringOrder, CreateDispute, CreateOrder +3 more",
        "common.max_length": "AddSavedAddress, AppleAuth, BaseAuthValidator +8 more",
        "common.min_length": "AddSavedAddress, CreateDispute, CreateOrder +1 more",
        "common.required": "AddDisputeMessage, AddSavedAddress, AppleAuth +38 more",
        "company.not_found": "ReceiptService",
        "country.not_existing_id": "AddSavedAddress, UpdateSavedAddress",
        "country.not_serviced": "OrderAddressResolver",
        "country.required": "OrderAddressResolver",
        "currency.invalid": "CreateOrder, QuoteOrder",
        "device.invalid_platform": "RegisterDevice",
        "device.not_found": "RevokeDevice",
        "dispute.already_exists": "CreateDispute",
        "dispute.max_length_exceeded": "AddDisputeMessage, CreateDispute",
        "dispute.not_found": "AddDisputeMessage, UploadDisputeEvidence",
        "dispute.not_owned_by_user": "AddDisputeMessage, UploadDisputeEvidence",
        "email.invalid_format": "BaseAuthValidator, CreateOrder, UserEmailValidator",
        "file.content_type_doesnt_match": "ImageFileValidator",
        "file.invalid_file_type": "UploadDisputeEvidence",
        "file.required": "UploadDisputeEvidence",
        "file.size_exceeded": "ImageFileValidator, UploadDisputeEvidence",
        "gdpr.consent_already_granted": "GrantConsent",
        "gdpr.consent_not_found": "WithdrawConsent",
        "gdpr.deletion_already_pending": "GdprDeletionService",
        "gdpr.deletion_blocked_by_invoice": "GdprDeletionService",
        "gdpr.deletion_blocked_by_order": "GdprDeletionService",
        "general.not_found": "MarkNotificationRead, OrderAddressResolver, SetDefaultSavedAddress +1 more",
        "language.not_found": "ReceiptService",
        "language.not_supported": "LanguageValidator, UpdateCurrentUser",
        "live_activity.order_not_active": "RegisterLiveActivityToken",
        "membership.already_active": "CreateMembershipCheckoutSession, CreateMembershipSubscription",
        "membership.express_waiver.no_longer_available": "CreateOrder",
        "membership.not_found": "CancelMembershipSubscription, SwapMembershipPlan",
        "membership.plan.not_found":
            "CreateMembershipCheckoutSession, CreateMembershipSubscription, SwapMembershipPlan",
        "membership.swap_same_plan": "SwapMembershipPlan",
        "order.address_exactly_one_required": "CreateOrder",
        "order.already_cancelled": "CancellationAssessor",
        "order.already_completed": "CancellationAssessor",
        "order.cleaning_date.below_lead_time": "CreateOrder",
        "order.cleaning_date.future": "CreateOrder",
        "order.empty": "CreateOrder",
        "order.in_progress_cannot_cancel": "CancellationAssessor",
        "order.not_completed": "SubmitOrderReview",
        "order.not_found": "CancelOrder, ConfirmRecurringOrder, CreateDispute +10 more",
        "order.payment_gateway_unavailable":
            "CancelMembershipSubscription, CreateMembershipCheckoutSession, CreateMembershipSubscription +3 more",
        "order.preferred_employee.not_eligible": "CreateOrder, CreateRecurringBooking",
        "order.review.rating_invalid": "SubmitOrderReview",
        "order.selected_package.invalid": "CreateOrder, QuoteOrder",
        "order.selected_services.invalid": "CreateOrder, QuoteOrder",
        "order.span_exceeds_maximum": "CreateOrder, QuoteOrder",
        "order.total_price.not_match": "CreateOrder",
        "order.total_price.positive": "CreateOrder",
        "receipt.not_found": "DownloadOrderReceipt",
        "recurring_booking.ends_on_before_start": "CreateRecurringBooking, UpdateRecurringBooking",
        "recurring_booking.membership_required": "CreateRecurringBooking, UpdateRecurringBooking",
        "recurring_booking.no_services_or_packages": "CreateRecurringBooking, UpdateRecurringBooking",
        "recurring_booking.not_found": "DeleteRecurringBooking, SetRecurringBookingActive, UpdateRecurringBooking",
        "recurring_booking.not_owned_by_user":
            "DeleteRecurringBooking, SetRecurringBookingActive, UpdateRecurringBooking",
        "recurring_booking.saved_address_not_found": "CreateRecurringBooking, UpdateRecurringBooking",
        "recurring_booking.starts_on_in_past": "CreateRecurringBooking",
        "refund.failed": "RefundService",
        "refund.nothing_refundable": "RefundService",
        "refund.order_not_refundable": "RefundService",
        "user.email_confirmed": "ResendConfirmationEmail",
        "user.existing_email": "Register",
        "user.existing_phone_number": "UpdateCurrentUser",
        "user.not_allowed_to_update": "UpdateCurrentUser",
        "user.not_existing_email": "ChangePassword, GdprDeletionService, LoginValidator +3 more",
        "user.not_found":
            "ConfirmRecurringOrder, CreateMembershipCheckoutSession, CreateMembershipSubscription +6 more",
        "validation.date_must_be_in_past": "UpdateCurrentUser",
        "validation.invalid_age": "UpdateCurrentUser",
        "validation.invalid_date": "UpdateCurrentUser",
        "validation.invalid_password": "AppleAuth, GoogleAuth, Login +2 more",
        "validation.must_be_positive": "QuoteOrder, ValidatePromoCode"
    ]

    /// The word for the express slot in each locale, as the booking summary already spells it.
    private static let expressVocabulary = [
        "en": "express",
        "cs": "expres",
        "sk": "expres",
        "uk": "експрес",
        "ru": "экспресс"
    ]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryCustomerReachableKeyResolvesInAllFiveLocales() {
        var gaps: [String] = []
        for locale in Self.locales {
            for (key, emitters) in Self.customerReachable {
                let resolved = resolve(key, locale: locale)
                if resolved == key || resolved.isEmpty {
                    gaps.append("\(key) · \(locale) · emitted by \(emitters), shows the raw key")
                }
            }
        }
        assertNoViolations(gaps, "customer-reachable error keys with no Core catalog entry")
    }

    func testEveryCustomerOnlyKeyResolvesToASentenceInAllFiveLocales() {
        var gaps: [String] = []
        for locale in Self.locales {
            for entry in Self.customerOnly {
                let resolved = resolve(entry.key, locale: locale)
                if resolved == entry.key || resolved.isEmpty {
                    gaps.append("\(entry.key) · \(locale) · emitted by \(entry.emitters), shows the raw key")
                }
            }
        }
        assertNoViolations(gaps, "customer-only error keys with no Core catalog entry")
    }

    func testTheExpressWaiverRefusalNamesTheExpressSurcharge() {
        var vague: [String] = []
        for locale in Self.locales {
            let word = Self.expressVocabulary[locale] ?? ""
            let resolved = resolve("membership.express_waiver.no_longer_available", locale: locale)
            if !resolved.lowercased().contains(word) {
                vague.append("\(locale) · never says \"\(word)\": \"\(resolved)\"")
            }
        }
        assertNoViolations(vague, "express-waiver refusals that don't say what changed")
    }

    func testTheExpressWaiverRefusalIsNotTheGenericPriceChange() {
        for locale in Self.locales {
            XCTAssertNotEqual(
                resolve("membership.express_waiver.no_longer_available", locale: locale),
                resolve("order.total_price.not_match", locale: locale),
                "\(locale): the express waiver reads as the generic price-changed refusal it was split off from"
            )
        }
    }

    func testTheLocalizerReachesTheExpressWaiverCopyThroughTheServerErrorKey() {
        CoreL10n.bundle = CoreL10n.localizedBundle(for: "en")
        let error = ApiError(
            code: "membership.express_waiver.no_longer_available",
            message: "raw server text",
            httpStatus: 400
        )

        XCTAssertEqual(
            ApiErrorLocalizer().message(for: error),
            resolve("membership.express_waiver.no_longer_available", locale: "en")
        )
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

private struct CustomerOnlyKey {
    let key: String
    let emitters: String

    init(_ key: String, emitters: String) {
        self.key = key
        self.emitters = emitters
    }
}
