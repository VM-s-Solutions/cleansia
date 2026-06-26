import Foundation

public protocol AppSettingsStore: AnyObject {
    var hasSeenOnboarding: Bool { get }
    func markOnboardingSeen()
    var languageTag: String { get }
}

public final class UserDefaultsAppSettingsStore: AppSettingsStore, @unchecked Sendable {
    public static let supportedLanguageTags = ["en", "cs", "sk", "uk", "ru"]
    private static let defaultLanguageTag = "en"

    private enum Key {
        static let onboardingSeen = "settings.onboarding_seen"
        static let language = "settings.language"
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

    public var languageTag: String {
        if let stored = defaults.string(forKey: Key.language), isSupported(stored) {
            return stored
        }
        if let seed = localeLanguageCode(), isSupported(seed) {
            return seed
        }
        return Self.defaultLanguageTag
    }

    private func isSupported(_ tag: String) -> Bool {
        Self.supportedLanguageTags.contains(tag)
    }
}
