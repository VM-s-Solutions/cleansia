import Foundation

extension L10n {
    enum Home {
        static var greeting: String {
            localized("home_greeting")
        }

        static var bookTitle: String {
            localized("home_book_title")
        }

        static var bookSubtitle: String {
            localized("home_book_subtitle")
        }

        static var bookCta: String {
            localized("home_book_cta")
        }

        static var recentOrdersTitle: String {
            localized("home_recent_orders_title")
        }

        static var seeAll: String {
            localized("home_see_all")
        }

        static var profileNudgeTitle: String {
            localized("home_profile_nudge_title")
        }

        static var profileNudgeSubtitle: String {
            localized("home_profile_nudge_subtitle")
        }
    }
}
