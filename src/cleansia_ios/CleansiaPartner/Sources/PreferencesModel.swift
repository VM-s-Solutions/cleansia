import CleansiaCore
import Combine
import SwiftUI

/// Drives the two device-local preferences (language + theme) for the live
/// UI. Reads/writes the single `AppSettingsStore` (D4); on a language change
/// it repoints `L10n.bundle` at the selected `.lproj` and bumps `@Published`
/// state so the root re-renders and the whole UI re-localizes without a
/// restart. Theme drives `.preferredColorScheme` at the root.
@MainActor
final class PreferencesModel: ObservableObject {
    /// The resolved tag the UI is localized in (explicit choice or device
    /// locale). Drives `L10n.bundle` + the root `\.locale`.
    @Published private(set) var languageTag: String
    /// True when no explicit language is persisted — i.e. following the device
    /// locale (the "System" row). The fresh-install default.
    @Published private(set) var isFollowingSystemLanguage: Bool
    @Published private(set) var theme: Theme

    private let settings: AppSettingsStore
    /// Required, with no default, because the only thing that would notice a dropped injection is the
    /// language a payout invoice PDF renders in — a tax document the cleaner files, decided server-side
    /// from `User.PreferredLanguageCode`. `SettingsViewModel.kt` takes it the same way.
    private let languageSync: LanguagePreferenceSync

    init(settings: AppSettingsStore, languageSync: LanguagePreferenceSync) {
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

    /// "System": clear the explicit choice so the store resolves via device
    /// locale, then re-localize to that resolved language.
    func setSystemLanguage() {
        settings.clearLanguage()
        applyResolvedLanguage()
    }

    /// Applies a picker row id, where `PreferencesLabels.systemLanguageId` is
    /// the "follow the device" sentinel and anything else is a real tag.
    ///
    /// Every language picker in the app — the profile screen and the pre-auth
    /// onboarding menu — funnels through here rather than branching on the
    /// sentinel itself. That is deliberate: the tag has to reach the store,
    /// because the store is what clamps it to the five supported codes, and
    /// `RegisterViewModel` reads `settings.languageTag` straight back out to
    /// stamp the registration. A raw device tag ("de-DE") reaching the API
    /// fails `LanguageValidator` with `language.not_supported` and takes the
    /// whole signup down with it.
    func selectLanguage(id: String) {
        if id == PreferencesLabels.systemLanguageId {
            setSystemLanguage()
        } else {
            setLanguage(id)
        }
    }

    /// What reaches the server is the *resolved* tag, never the "System" sentinel: the server cannot see
    /// this handset's locale and `LanguageValidator` only accepts one of the five supported codes.
    private func applyResolvedLanguage() {
        let resolved = settings.languageTag
        L10n.bundle = Self.bundle(for: resolved)
        CoreL10n.apply(languageTag: resolved)
        languageTag = resolved
        isFollowingSystemLanguage = settings.persistedLanguageTag == nil
        languageSync.send(languageCode: resolved)
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
