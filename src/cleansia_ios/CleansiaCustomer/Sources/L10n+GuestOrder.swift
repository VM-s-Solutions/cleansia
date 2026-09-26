import Foundation

extension L10n {
    enum GuestOrder {
        static var entry: String {
            localized("guest_order_entry")
        }

        static var title: String {
            localized("guest_order_title")
        }

        static var intro: String {
            localized("guest_order_intro")
        }

        static var link: String {
            localized("guest_order_link")
        }

        static var lookup: String {
            localized("guest_order_lookup")
        }

        static var required: String {
            localized("guest_order_required")
        }

        static var empty: String {
            localized("guest_order_empty")
        }

        static var loading: String {
            localized("guest_order_loading")
        }

        static var cancel: String {
            localized("guest_order_cancel")
        }

        static var cannotCancel: String {
            localized("guest_order_cannot_cancel")
        }

        static var quoteRequired: String {
            localized("guest_order_preview_required")
        }
    }
}
