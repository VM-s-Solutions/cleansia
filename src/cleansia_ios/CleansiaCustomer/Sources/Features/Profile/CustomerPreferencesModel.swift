import CleansiaCore
import Combine
import SwiftUI

@MainActor
final class CustomerPreferencesModel: ObservableObject {
    @Published private(set) var languageTag: String
    @Published private(set) var isFollowingSystemLanguage: Bool
    @Published private(set) var theme: Theme

    private let settings: AppSettingsStore
    private let languageSync: LanguagePreferenceSync?

    init(settings: AppSettingsStore, languageSync: LanguagePreferenceSync? = nil) {
        self.settings = settings
        self.languageSync = languageSync
        languageTag = settings.languageTag
        isFollowingSystemLanguage = settings.persistedLanguageTag == nil
        theme = settings.theme
        L10n.bundle = Self.bundle(for: settings.languageTag)
        CoreL10n.apply(languageTag: settings.languageTag)
    }

    var locale: Locale {
        Locale(identifier: languageTag)
    }

    func setLanguage(_ tag: String) {
        settings.setLanguage(tag)
        applyResolvedLanguage()
    }

    func setSystemLanguage() {
        settings.clearLanguage()
        applyResolvedLanguage()
    }

    /// The RESOLVED tag reaches the server, never the "System" sentinel: `PreferredLanguageCode` has to be
    /// one of the five supported codes because it picks the template every server-rendered mail is written
    /// in, and the server cannot see this device's locale.
    private func applyResolvedLanguage() {
        let resolved = settings.languageTag
        L10n.bundle = Self.bundle(for: resolved)
        CoreL10n.apply(languageTag: resolved)
        languageTag = resolved
        isFollowingSystemLanguage = settings.persistedLanguageTag == nil
        if let languageSync {
            Task { await languageSync.send(languageCode: resolved) }
        }
    }

    func setTheme(_ theme: Theme) {
        settings.setTheme(theme)
        self.theme = theme
    }

    private static func bundle(for tag: String) -> Bundle {
        guard let path = Bundle.main.path(forResource: tag, ofType: "lproj"),
              let bundle = Bundle(path: path)
        else {
            return .main
        }
        return bundle
    }
}

extension Theme {
    var colorScheme: ColorScheme? {
        switch self {
        case .system: nil
        case .light: .light
        case .dark: .dark
        }
    }
}

enum CustomerPreferencesLabels {
    static let systemLanguageId = "__system__"

    static let languages: [(tag: String, label: String)] = [
        ("en", "English"),
        ("cs", "Čeština"),
        ("sk", "Slovenčina"),
        ("uk", "Українська"),
        ("ru", "Русский")
    ]

    static func languageLabel(_ tag: String) -> String {
        languages.first { $0.tag == tag }?.label ?? tag
    }

    static func languageSummary(isFollowingSystem: Bool, tag: String) -> String {
        isFollowingSystem ? L10n.Preferences.languageSystem : languageLabel(tag)
    }

    static let themes: [Theme] = [.system, .light, .dark]

    static func themeLabel(_ theme: Theme) -> String {
        switch theme {
        case .system: L10n.Preferences.themeSystem
        case .light: L10n.Preferences.themeLight
        case .dark: L10n.Preferences.themeDark
        }
    }
}
