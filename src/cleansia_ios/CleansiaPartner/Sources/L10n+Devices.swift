import Foundation

extension L10n {
    static var cancel: String {
        localized("cancel")
    }

    enum Devices {
        static var title: String {
            localized("devices_title")
        }

        static var intro: String {
            localized("devices_intro")
        }

        static var thisDevice: String {
            localized("devices_this_device")
        }

        static func lastActive(_ value: String) -> String {
            format("devices_last_active", value)
        }

        static var revokeButton: String {
            localized("devices_revoke_button")
        }

        static var revokeDialogTitle: String {
            localized("devices_revoke_dialog_title")
        }

        static func revokeDialogMessage(_ platform: String) -> String {
            format("devices_revoke_dialog_message", platform)
        }

        static var revokeDialogConfirm: String {
            localized("devices_revoke_dialog_confirm")
        }

        static var revokeSuccess: String {
            localized("devices_revoke_success")
        }

        static var revokeRetryHint: String {
            localized("devices_revoke_retry_hint")
        }

        static var platformAndroid: String {
            localized("devices_platform_android")
        }

        static var platformIos: String {
            localized("devices_platform_ios")
        }

        static var platformWeb: String {
            localized("devices_platform_web")
        }

        static var empty: String {
            localized("devices_empty")
        }

        static var errorMessage: String {
            localized("devices_error_message")
        }
    }
}
