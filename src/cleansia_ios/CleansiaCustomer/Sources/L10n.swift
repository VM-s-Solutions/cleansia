import Foundation

enum L10n {
    enum Shell {
        static var home: String {
            localized("nav_home")
        }

        static var orders: String {
            localized("nav_orders")
        }

        static var rewards: String {
            localized("nav_rewards")
        }

        static var profile: String {
            localized("nav_profile")
        }

        static var book: String {
            localized("nav_book")
        }

        static func placeholderComingSoon(_ name: String) -> String {
            format("shell_tab_placeholder", name)
        }
    }

    enum Splash {
        static var tagline: String {
            localized("splash_tagline")
        }
    }

    static var signOut: String {
        localized("profile_logout")
    }

    static var cancel: String {
        localized("common_cancel")
    }

    nonisolated(unsafe) static var bundle: Bundle = .main

    static func localized(_ key: String) -> String {
        bundle.localizedString(forKey: key, value: nil, table: nil)
    }

    static func format(_ key: String, _ args: CVarArg...) -> String {
        String(format: localized(key), arguments: args)
    }
}
