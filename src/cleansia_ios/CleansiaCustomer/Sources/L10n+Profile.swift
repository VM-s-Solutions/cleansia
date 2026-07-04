import Foundation

extension L10n {
    enum Profile {
        static var groupAccount: String {
            localized("profile_group_account")
        }

        static var groupPreferences: String {
            localized("profile_group_preferences")
        }

        static var groupSupport: String {
            localized("profile_group_support")
        }

        static var rowEditProfile: String {
            localized("profile_row_edit")
        }

        static var rowDisputes: String {
            localized("profile_row_disputes")
        }

        static var rowNotifications: String {
            localized("profile_row_notifications")
        }

        static var rowLanguage: String {
            localized("profile_row_language")
        }

        static var rowAppearance: String {
            localized("profile_row_appearance")
        }

        static var rowSecurity: String {
            localized("profile_row_security")
        }

        static var rowDevices: String {
            localized("profile_row_devices")
        }

        static var rowHelp: String {
            localized("profile_row_help")
        }

        static var deleteAccount: String {
            localized("profile_delete_account")
        }

        static var signOut: String {
            localized("profile_logout")
        }

        static var signOutDialogTitle: String {
            localized("profile_logout_dialog_title")
        }

        static var signOutDialogMessage: String {
            localized("profile_logout_dialog_message")
        }

        static var signOutDialogConfirm: String {
            localized("profile_logout_dialog_confirm")
        }
    }

    enum Onboarding {
        static var greeting: String {
            localized("onboarding_greeting")
        }

        static func greetingNamed(_ firstName: String) -> String {
            format("onboarding_greeting_named", firstName)
        }

        static var subtitle: String {
            localized("onboarding_subtitle")
        }

        static var phoneLabel: String {
            localized("onboarding_phone_label")
        }

        static var phoneHelper: String {
            localized("onboarding_phone_helper")
        }

        static var birthDateLabel: String {
            localized("onboarding_birthdate_label")
        }

        static var birthDatePlaceholder: String {
            localized("onboarding_birthdate_placeholder")
        }

        static var birthDateHelper: String {
            localized("onboarding_birthdate_helper")
        }

        static var save: String {
            localized("onboarding_save")
        }

        static var skip: String {
            localized("onboarding_skip")
        }
    }

    enum EditProfile {
        static var title: String {
            localized("profile_edit_title")
        }

        static var firstName: String {
            localized("profile_edit_first_name")
        }

        static var lastName: String {
            localized("profile_edit_last_name")
        }

        static var email: String {
            localized("profile_edit_email")
        }

        static var emailReadonly: String {
            localized("profile_edit_email_readonly")
        }

        static var phone: String {
            localized("profile_edit_phone")
        }

        static var birthDate: String {
            localized("profile_edit_birthdate")
        }

        static var birthDatePlaceholder: String {
            localized("onboarding_birthdate_placeholder")
        }

        static var save: String {
            localized("profile_edit_save")
        }

        static var bookingHint: String {
            localized("profile_edit_booking_hint")
        }

        static var phoneRequired: String {
            localized("profile_edit_phone_required")
        }
    }

    enum Preferences {
        static var language: String {
            localized("profile_row_language")
        }

        static var languageSystem: String {
            localized("preferences_language_system")
        }

        static var appearance: String {
            localized("profile_row_appearance")
        }

        static var themeSystem: String {
            localized("preferences_theme_system")
        }

        static var themeLight: String {
            localized("preferences_theme_light")
        }

        static var themeDark: String {
            localized("preferences_theme_dark")
        }
    }

    enum Security {
        static var title: String {
            localized("profile_security_title")
        }

        static var changePassword: String {
            localized("profile_security_change_password")
        }

        static var intro: String {
            localized("security_intro")
        }

        static var requestCode: String {
            localized("security_request_code")
        }

        static var codeSentSnackbar: String {
            localized("security_code_sent")
        }

        static var codeHelper: String {
            localized("security_code_helper")
        }

        static var codeLabel: String {
            localized("security_code_label")
        }

        static var newPassword: String {
            localized("profile_security_new")
        }

        static var confirmPassword: String {
            localized("profile_security_confirm")
        }

        static var updateButton: String {
            localized("profile_security_update")
        }

        static var changeSuccessSnackbar: String {
            localized("security_change_success")
        }

        static var passwordPolicyError: String {
            localized("security_password_policy_error")
        }

        static var passwordMismatchError: String {
            localized("security_password_mismatch_error")
        }
    }

    enum Help {
        static var title: String {
            localized("help_title")
        }

        static var contactTitle: String {
            localized("help_contact_title")
        }

        static var email: String {
            localized("help_email")
        }

        static var emailDesc: String {
            localized("help_email_desc")
        }

        static var call: String {
            localized("help_call")
        }

        static var callDesc: String {
            localized("help_call_desc")
        }

        static var faqTitle: String {
            localized("help_faq_title")
        }

        static var faqQ1: String {
            localized("help_faq_q1")
        }

        static var faqA1: String {
            localized("help_faq_a1")
        }

        static var faqQ2: String {
            localized("help_faq_q2")
        }

        static var faqA2: String {
            localized("help_faq_a2")
        }

        static var faqQ3: String {
            localized("help_faq_q3")
        }

        static var faqA3: String {
            localized("help_faq_a3")
        }

        static var faqQ4: String {
            localized("help_faq_q4")
        }

        static var faqA4: String {
            localized("help_faq_a4")
        }

        static var faqQ5: String {
            localized("help_faq_q5")
        }

        static var faqA5: String {
            localized("help_faq_a5")
        }
    }

    enum DeleteAccount {
        static var title: String {
            localized("delete_account_title")
        }

        static var subtitle: String {
            localized("delete_account_subtitle")
        }

        static var whatHappens: String {
            localized("delete_account_what_happens")
        }

        static var itemProfile: String {
            localized("delete_account_item_profile")
        }

        static var itemAddresses: String {
            localized("delete_account_item_addresses")
        }

        static var itemHistory: String {
            localized("delete_account_item_history")
        }

        static var itemDevices: String {
            localized("delete_account_item_devices")
        }

        static var itemConsents: String {
            localized("delete_account_item_consents")
        }

        static var appleRevokeNote: String {
            localized("delete_account_apple_note")
        }

        static var confirmLabel: String {
            localized("delete_account_confirm_label")
        }

        static var confirmHint: String {
            localized("delete_account_confirm_hint")
        }

        static var confirmMismatch: String {
            localized("delete_account_confirm_mismatch")
        }

        static var confirmButton: String {
            localized("delete_account_confirm_button")
        }

        static var dialogTitle: String {
            localized("delete_account_dialog_title")
        }

        static var dialogMessage: String {
            localized("delete_account_dialog_message")
        }

        static var dialogConfirm: String {
            localized("delete_account_dialog_confirm")
        }

        static var errorBlockedByOrder: String {
            localized("error_gdpr_deletion_blocked_by_order")
        }

        static var errorBlockedByInvoice: String {
            localized("error_gdpr_deletion_blocked_by_invoice")
        }

        static var errorAlreadyPending: String {
            localized("error_gdpr_deletion_already_pending")
        }
    }
}
