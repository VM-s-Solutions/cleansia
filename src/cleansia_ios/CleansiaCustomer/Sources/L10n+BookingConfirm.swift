import Foundation

extension L10n.Booking {
    static var specialInstructionsHint: String {
        L10n.localized("booking_special_instructions_hint")
    }

    static var preferredCleanerTitle: String {
        L10n.localized("booking_preferred_cleaner_title")
    }

    static var preferredCleanerSubtitle: String {
        L10n.localized("booking_preferred_cleaner_subtitle")
    }

    static var preferredCleanerClear: String {
        L10n.localized("booking_preferred_cleaner_clear")
    }

    static var preferredCleanerDialogTitle: String {
        L10n.localized("booking_preferred_cleaner_dialog_title")
    }

    static var cancelTitle: String {
        L10n.localized("booking_cancel_title")
    }

    static var cancelPlusBadge: String {
        L10n.localized("booking_cancel_plus_badge")
    }

    static func cancelPlusSubtitle(_ hours: Int) -> String {
        L10n.format("booking_cancel_plus_subtitle", hours)
    }

    static func cancelTier1WhenPlus(_ hours: Int) -> String {
        L10n.format("booking_cancel_tier1_when_plus", hours)
    }

    static var cancelTier1Value: String {
        L10n.localized("booking_cancel_tier1_value")
    }

    static func cancelTier2WhenRange(_ from: Int, _ to: Int) -> String {
        L10n.format("booking_cancel_tier2_when_range", from, to)
    }

    static var cancelTier2Value: String {
        L10n.localized("booking_cancel_tier2_value")
    }

    static func cancelTier3WhenUnder(_ hours: Int) -> String {
        L10n.format("booking_cancel_tier3_when_under", hours)
    }

    static var cancelTier3Value: String {
        L10n.localized("booking_cancel_tier3_value")
    }

    static var trustInsured: String {
        L10n.localized("booking_trust_insured")
    }

    static var trustVetted: String {
        L10n.localized("booking_trust_vetted")
    }

    static func tierDiscountMinNotMet(_ amount: Int) -> String {
        L10n.format("booking_summary_tier_discount_min_not_met", amount)
    }

    static var successTitle: String {
        L10n.localized("booking_success_title")
    }

    static var successSubtitle: String {
        L10n.localized("booking_success_subtitle")
    }

    static var successConfirmationCode: String {
        L10n.localized("booking_success_confirmation_code")
    }

    static var successGoHome: String {
        L10n.localized("booking_success_go_home")
    }

    static var errorSignInRequired: String {
        L10n.localized("error_booking_sign_in_required")
    }

    static var errorPickTime: String {
        L10n.localized("error_booking_pick_time")
    }

    static var errorGenericNetwork: String {
        L10n.localized("error_generic_network")
    }

    static var busyBooking: String {
        L10n.localized("busy_booking")
    }
}
