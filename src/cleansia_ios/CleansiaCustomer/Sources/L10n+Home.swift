import Foundation

extension L10n {
    enum Home {
        static var addressLabel: String {
            localized("home_address_label")
        }

        static var addressPlaceholder: String {
            localized("home_address_placeholder")
        }

        static var heroGreeting: String {
            localized("home_hero_greeting")
        }

        static var heroPrompt: String {
            localized("home_hero_prompt")
        }

        static var heroCta: String {
            localized("home_hero_cta")
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

        static var upsellSetupRecurringTop: String {
            localized("home_upsell_setup_recurring_top")
        }

        static var upsellSetupRecurringTitle: String {
            localized("home_upsell_setup_recurring_title")
        }

        static var upsellSetupRecurringCta: String {
            localized("home_upsell_setup_recurring_cta")
        }

        static var upsellWelcomeTop: String {
            localized("home_upsell_welcome_top")
        }

        static var upsellWelcomeTitle: String {
            localized("home_upsell_welcome_title")
        }

        static var upsellWelcomeCta: String {
            localized("home_upsell_welcome_cta")
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

        static var trustInsured: String {
            localized("home_trust_insured")
        }

        static var trustVetted: String {
            localized("home_trust_vetted")
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
            format("home_milestone_subtitle_v2", pointsToNext, nextTier)
        }

        static var seasonalTitle: String {
            localized("home_seasonal_title")
        }

        static var seasonalSubtitle: String {
            localized("home_seasonal_subtitle")
        }
    }
}
