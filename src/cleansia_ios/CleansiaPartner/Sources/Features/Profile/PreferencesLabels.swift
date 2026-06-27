import CleansiaCore

enum PreferencesLabels {
    /// Sentinel row id for "follow device locale" — not a real tag.
    static let systemLanguageId = "__system__"

    /// (tag, native label) for each supported language, in display order.
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

    /// Hub summary: the System label when following device locale, else the
    /// selected language's native label (Android parity).
    static func languageSummary(isFollowingSystem: Bool, tag: String) -> String {
        isFollowingSystem ? L10n.Profile.languageSystem : languageLabel(tag)
    }

    static let themes: [Theme] = [.system, .light, .dark]

    static func themeLabel(_ theme: Theme) -> String {
        switch theme {
        case .system: L10n.Profile.themeSystem
        case .light: L10n.Profile.themeLight
        case .dark: L10n.Profile.themeDark
        }
    }
}
