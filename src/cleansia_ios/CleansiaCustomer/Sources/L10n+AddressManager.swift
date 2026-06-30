import Foundation

extension L10n {
    enum AddressManager {
        static var title: String {
            localized("address_manager_title")
        }

        static var empty: String {
            localized("address_manager_empty")
        }

        static var add: String {
            localized("address_manager_add")
        }

        static var options: String {
            localized("address_manager_options")
        }

        static var setDefault: String {
            localized("address_manager_set_default")
        }

        static var rename: String {
            localized("address_manager_rename")
        }

        static var renameTitle: String {
            localized("address_manager_rename_title")
        }

        static var reviewTitle: String {
            localized("address_manager_review_title")
        }

        static var labelHint: String {
            localized("address_manager_label_hint")
        }

        static var confirm: String {
            localized("address_manager_confirm")
        }

        static var deleteTitle: String {
            localized("address_manager_delete_title")
        }

        static func deleteBody(_ address: String) -> String {
            format("address_manager_delete_body", address)
        }

        static var saveAddress: String {
            localized("booking_save_address")
        }

        static var setAsDefaultToggle: String {
            localized("booking_set_as_default")
        }

        static var defaultBadge: String {
            localized("booking_address_default")
        }

        static var fallbackLabel: String {
            localized("address_manager_fallback_label")
        }

        static var profileRow: String {
            localized("address_manager_profile_row")
        }

        static var back: String {
            localized("common_back")
        }

        static var delete: String {
            localized("common_delete")
        }

        static var save: String {
            localized("common_save")
        }

        static var cancel: String {
            localized("common_cancel")
        }
    }
}
