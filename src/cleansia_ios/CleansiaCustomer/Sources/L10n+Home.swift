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

        /// The formatted balance, and the server's share as a whole percent.
        static func upsellCreditTitle(_ balance: String, share: Double) -> String {
            format("home_upsell_credit_title", balance, L10n.Credit.sharePercent(share))
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

        /// "Offer 2 of 3" — the card's VoiceOver value; swipe up/down moves between offers.
        static func upsellPageA11y(_ position: Int, _ count: Int) -> String {
            format("home_upsell_page_a11y", position, count)
        }

        static var trustInsured: String {
            localized("home_trust_insured")
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
