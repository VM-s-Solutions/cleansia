import Foundation

extension L10n {
    enum Offers {
        static var title: String {
            localized("offers_title")
        }

        static var subtitle: String {
            localized("offers_subtitle")
        }

        static func reservedUntilToday(_ time: String) -> String {
            format("offer_reserved_until_today", time)
        }

        static func reservedUntilTomorrow(_ time: String) -> String {
            format("offer_reserved_until_tomorrow", time)
        }

        static func reservedUntilDate(_ date: String, _ time: String) -> String {
            format("offer_reserved_until_date", date, time)
        }

        static var reservedEnded: String {
            localized("offer_reserved_ended")
        }

        static var confirm: String {
            localized("offer_confirm")
        }

        static var slideToConfirm: String {
            localized("offer_slide_to_confirm")
        }

        static var confirming: String {
            localized("offer_confirming")
        }

        static var decline: String {
            localized("offer_decline")
        }

        static var declineTitle: String {
            localized("offer_decline_title")
        }

        static var declineBody: String {
            localized("offer_decline_body")
        }

        static var declineCta: String {
            localized("offer_decline_cta")
        }

        static var declinedToast: String {
            localized("offer_declined_toast")
        }

        static var empty: String {
            localized("offer_empty")
        }

        static var blockedTitle: String {
            localized("offer_blocked_title")
        }

        static func blockedBody(_ reason: String) -> String {
            format("offer_blocked_body", reason)
        }

        static var blockedDismiss: String {
            localized("offer_blocked_dismiss")
        }

        static var releaseFailedTitle: String {
            localized("offer_release_failed_title")
        }

        static func releaseFailedBody(_ reason: String) -> String {
            format("offer_release_failed_body", reason)
        }

        static var cardTitle: String {
            localized("offers_card_title")
        }

        static func cardMore(_ count: Int) -> String {
            format("offers_card_more", count)
        }

        static var cardCta: String {
            localized("offers_card_cta")
        }
    }
}
