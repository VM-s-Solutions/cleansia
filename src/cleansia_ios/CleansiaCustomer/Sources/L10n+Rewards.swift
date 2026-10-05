import Foundation

extension L10n {
    enum Rewards {
        static var title: String {
            localized("rewards_title")
        }

        static func tierLabel(_ tier: LoyaltyTier) -> String {
            switch tier {
            case .bronzeCleaner: localized("loyalty_tier_bronze_cleaner")
            case .silverMopper: localized("loyalty_tier_silver_mopper")
            case .goldPolisher: localized("loyalty_tier_gold_polisher")
            case .platinumSparkler: localized("loyalty_tier_platinum_sparkler")
            }
        }

        static var lifetimePoints: String {
            localized("loyalty_lifetime_points")
        }

        static var pointsUnit: String {
            localized("loyalty_points_unit")
        }

        static func bookingsCompleted(_ count: Int) -> String {
            plural("loyalty_bookings_completed", count)
        }

        static func progressToNext(_ current: Int, _ threshold: Int, _ nextTier: String) -> String {
            format("loyalty_progress_to_next", current, threshold, nextTier)
        }

        static var maxTierReached: String {
            localized("loyalty_max_tier_reached")
        }

        static var currentPerksTitle: String {
            localized("loyalty_current_perks_title")
        }

        static var tierLadderTitle: String {
            localized("loyalty_tier_ladder_title")
        }

        static var statusUnlocked: String {
            localized("loyalty_tier_status_unlocked")
        }

        static var statusCurrent: String {
            localized("loyalty_tier_status_current")
        }

        static var statusLocked: String {
            localized("loyalty_tier_status_locked")
        }

        static func thresholdPoints(_ points: Int) -> String {
            format("loyalty_threshold_points", points)
        }

        static func discountBasic(_ percent: Int) -> String {
            format("loyalty_discount_basic", percent)
        }

        static func discountMinOrder(_ percent: Int, _ minOrder: String) -> String {
            format("loyalty_discount_min_order", percent, minOrder)
        }

        static var noDiscountYet: String {
            localized("loyalty_no_discount_yet")
        }

        static var activityTitle: String {
            localized("loyalty_activity_title")
        }

        static var activityViewAll: String {
            localized("loyalty_activity_view_all")
        }

        static func txEarnOrder(_ points: Int, _ order: String) -> String {
            format("loyalty_tx_earn_order", points, order)
        }

        static func txRevokeOrder(_ points: Int, _ order: String) -> String {
            format("loyalty_tx_revoke_order", points, order)
        }

        static func txRefundOrder(_ points: Int, _ order: String) -> String {
            format("loyalty_tx_refund_order", points, order)
        }

        static func txReferral(_ points: Int) -> String {
            format("loyalty_tx_referral", points)
        }

        static func txManual(_ points: Int) -> String {
            format("loyalty_tx_manual", points)
        }

        static var emptyActivity: String {
            localized("loyalty_empty_activity")
        }

        static var errorLoad: String {
            localized("loyalty_error_load")
        }

        static var retry: String {
            localized("loyalty_retry")
        }

        static var referralSectionTitle: String {
            localized("loyalty_referral_section_title")
        }

        static func referralSubtitle(_ credit: String) -> String {
            format("loyalty_referral_subtitle", credit)
        }

        static var referralSubtitleNoFigure: String {
            localized("loyalty_referral_subtitle_no_figure")
        }

        static var referralShareButton: String {
            localized("loyalty_referral_share_button")
        }

        static var referralCopyButton: String {
            localized("loyalty_referral_copy_button")
        }

        static var referralCopiedToast: String {
            localized("loyalty_referral_copied_toast")
        }

        static func referralShareText(_ credit: String, _ code: String, _ url: String) -> String {
            format("loyalty_referral_share_text", credit, code, url)
        }

        static func referralShareTextNoFigure(_ code: String, _ url: String) -> String {
            format("loyalty_referral_share_text_no_figure", code, url)
        }

        static var referralStatsEmpty: String {
            localized("loyalty_referral_stats_empty")
        }

        static func referralStatsWaiting(_ accepted: Int) -> String {
            plural("loyalty_referral_stats_waiting", accepted)
        }

        static func referralStatsQualified(_ accepted: Int, _ qualified: Int) -> String {
            format("loyalty_referral_stats_qualified", accepted, qualified)
        }

        static var back: String {
            localized("common_back")
        }

        /// Backend perk label keys use dot notation (`loyalty.perks.welcome_badge`);
        /// resolved against the underscore string key, falling back to the raw key
        /// so an unknown future perk stays visible (`resolveLabelKey` parity).
        static func perkLabel(_ labelKey: String?) -> String {
            guard let labelKey, !labelKey.isEmpty else { return "" }
            let resourceKey = labelKey.replacingOccurrences(of: ".", with: "_")
            return bundle.localizedString(forKey: resourceKey, value: labelKey, table: nil)
        }
    }
}

extension L10n {
    /// Credit is money off a booking, not points — see `CustomerCredit`. The share and the dates are the
    /// server's; no row states a percentage or a duration of its own.
    enum Credit {
        static var yourCredit: String {
            localized("credit_your_credit")
        }

        static func autoAppliedShare(_ share: Double) -> String {
            format("credit_auto_applied_share", sharePercent(share))
        }

        static func expiresOn(_ date: Date, locale: Locale) -> String {
            format("credit_expires_on", expiryDate(date, locale: locale))
        }

        static var none: String {
            localized("credit_none")
        }

        static var explainerTitle: String {
            localized("credit_explainer_title")
        }

        static var explainerSourceTitle: String {
            localized("credit_explainer_source_title")
        }

        static var explainerSourceBody: String {
            localized("credit_explainer_source_body")
        }

        static var explainerSpendTitle: String {
            localized("credit_explainer_spend_title")
        }

        static func explainerSpendBody(_ share: Double) -> String {
            format("credit_explainer_spend_body", sharePercent(share))
        }

        static var explainerPointsTitle: String {
            localized("credit_explainer_points_title")
        }

        static var explainerPointsBody: String {
            localized("credit_explainer_points_body")
        }

        static var profileRow: String {
            localized("profile_row_credit")
        }

        static var dueOnCard: String {
            localized("booking_summary_due_on_card")
        }

        static func summaryNote(balance: String) -> String {
            format("booking_summary_credit_note", balance)
        }

        static var cardOnly: String {
            localized("booking_summary_credit_card_only")
        }

        static var paidWithCredit: String {
            localized("order_paid_with_credit")
        }

        static var paidByCard: String {
            localized("order_paid_by_card")
        }

        static var gotIt: String {
            localized("common_got_it")
        }

        /// The server's fraction as whole percent — 0.70 → 70. The `%` and its spacing are the copy's.
        static func sharePercent(_ share: Double) -> Int {
            Int((share * 100).rounded())
        }

        static func expiryDate(_ date: Date, locale: Locale) -> String {
            let formatter = DateFormatter()
            formatter.locale = locale
            formatter.dateStyle = .medium
            formatter.timeStyle = .none
            return formatter.string(from: date)
        }
    }
}
