import Foundation

extension L10n.Booking {
    static var specialInstructionsHint: String {
        L10n.localized("booking_special_instructions_hint")
    }

    static var accessInstructionsHint: String {
        L10n.localized("booking_access_instructions_hint")
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

    static func trustInsured(_ insuranceAmount: String) -> String {
        L10n.format("booking_trust_insured", insuranceAmount)
    }

    static func tierDiscountMinNotMet(_ amount: String) -> String {
        L10n.format("booking_summary_tier_discount_min_not_met", amount)
    }

    static var marketSelectionPruned: String {
        L10n.localized("booking_market_selection_pruned")
    }

    static var cashCleared: String {
        L10n.localized("booking_cash_cleared")
    }

    static var draftTimeChanged: String {
        L10n.localized("booking_draft_time_changed")
    }

    /// Why cash cannot be chosen right now, or nil when it can.
    static func cashReason(_ eligibility: CashEligibility) -> String? {
        switch eligibility {
        case .available: nil
        case .needsAccount: L10n.localized("booking_cash_needs_account")
        case let .needsCard(requiredCleaners): L10n.format("booking_cash_needs_card", requiredCleaners)
        case .needsSavedCard: L10n.localized("booking_cash_needs_saved_card")
        case .cardCheckFailed: L10n.localized("booking_cash_card_check_failed")
        case .pending: L10n.localized("booking_cash_pending")
        }
    }

    static var cancelGraceNote: String {
        L10n.localized("booking_cancel_grace_note")
    }

    static var successTitle: String {
        L10n.localized("booking_success_title")
    }

    static var successDetailsLoading: String {
        L10n.localized("booking_success_details_loading")
    }

    static var successDetailsUnavailable: String {
        L10n.localized("booking_success_details_unavailable")
    }

    static var successGoHome: String {
        L10n.localized("booking_success_go_home")
    }

    static var successTotalLabel: String {
        L10n.localized("booking_success_total_label")
    }

    static var successFinding: String {
        L10n.localized("booking_success_finding")
    }

    static var successFindingSub: String {
        L10n.localized("booking_success_finding_sub")
    }

    static func successCrew(_ count: Int) -> String {
        L10n.plural("booking_success_crew", count)
    }

    static func successCrewNeeded(_ count: Int) -> String {
        L10n.plural("booking_success_crew_needed", count)
    }

    static func successFreeUntil(_ deadline: String) -> String {
        L10n.format("booking_success_free_until", deadline)
    }

    static var successFreeWhile: String {
        L10n.localized("booking_success_free_while")
    }

    static var successNoCleanerCard: String {
        L10n.localized("booking_success_no_cleaner_card")
    }

    static var successNoCleanerCash: String {
        L10n.localized("booking_success_no_cleaner_cash")
    }

    static var successCashOnDay: String {
        L10n.localized("booking_success_cash_on_day")
    }

    static var errorGenericNetwork: String {
        L10n.localized("error_generic_network")
    }

    static var busyBooking: String {
        L10n.localized("busy_booking")
    }

    /// The contract line under the consent block; its link placeholder is pinned by the Core consent
    /// catalog test beside the two consent sentences.
    static var contractNotice: String {
        L10n.localized("booking_contract_notice")
    }

    /// The draft wording the server records as `Order.EarlyPerformanceConsentTextVersionInForce`; a new
    /// wording gets a new key together with a new version on the server.
    static var earlyPerformanceRequest: String {
        L10n.localized("consent_early_performance_draft_2026_09_29")
    }

    static var earlyPerformanceRequestToggle: String {
        L10n.localized("booking_early_performance_toggle")
    }
}
