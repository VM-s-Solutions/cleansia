import Foundation

public enum Theme: String, CaseIterable, Sendable {
    case system
    case light
    case dark
}

public protocol AppSettingsStore: AnyObject {
    var hasSeenOnboarding: Bool { get }
    func markOnboardingSeen()

    /// Per-user variant for post-signin onboarding (the customer app): keyed on
    /// the user id so a different user signing in on the same device still gets
    /// prompted once. The partner pre-auth onboarding keeps the global flag.
    func hasSeenOnboarding(userId: String) -> Bool
    func markOnboardingSeen(userId: String)

    /// The resolved language tag — an explicit choice, else the device locale,
    /// else "en". Always one of `supportedLanguageTags`.
    var languageTag: String { get }
    /// The explicit choice, or `nil` when following the device locale (the
    /// fresh-install "System" default).
    var persistedLanguageTag: String? { get }
    func setLanguage(_ tag: String)
    /// Clears the explicit choice so `languageTag` resolves via device locale.
    func clearLanguage()

    var theme: Theme { get }
    func setTheme(_ theme: Theme)
}

public extension AppSettingsStore {
    func hasSeenOnboarding(userId _: String) -> Bool {
        hasSeenOnboarding
    }

    func markOnboardingSeen(userId _: String) {
        markOnboardingSeen()
    }
}

public final class UserDefaultsAppSettingsStore: AppSettingsStore, @unchecked Sendable {
    public static let supportedLanguageTags = ["en", "cs", "sk", "uk", "ru"]
    private static let defaultLanguageTag = "en"

    private enum Key {
        static let onboardingSeen = "settings.onboarding_seen"
        static let language = "settings.language"
        static let theme = "settings.theme"
    }

    private let defaults: UserDefaults
    private let localeLanguageCode: () -> String?

    public init(
        defaults: UserDefaults = .standard,
        localeLanguageCode: @escaping () -> String? = { Locale.current.languageCode }
    ) {
        self.defaults = defaults
        self.localeLanguageCode = localeLanguageCode
    }

    public var hasSeenOnboarding: Bool {
        defaults.bool(forKey: Key.onboardingSeen)
    }

    public func markOnboardingSeen() {
        defaults.set(true, forKey: Key.onboardingSeen)
    }

    public func hasSeenOnboarding(userId: String) -> Bool {
        defaults.bool(forKey: "\(Key.onboardingSeen)_\(userId)")
    }

    public func markOnboardingSeen(userId: String) {
        defaults.set(true, forKey: "\(Key.onboardingSeen)_\(userId)")
    }

    public var languageTag: String {
        if let stored = persistedLanguageTag {
            return stored
        }
        if let seed = localeLanguageCode(), isSupported(seed) {
            return seed
        }
        return Self.defaultLanguageTag
    }

    public var persistedLanguageTag: String? {
        guard let stored = defaults.string(forKey: Key.language), isSupported(stored) else {
            return nil
        }
        return stored
    }

    public func setLanguage(_ tag: String) {
        // Clamp to the supported set; an unsupported tag clears the choice and
        // falls back through the same resolve as the getter (device locale → "en").
        if isSupported(tag) {
            defaults.set(tag, forKey: Key.language)
        } else {
            clearLanguage()
        }
    }

    public func clearLanguage() {
        defaults.removeObject(forKey: Key.language)
    }

    public var theme: Theme {
        guard let raw = defaults.string(forKey: Key.theme), let value = Theme(rawValue: raw) else {
            return .system
        }
        return value
    }

    public func setTheme(_ theme: Theme) {
        defaults.set(theme.rawValue, forKey: Key.theme)
    }

    private func isSupported(_ tag: String) -> Bool {
        Self.supportedLanguageTags.contains(tag)
    }
}
