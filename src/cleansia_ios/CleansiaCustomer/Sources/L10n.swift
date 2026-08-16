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

    static var retry: String {
        localized("common_retry")
    }

    nonisolated(unsafe) static var bundle: Bundle = .main

    static func localized(_ key: String) -> String {
        bundle.localizedString(forKey: key, value: nil, table: nil)
    }

    static func format(_ key: String, _ args: CVarArg...) -> String {
        String(format: localized(key), arguments: args)
    }

    /// A key whose `.xcstrings` entry carries PLURAL VARIATIONS.
    ///
    /// `String(format:)` is not enough: plural selection is applied by
    /// `String.localizedStringWithFormat`, which resolves the variation against the current locale's
    /// rules before substituting. Calling `format` on a plural key returns whichever form happens to be
    /// the base and silently ignores the count — which is the bug this whole migration exists to fix,
    /// so it must not be reintroduced by using the wrong helper.
    static func plural(_ key: String, _ count: Int, _ extra: CVarArg...) -> String {
        // locale: .current is what applies the plural rules; String(format:) without it does not.
        String(format: localized(key), locale: .current, arguments: [count as CVarArg] + extra)
    }

    static func format(_ key: String, arguments: [CVarArg]) -> String {
        String(format: localized(key), arguments: arguments)
    }
}
