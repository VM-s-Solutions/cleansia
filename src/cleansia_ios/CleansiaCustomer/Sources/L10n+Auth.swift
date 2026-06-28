import Foundation

extension L10n {
    enum Auth {
        static var signInTitle: String {
            localized("login_title")
        }

        static var signInSubtitle: String {
            localized("login_subtitle")
        }

        static var signIn: String {
            localized("login_login")
        }

        static var signUpTitle: String {
            localized("register_title")
        }

        static var signUpSubtitle: String {
            localized("register_subtitle")
        }

        static var signUp: String {
            localized("register_submit")
        }

        static var email: String {
            localized("auth_email")
        }

        static var password: String {
            localized("auth_password")
        }

        static var rememberMe: String {
            localized("login_remember_me")
        }

        static var forgotPassword: String {
            localized("login_forgot_password")
        }

        static var dontHaveAccount: String {
            localized("login_dont_have_account")
        }

        static var signUpLink: String {
            localized("login_register")
        }

        static var alreadyHaveAccount: String {
            localized("register_already_have_account")
        }

        static var signInLink: String {
            localized("register_login_link")
        }

        static var firstName: String {
            localized("auth_first_name")
        }

        static var lastName: String {
            localized("auth_last_name")
        }

        static var confirmPassword: String {
            localized("auth_confirm_password")
        }

        static var ruleMinLength: String {
            localized("register_pw_min_length")
        }

        static var ruleLetter: String {
            localized("register_pw_letter")
        }

        static var ruleNumber: String {
            localized("register_pw_number")
        }

        static var ruleMatch: String {
            localized("register_pw_match")
        }

        static var verifyTitle: String {
            localized("verify_title")
        }

        static var verifyDescription: String {
            localized("verify_description")
        }

        static var verifySubmit: String {
            localized("verify_submit")
        }

        static var verifyResendCode: String {
            localized("verify_resend_code")
        }

        static var resendSuccess: String {
            localized("auth_resend_success")
        }

        static var forgotTitle: String {
            localized("forgot_title")
        }

        static var forgotDescription: String {
            localized("forgot_description")
        }

        static var forgotSendCode: String {
            localized("forgot_send_code")
        }

        static var forgotCodeSent: String {
            localized("forgot_code_sent")
        }

        static var forgotSendFailed: String {
            localized("forgot_send_failed")
        }

        static var rememberPassword: String {
            localized("forgot_remember_password")
        }

        static var back: String {
            localized("common_back")
        }

        static var errorEmailRequired: String {
            localized("auth_error_email_required")
        }

        static var errorEmailInvalid: String {
            localized("auth_error_email_invalid")
        }

        static var errorPasswordRequired: String {
            localized("auth_error_password_required")
        }

        static var errorFirstNameRequired: String {
            localized("auth_error_first_name_required")
        }

        static var errorLastNameRequired: String {
            localized("auth_error_last_name_required")
        }

        static var errorPasswordRules: String {
            localized("auth_error_password_rules")
        }

        static var errorPasswordsNoMatch: String {
            localized("auth_error_passwords_no_match")
        }

        static var errorGeneric: String {
            localized("auth_error_generic")
        }

        static var errorEmailSendFailed: String {
            localized("error_email_sending_failed")
        }

        static var dividerOr: String {
            localized("login_or")
        }

        static var continueWithGoogle: String {
            localized("login_continue_with_google")
        }

        static var socialNoAccount: String {
            localized("auth_social_no_account")
        }

        static var socialNotConfigured: String {
            localized("auth_social_not_configured")
        }

        static var socialFailed: String {
            localized("auth_social_failed")
        }
    }
}
