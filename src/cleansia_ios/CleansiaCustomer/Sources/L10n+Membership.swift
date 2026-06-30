import Foundation

extension L10n {
    enum Membership {
        static var plusTitle: String {
            localized("membership_plus_title")
        }

        static var heroHeadline: String {
            localized("membership_hero_headline")
        }

        static func heroTrialPrice(_ days: Int) -> String {
            format("membership_hero_trial_price", days)
        }

        static func heroThenPrice(_ price: String) -> String {
            format("membership_hero_then_price", price)
        }

        static func heroThenPriceYear(_ price: String) -> String {
            format("membership_hero_then_price_year", price)
        }

        static func planPerMonth(_ price: String) -> String {
            format("membership_plan_per_month", price)
        }

        static func planPerYear(_ price: String) -> String {
            format("membership_plan_per_year", price)
        }

        static var socialProofHeadline: String {
            localized("membership_social_proof_headline")
        }

        static var socialProofSub: String {
            localized("membership_social_proof_sub")
        }

        static var perksSectionTitle: String {
            localized("membership_perks_section_title")
        }

        static var planMonthly: String {
            localized("membership_plan_monthly")
        }

        static var planAnnual: String {
            localized("membership_plan_annual")
        }

        static var ctaStartTrial: String {
            localized("membership_cta_start_trial")
        }

        static var ctaSubscribe: String {
            localized("membership_cta_subscribe")
        }

        static var disclosure: String {
            localized("membership_disclosure")
        }

        static func ctaDisclosureTrial(_ price: String) -> String {
            format("membership_cta_disclosure_trial", price)
        }

        static func ctaDisclosureTrialYear(_ price: String) -> String {
            format("membership_cta_disclosure_trial_year", price)
        }

        static var perkDiscountTitle: String {
            localized("membership_perk_discount_title")
        }

        static var perkDiscountDesc: String {
            localized("membership_perk_discount_desc")
        }

        static var perkCancellationTitle: String {
            localized("membership_perk_cancellation_title")
        }

        static var perkCancellationDesc: String {
            localized("membership_perk_cancellation_desc")
        }

        static var perkFavoriteCleanerTitle: String {
            localized("membership_perk_favorite_cleaner_title")
        }

        static var perkFavoriteCleanerDesc: String {
            localized("membership_perk_favorite_cleaner_desc")
        }

        static var perkRecurringTitle: String {
            localized("membership_perk_recurring_title")
        }

        static var perkRecurringDesc: String {
            localized("membership_perk_recurring_desc")
        }

        static var perkExpressTitle: String {
            localized("membership_perk_express_title")
        }

        static var perkExpressDesc: String {
            localized("membership_perk_express_desc")
        }

        static var inactiveBadge: String {
            localized("membership_inactive_badge")
        }

        static var inactiveTitle: String {
            localized("membership_inactive_title")
        }

        static var inactivePerksSummary: String {
            localized("membership_inactive_perks_summary")
        }

        static var inactiveCta: String {
            localized("membership_inactive_cta")
        }

        static var successTitle: String {
            localized("membership_success_title")
        }

        static var successSubtitle: String {
            localized("membership_success_subtitle")
        }

        static var successPerksHeader: String {
            localized("membership_success_perks_header")
        }

        static var successCtaSetupRecurring: String {
            localized("membership_success_cta_setup_recurring")
        }

        static var successCtaBackHome: String {
            localized("membership_success_cta_back_home")
        }

        static var statusActiveBadge: String {
            localized("membership_status_active_badge")
        }

        static var statusEndingBadge: String {
            localized("membership_status_ending_badge")
        }

        static func renewsOn(_ date: String) -> String {
            format("membership_renews_on", date)
        }

        static func activeUntil(_ date: String) -> String {
            format("membership_active_until", date)
        }

        static var thenEndsHint: String {
            localized("membership_then_ends_hint")
        }

        static var autoRenewHint: String {
            localized("membership_auto_renew_hint")
        }

        static var cancelAction: String {
            localized("membership_cancel_action")
        }

        static var cancelDialogTitle: String {
            localized("membership_cancel_dialog_title")
        }

        static var cancelDialogMessage: String {
            localized("membership_cancel_dialog_message")
        }

        static var cancelDialogConfirm: String {
            localized("membership_cancel_dialog_confirm")
        }

        static func cancelledUntil(_ date: String) -> String {
            format("membership_cancelled_until", date)
        }

        static func switchToAnnualCta(_ savings: Int) -> String {
            format("membership_switch_to_annual_cta", savings)
        }

        static var switchDialogTitle: String {
            localized("membership_switch_dialog_title")
        }

        static func switchDialogMessage(_ price: String) -> String {
            format("membership_switch_dialog_message", price)
        }

        static var switchDialogConfirm: String {
            localized("membership_switch_dialog_confirm")
        }

        static var switchSuccess: String {
            localized("membership_switch_success")
        }

        static var alreadyActive: String {
            localized("membership_already_active")
        }

        static var back: String {
            localized("common_back")
        }
    }
}
