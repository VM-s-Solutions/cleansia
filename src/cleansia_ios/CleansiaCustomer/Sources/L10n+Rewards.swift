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
            format("loyalty_bookings_completed", count)
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

        static func discountMinOrder(_ percent: Int, _ minOrder: Int) -> String {
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

        static var referralSubtitle: String {
            localized("loyalty_referral_subtitle")
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

        static func referralShareText(_ code: String, _ url: String) -> String {
            format("loyalty_referral_share_text", code, url)
        }

        static var referralStatsEmpty: String {
            localized("loyalty_referral_stats_empty")
        }

        static func referralStatsWaiting(_ accepted: Int) -> String {
            format("loyalty_referral_stats_waiting", accepted)
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
