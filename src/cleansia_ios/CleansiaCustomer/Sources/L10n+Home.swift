import Foundation

extension L10n {
    enum Home {
        static var addressLabel: String {
            localized("home_address_label")
        }

        static var addressPlaceholder: String {
            localized("home_address_placeholder")
        }

        static var upsellNotificationsTop: String {
            localized("home_upsell_notifications_top")
        }

        static var upsellNotificationsTitle: String {
            localized("home_upsell_notifications_title")
        }

        static var upsellNotificationsCta: String {
            localized("home_upsell_notifications_cta")
        }

        /// The formatted balance.
        static func upsellCreditTitle(_ balance: String) -> String {
            format("home_upsell_credit_title", balance)
        }

        /// The server's share as a whole percent.
        static func upsellCreditDesc(share: Double) -> String {
            format("home_upsell_credit_desc", L10n.Credit.sharePercent(share))
        }

        /// The express window, from the booking bands the server pins.
        static func upsellExpressTop(_ fromHours: Int, _ toHours: Int) -> String {
            format("home_upsell_express_top", fromHours, toHours)
        }

        static func upsellExpressTitle(_ remaining: Int) -> String {
            plural("home_upsell_express_title", remaining)
        }

        static var upsellBookCta: String {
            localized("home_upsell_book_cta")
        }

        static var quickSizeTitle: String {
            localized("home_quick_size_title")
        }

        static var quickSizeCta: String {
            localized("home_quick_size_cta")
        }

        static var quickSizeRoomsLess: String {
            localized("home_quick_size_rooms_less")
        }

        static var quickSizeRoomsMore: String {
            localized("home_quick_size_rooms_more")
        }

        static var quickSizeBathsLess: String {
            localized("home_quick_size_baths_less")
        }

        static var quickSizeBathsMore: String {
            localized("home_quick_size_baths_more")
        }

        static var upsellPlusTop: String {
            localized("home_upsell_plus_top")
        }

        static var upsellPlusTitle: String {
            localized("home_upsell_plus_title")
        }

        static var upsellPlusCta: String {
            localized("home_upsell_plus_cta")
        }

        static func upsellPlusTitleTrial(_ days: Int) -> String {
            format("home_upsell_plus_title_trial", days)
        }

        static var upsellPlusCtaTrial: String {
            localized("home_upsell_plus_cta_trial")
        }

        static var upsellSetupRecurringTop: String {
            localized("home_upsell_setup_recurring_top")
        }

        static var upsellSetupRecurringTitle: String {
            localized("home_upsell_setup_recurring_title")
        }

        static var upsellSetupRecurringCta: String {
            localized("home_upsell_setup_recurring_cta")
        }

        static var upsellReferralTop: String {
            localized("home_upsell_referral_top")
        }

        static var upsellReferralTitle: String {
            localized("home_upsell_referral_title")
        }

        static var upsellReferralCta: String {
            localized("home_upsell_referral_cta")
        }

        static var upsellDidYouKnow: String {
            localized("home_upsell_did_you_know")
        }

        static var upsellNotificationsDesc: String {
            localized("home_upsell_notifications_desc")
        }

        static var upsellExpressDesc: String {
            localized("home_upsell_express_desc")
        }

        static var upsellSetupRecurringDesc: String {
            localized("home_upsell_setup_recurring_desc")
        }

        /// The headline plan's discount, in whole percent, from the plans the server lists.
        static func upsellPlusDesc(_ percent: Int) -> String {
            format("home_upsell_plus_desc", percent)
        }

        static var upsellPlusDescGeneric: String {
            localized("home_upsell_plus_desc_generic")
        }

        /// The server's `pointsPerReferral`.
        static func upsellReferralDesc(_ points: Int) -> String {
            plural("home_upsell_referral_desc", points)
        }

        static var upsellReferralDescGeneric: String {
            localized("home_upsell_referral_desc_generic")
        }

        /// The member's own free-cancellation window, as the membership reports it.
        static func upsellPlusCancelTitle(_ hours: Int) -> String {
            format("home_upsell_plus_cancel_title", hours)
        }

        static var upsellPlusCancelDesc: String {
            localized("home_upsell_plus_cancel_desc")
        }

        /// The booking policy's express lead.
        static func upsellExpressTodayTitle(_ hours: Int) -> String {
            format("home_upsell_express_today_title", hours)
        }

        /// The booking policy's standard lead, below which the surcharge applies.
        static func upsellExpressTodayDesc(_ hours: Int) -> String {
            format("home_upsell_express_today_desc", hours)
        }

        static var upsellRewardsTitle: String {
            localized("home_upsell_rewards_title")
        }

        static var upsellRewardsDesc: String {
            localized("home_upsell_rewards_desc")
        }

        static var upsellRewardsCta: String {
            localized("home_upsell_rewards_cta")
        }

        static var upsellTimesTitle: String {
            localized("home_upsell_times_title")
        }

        /// The first and the last arrival time the booking offers.
        static func upsellTimesDesc(_ first: String, _ last: String) -> String {
            format("home_upsell_times_desc", first, last)
        }

        static func upsellChipPercentOff(_ percent: Int) -> String {
            format("home_upsell_chip_percent_off", percent)
        }

        static func upsellChipPoints(_ points: Int) -> String {
            format("home_upsell_chip_points", points)
        }

        static func upsellChipHours(_ hours: Int) -> String {
            format("home_upsell_chip_hours", hours)
        }

        static func upsellChipMinutes(_ minutes: Int) -> String {
            format("home_upsell_chip_minutes", minutes)
        }

        static func upsellChipTimes(_ count: Int) -> String {
            format("home_upsell_chip_times", count)
        }

        /// "Offer 2 of 3" — the card's VoiceOver value; swipe up/down moves between offers.
        static func upsellPageA11y(_ position: Int, _ count: Int) -> String {
            format("home_upsell_page_a11y", position, count)
        }

        static var trustSameDay: String {
            localized("home_trust_same_day")
        }

        static var orderAgainTitle: String {
            localized("home_order_again_title")
        }

        static func orderAgainSubtitle(_ when: String) -> String {
            format("home_order_again_subtitle", when)
        }

        static var orderAgainFallbackTitle: String {
            localized("home_order_again_fallback_title")
        }

        static var recurringSectionTitle: String {
            localized("home_recurring_section_title")
        }

        static var recurringSectionManage: String {
            localized("home_recurring_section_manage")
        }

        static var popularPackagesTitle: String {
            localized("home_popular_packages_title")
        }

        static var popularPackagesAddCta: String {
            localized("home_popular_packages_add_cta")
        }

        static var recentTitle: String {
            localized("home_recent_title")
        }

        static var recentSeeAll: String {
            localized("home_recent_see_all")
        }

        static var recentFallbackTitle: String {
            localized("home_recent_fallback_title")
        }

        static func milestoneTitle(_ currentTier: String) -> String {
            format("home_milestone_title_v2", currentTier)
        }

        static func milestoneSubtitle(_ pointsToNext: Int, _ nextTier: String) -> String {
            plural("home_milestone_subtitle_v2", pointsToNext, nextTier)
        }
    }

    enum Market {
        static func chipA11y(_ marketName: String, _ currencyCode: String) -> String {
            format("market_chip_a11y", marketName, currencyCode)
        }
    }
}
