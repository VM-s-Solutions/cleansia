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

    /// The resolved language tag — an explicit choice, else the first supported
    /// entry of the device's ordered preferred languages, else "en". Always one
    /// of `supportedLanguageTags`.
    var languageTag: String { get }
    /// The explicit choice, or `nil` when following the device languages (the
    /// fresh-install "System" default).
    var persistedLanguageTag: String? { get }
    func setLanguage(_ tag: String)
    /// Clears the explicit choice so `languageTag` resolves via device languages.
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
    private let preferredLanguageTags: () -> [String]

    public init(
        defaults: UserDefaults = .standard,
        preferredLanguageTags: @escaping () -> [String] = UserDefaultsAppSettingsStore.systemPreferredLanguageTags
    ) {
        self.defaults = defaults
        self.preferredLanguageTags = preferredLanguageTags
    }

    /// `Bundle.main.preferredLocalizations` — not `Locale.current` — because it is
    /// the only source that honours the per-app language override and the full
    /// ordered preference list rather than the formatting locale.
    public static func systemPreferredLanguageTags() -> [String] {
        let bundled = Bundle.main.preferredLocalizations
        return bundled.isEmpty ? Locale.preferredLanguages : bundled
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
        let seed = preferredLanguageTags()
            .lazy
            .compactMap(Self.bareLanguageCode)
            .first(where: isSupported)
        return seed ?? Self.defaultLanguageTag
    }

    public var persistedLanguageTag: String? {
        guard let stored = defaults.string(forKey: Key.language), isSupported(stored) else {
            return nil
        }
        return stored
    }

    public func setLanguage(_ tag: String) {
        // Clamp to the supported set; an unsupported tag clears the choice and
        // falls back through the same resolve as the getter (device languages → "en").
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

    /// Preferred entries arrive region/script-qualified ("cs-CZ", "zh-Hant-TW");
    /// the supported set is bare language codes, so they must be narrowed before
    /// matching. The split covers identifiers `Locale` refuses to parse.
    private static func bareLanguageCode(_ tag: String) -> String? {
        let trimmed = tag.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else { return nil }
        if let code = Locale(identifier: trimmed).language.languageCode?.identifier, !code.isEmpty {
            return code.lowercased()
        }
        return trimmed.split(whereSeparator: { $0 == "-" || $0 == "_" }).first.map { $0.lowercased() }
    }
}
