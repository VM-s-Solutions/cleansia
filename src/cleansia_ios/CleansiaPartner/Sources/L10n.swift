import Foundation

enum L10n {
    static var welcomeBack: String {
        localized("welcome_back")
    }

    static var loginSubtitle: String {
        localized("login_subtitle")
    }

    static var email: String {
        localized("email")
    }

    static var password: String {
        localized("password")
    }

    static var rememberMe: String {
        localized("remember_me")
    }

    static var forgotPassword: String {
        localized("forgot_password")
    }

    static var login: String {
        localized("login")
    }

    static var dontHaveAccount: String {
        localized("dont_have_account")
    }

    static var signUpHere: String {
        localized("sign_up_here")
    }

    static var loginErrorEmailRequired: String {
        localized("login_error_email_required")
    }

    static var loginErrorEmailInvalid: String {
        localized("login_error_email_invalid")
    }

    static var loginErrorPasswordRequired: String {
        localized("login_error_password_required")
    }

    private static func localized(_ key: String) -> String {
        String(localized: String.LocalizationValue(key))
    }
}
