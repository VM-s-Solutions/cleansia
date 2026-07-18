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
            localized("notifications_inbox_close")
        }

        static var error: String {
            localized("notifications_inbox_error")
        }

        static var retry: String {
            localized("retry")
        }

        static func unreadA11y(_ label: String) -> String {
            format("notifications_inbox_unread_a11y", label)
        }
    }
}
