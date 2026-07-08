import Foundation

extension L10n {
    enum NotificationsInbox {
        static var title: String {
            localized("notifications_inbox_title")
        }

        static var emptyTitle: String {
            localized("notifications_inbox_empty_title")
        }

        static var emptySubtitle: String {
            localized("notifications_inbox_empty_subtitle")
        }

        static var close: String {
            localized("common_close")
        }
    }
}
