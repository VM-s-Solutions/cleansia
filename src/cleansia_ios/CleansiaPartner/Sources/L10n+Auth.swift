import Foundation

extension L10n {
    enum Register {
        static var title: String {
            localized("create_account")
        }

        static var subtitle: String {
            localized("register_subtitle")
        }

        static var firstName: String {
            localized("first_name")
        }

        static var lastName: String {
            localized("last_name")
        }

        static var confirmPassword: String {
            localized("confirm_password")
        }

        static var acceptTerms: String {
            localized("accept_terms")
        }

        static var submit: String {
            localized("register")
        }

        static var alreadyHaveAccount: String {
            localized("already_have_account")
        }

        static var signInHere: String {
            localized("sign_in_here")
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

        static var errorFirstNameRequired: String {
            localized("register_error_first_name_required")
        }

        static var errorLastNameRequired: String {
            localized("register_error_last_name_required")
        }

        static var errorEmailRequired: String {
            localized("register_error_email_required")
        }

        static var errorEmailInvalid: String {
            localized("register_error_email_invalid")
        }

        static var errorPasswordRules: String {
            localized("register_error_password_rules")
        }

        static var errorPasswordsNoMatch: String {
            localized("register_error_passwords_no_match")
        }

        static var errorTermsRequired: String {
            localized("register_error_terms_required")
        }
    }

    enum ForgotPassword {
        static var title: String {
            localized("forgot_password_title")
        }

        static var subtitle: String {
            localized("forgot_password_subtitle")
        }

        static var reset: String {
            localized("reset")
        }

        static var back: String {
            localized("back")
        }

        static var alreadyHaveAccount: String {
            localized("already_have_account")
        }

        static var signInHere: String {
            localized("sign_in_here")
        }

        static var errorEmailRequired: String {
            localized("register_error_email_required")
        }

        static var errorEmailInvalid: String {
            localized("register_error_email_invalid")
        }
    }

    enum Onboarding {
        static var skip: String {
            localized("onboarding_skip")
        }

        static var welcomeTitle: String {
            localized("onboarding_welcome_title")
        }

        static var welcomeBody: String {
            localized("onboarding_welcome_body")
        }

        static var readyTitle: String {
            localized("onboarding_ready_title")
        }

        static var readyBody: String {
            localized("onboarding_ready_body")
        }

        static var getStarted: String {
            localized("onboarding_get_started")
        }

        static var next: String {
            localized("onboarding_next")
        }
    }

    enum ConfirmEmail {
        static var title: String {
            localized("verify_email")
        }

        static var subtitle: String {
            localized("verify_email_subtitle")
        }

        static var verify: String {
            localized("verify")
        }

        static var resendCode: String {
            localized("resend_code")
        }

        static var resendSent: String {
            localized("confirm_email_subtitle")
        }

        static var errorGeneric: String {
            localized("error_generic")
        }

        static var back: String {
            localized("back")
        }
    }
}
